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
using Nvidia.Clara.Platform;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Repositories
{
    /// <summary>
    /// Default implementation for <c>IJobRepository</c> which manages jobs that are to
    /// be submitted to Clara Platform using a database via <c>IDicomAdapterRepository<></c>.
    /// </summary>
    public class ClaraJobRepository : IJobRepository
    {
        private const int ERROR_HANDLE_DISK_FULL = 0x27;
        private const int ERROR_DISK_FULL = 0x70;
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

        /// <summary>
        /// Adds a new job to the queue (database). A copy of the payload is made to support multiple pipelines per AE Title.
        /// </summary>
        /// <param name="job">Job to be queued.</param>
        public async Task Add(InferenceJob job)
        {
            Guard.Against.Null(job, nameof(job));

            using (_logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", job.JobId }, { "PayloadId", job.PayloadId } }))
            {
                ConfigureStoragePath(job);

                // Makes a copy of the payload to support multiple pipelines per AE Title.
                // Future, consider use of persisted payloads.
                MakeACopyOfPayload(job);

                await Policy
                    .Handle<Exception>()
                    .WaitAndRetryAsync(
                        3,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, exception, $"Error saving inference job. Waiting {timeSpan} before next retry. Retry attempt {retryCount}.");
                    })
                    .ExecuteAsync(async () =>
                    {
                        await _inferenceJobRepository.AddAsync(job);
                        await _inferenceJobRepository.SaveChangesAsync();
                    })
                    .ConfigureAwait(false);
            }
        }

        public async Task Update(InferenceJob inferenceJob, InferenceJobStatus status)
        {
            Guard.Against.Null(inferenceJob, nameof(inferenceJob));

            using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", inferenceJob.JobId }, { "PayloadId", inferenceJob.PayloadId } });
            if (status == InferenceJobStatus.Success)
            {
                _logger.Log(LogLevel.Information, $"Marking inference job as completed.");
                inferenceJob.State = InferenceJobState.Completed;
            }
            else
            {
            }
            inferenceJob.LastUpdate = DateTime.UtcNow;
            await UpdateInferenceJob(inferenceJob);
        }

        public async Task<InferenceJob> Take(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var request = _inferenceJobRepository
                                .AsQueryable()
                                .Where(p => (p.State == InferenceJobState.Queued ||
                                    p.State == InferenceJobState.Created ||
                                    p.State == InferenceJobState.PayloadUploaded ||
                                    p.State == InferenceJobState.MetadataUploaded) &&
                                    p.LastUpdate < DateTime.UtcNow.AddSeconds(-_configuration.Value.Services.Platform.RetryDelaySeconds))
                                .OrderBy(p => p.LastUpdate)
                                .FirstOrDefault();

                if (!(request is null))
                {
                    using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", request.JobId }, { "PayloadId", request.PayloadId } });
                    var originalState = request.State;
                    request.State = request.State switch
                    {
                        InferenceJobState.Queued => InferenceJobState.Creating,
                        InferenceJobState.Created => InferenceJobState.MetadataUploading,
                        InferenceJobState.MetadataUploaded => InferenceJobState.PayloadUploading,
                        InferenceJobState.PayloadUploaded => InferenceJobState.Starting,
                        _ => throw new ApplicationException($"unsupported job state {request.State}")
                    };
                    _logger.Log(LogLevel.Information, $"Updating inference job {request.JobId} from {originalState } to {request.State}. (Attempt #{request.TryCount + 1}).");
                    await UpdateInferenceJob(request, cancellationToken);
                    return request;
                }
                await Task.Delay(250);
            }

            throw new OperationCanceledException("Cancellation requested.");
        }

        public async Task<InferenceJob> TransitionState(InferenceJob job, InferenceJobStatus status, CancellationToken cancellationToken = default)
        {
            Guard.Against.Null(job, nameof(job));

            if (status == InferenceJobStatus.Success)
            {
                var originalState = job.State;
                job.State = job.State switch
                {
                    InferenceJobState.Creating => InferenceJobState.Created,
                    InferenceJobState.MetadataUploading => InferenceJobState.MetadataUploaded,
                    InferenceJobState.PayloadUploading => InferenceJobState.PayloadUploaded,
                    InferenceJobState.Starting => InferenceJobState.Completed,
                    _ => throw new ApplicationException($"unsupported job state {job.State}")
                };
                job.LastUpdate = DateTime.UtcNow;
                job.TryCount = 0;

                _logger.Log(LogLevel.Information, $"Updating inference job state {job.JobId} from {originalState } to {job.State}.");
                await UpdateInferenceJob(job, cancellationToken);
            }
            else
            {
                if (++job.TryCount > _configuration.Value.Services.Platform.MaxRetries)
                {
                    _logger.Log(LogLevel.Information, $"Exceeded maximum job submission retries.");
                    job.State = InferenceJobState.Faulted;
                }
                else
                {
                    job.State = job.State switch
                    {
                        InferenceJobState.Creating => InferenceJobState.Queued,
                        InferenceJobState.MetadataUploading => InferenceJobState.Created,
                        InferenceJobState.PayloadUploading => InferenceJobState.MetadataUploaded,
                        InferenceJobState.Starting => InferenceJobState.PayloadUploaded,
                        _ => throw new ApplicationException($"unsupported job state {job.State}")
                    };
                    _logger.Log(LogLevel.Information, $"Putting inference job {job.JobId} back to {job.State} state for retry.");
                }
                job.LastUpdate = DateTime.UtcNow;
                await UpdateInferenceJob(job, cancellationToken);
            }

            return job;
        }

        private async Task UpdateInferenceJob(InferenceJob job, CancellationToken cancellationToken = default)
        {
            Guard.Against.Null(job, nameof(job));

            await Policy
                 .Handle<Exception>()
                 .WaitAndRetryAsync(
                     3,
                     retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                     (exception, timeSpan, retryCount, context) =>
                     {
                         _logger.Log(LogLevel.Warning, exception, $"Failed to update job. Waiting {timeSpan} before next retry. Retry attempt {retryCount}.");
                     })
                 .ExecuteAsync(async (cancellationTokenInsideExecution) =>
                 {
                     _logger.Log(LogLevel.Debug, $"Updating inference job.");
                     await _inferenceJobRepository.SaveChangesAsync(cancellationTokenInsideExecution);
                     _logger.Log(LogLevel.Debug, $"Inference job updated.");
                 }, cancellationToken)
                 .ConfigureAwait(false);
        }

        private void ConfigureStoragePath(InferenceJob job)
        {
            Guard.Against.Null(job, nameof(job));

            var targetStoragePath = string.Empty; ;
            if (_fileSystem.Directory.TryGenerateDirectory(_fileSystem.Path.Combine(_configuration.Value.Storage.TemporaryDataDirFullPath, "jobs", $"{job.JobId}"), out targetStoragePath))
            {
                _logger.Log(LogLevel.Information, $"Job payloads directory set to {targetStoragePath}");
                job.SetStoragePath(targetStoragePath);
            }
            else
            {
                throw new JobStoreException($"Failed to generate a temporary storage location");
            }
        }

        private void MakeACopyOfPayload(InferenceJob request)
        {
            Guard.Against.Null(request, nameof(request));

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
                catch (IOException ex) when ((ex.HResult & 0xFFFF) == ERROR_HANDLE_DISK_FULL || (ex.HResult & 0xFFFF) == ERROR_DISK_FULL)
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
                files.Count == 0 ? LogLevel.Information : LogLevel.Warning, $"Copied {request.Instances.Count - files.Count:D} files to '{request.JobPayloadsStoragePath}'.");
        }
    }
}