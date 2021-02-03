/*
 * Apache License, Version 2.0
 * Copyright 2019-2021 NVIDIA Corporation
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Repositories
{
    public class ClaraJobRepository : IJobRepository
    {
        private const int MaxRetryLimit = 3;

        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        private readonly ILogger<ClaraJobRepository> _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IDicomAdapterRepository<InferenceJob> _inferenceJobRepository;

        public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;

        public ClaraJobRepository(
            ILogger<ClaraJobRepository> logger,
            IOptions<DicomAdapterConfiguration> configuration,
            IFileSystem fileSystem,
            IDicomAdapterRepository<InferenceJob> inferenceJobRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _inferenceJobRepository = inferenceJobRepository ?? throw new ArgumentNullException(nameof(inferenceJobRepository));
        }

        public async Task Add(Job job, string jobName, IList<InstanceStorageInfo> instances)
        {
            Guard.Against.Null(job, nameof(job));
            Guard.Against.Null(jobName, nameof(jobName));
            Guard.Against.NullOrEmpty(instances, nameof(instances));

            using (_logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", job.JobId }, { "PayloadId", job.PayloadId } }))
            {
                var inferenceJob = CreateInferenceJob(job, jobName, instances);

                // Makes a copy of the payload to support multiple pipelines per AE Title.
                // Future, consider use of persisted payloads.
                MakeACopyOfPayload(inferenceJob);

                await Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(
                        3,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, exception, $"Error saving inference request. Waiting {timeSpan} before next retry. Retry attempt {retryCount}.");
                    })
                    .ExecuteAsync(async () =>
                    {
                        await _inferenceJobRepository.AddAsync(inferenceJob);
                        await _inferenceJobRepository.SaveChangesAsync();
                    })
                    .ConfigureAwait(false);
            }
        }

        public async Task Update(InferenceJob inferenceJob, InferenceJobStatus status)
        {
            using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", inferenceJob.JobId }, { "PayloadId", inferenceJob.PayloadId } });
            if (status == InferenceJobStatus.Success)
            {
                _logger.Log(LogLevel.Information, $"Removing job as completed.");
                await Delete(inferenceJob);
            }
            else
            {
                if (++inferenceJob.TryCount > MaxRetryLimit)
                {
                    _logger.Log(LogLevel.Information, $"Exceeded maximum job submission retries; removing job from job store.");
                    await Delete(inferenceJob);
                }
                else
                {
                    _logger.Log(LogLevel.Debug, $"Adding job back to job store for retry.");
                    inferenceJob.State = InferenceJobState.Queued;
                    await UpdateInferenceJob(inferenceJob);
                }
            }
        }

        public async Task<InferenceJob> Take(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var request = _inferenceJobRepository.FirstOrDefault(p => p.State == InferenceJobState.Queued);

                if (!(request is null))
                {
                    using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", request.JobId }, { "PayloadId", request.PayloadId } });
                    request.State = InferenceJobState.InProcess;
                    _logger.Log(LogLevel.Debug, $"Updating request {request.JobId} to InProgress.");
                    await UpdateInferenceJob(request);
                    return request;
                }
                await Task.Delay(250);
            }

            throw new OperationCanceledException("Cancellation requested.");
        }

        private async Task UpdateInferenceJob(InferenceJob request)
        {
            Guard.Against.Null(request, nameof(request));

            await Policy
                 .Handle<Exception>()
                 .WaitAndRetryAsync(
                     3,
                     retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                     (exception, timeSpan, retryCount, context) =>
                     {
                         _logger.Log(LogLevel.Warning, exception, $"Failed to update job. Waiting {timeSpan} before next retry. Retry attempt {retryCount}.");
                     })
                 .ExecuteAsync(async () =>
                 {
                     _logger.Log(LogLevel.Debug, $"Updating inference job.");
                     await _inferenceJobRepository.SaveChangesAsync();
                     _logger.Log(LogLevel.Debug, $"Inference job updated.");
                 })
                 .ConfigureAwait(false);
        }

        private InferenceJob CreateInferenceJob(Job job, string jobName, IList<InstanceStorageInfo> instances)
        {
            Guard.Against.Null(job, nameof(job));
            Guard.Against.Null(jobName, nameof(jobName));
            Guard.Against.Null(instances, nameof(instances));

            var targetStoragePath = string.Empty; ;
            if (_fileSystem.Directory.TryGenerateDirectory(_fileSystem.Path.Combine(_configuration.Value.Storage.Temporary, "jobs", $"{job.JobId}"), out targetStoragePath))
            {
                _logger.Log(LogLevel.Information, $"Job payloads directory set to {targetStoragePath}");
                return new InferenceJob(targetStoragePath, job) { Instances = instances };
            }
            else
            {
                throw new JobStoreException($"Failed to generate a temporary storage location");
            }
        }

        private void MakeACopyOfPayload(InferenceJob request)
        {
            _logger.Log(LogLevel.Information, $"Copying {request.Instances.Count} instances to {request.JobPayloadsStoragePath}.");
            var files = new Stack<InstanceStorageInfo>(request.Instances);
            var retrySleepMs = 1000;
            var retryCount = 0;

            while (files.Count > 0)
            {
                try
                {
                    var file = files.Peek();
                    var destPath = _fileSystem.Path.Combine(request.JobPayloadsStoragePath, $"{file.SopInstanceUid}.dcm");
                    _fileSystem.File.Copy(file.InstanceStorageFullPath, destPath, true);
                    _logger.Log(LogLevel.Debug, $"Instance {file.SopInstanceUid} moved to {destPath}");
                    files.Pop();
                }
                catch (IOException ex) when ((ex.HResult & 0xFFFF) == 0x27 || (ex.HResult & 0xFFFF) == 0x70)
                {
                    if (++retryCount > 3)
                    {
                        _logger.Log(LogLevel.Error, ex, $"Error copying file to {request.JobPayloadsStoragePath}; destination may be out of disk space.  Exceeded maximum retries.");
                        throw;
                    }
                    _logger.Log(LogLevel.Error, ex, $"Error copying file to {request.JobPayloadsStoragePath}; destination may be out of disk space, will retry in {retrySleepMs}ms");
                    Thread.Sleep(retryCount * retrySleepMs);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, $"Failed to copy file {request.JobPayloadsStoragePath}");
                    throw;
                }
            }

            _logger.Log(
                files.Count == 0 ? LogLevel.Information : LogLevel.Warning,
                $"Copied {request.Instances.Count - files.Count} files to {request.JobPayloadsStoragePath}.");
        }

        private async Task Delete(InferenceJob request)
        {
            Guard.Against.Null(request, nameof(request));

            await Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, exception, $"Failed to delete job. Waiting {timeSpan} before next retry. Retry attempt {retryCount}.");
                    })
                .ExecuteAsync(async () =>
                {
                    _inferenceJobRepository.Remove(request);
                    await _inferenceJobRepository.SaveChangesAsync();
                })
                .ConfigureAwait(false);

            _logger.Log(LogLevel.Information, $"Job removed from job store.");
        }
    }
}