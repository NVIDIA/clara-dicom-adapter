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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Jobs
{
    public class JobSubmissionService : IHostedService, IClaraService
    {
        private readonly IInstanceCleanupQueue _cleanupQueue;
        private readonly ILogger<JobSubmissionService> _logger;
        private readonly IJobs _jobsApi;
        private readonly IPayloads _payloadsApi;
        private readonly IJobRepository _jobStore;
        private readonly IFileSystem _fileSystem;
        private readonly IOptions<DicomAdapterConfiguration> _configuration;

        public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;

        public JobSubmissionService(
            IInstanceCleanupQueue cleanupQueue,
            ILogger<JobSubmissionService> logger,
            IJobs jobsApi,
            IPayloads payloadsApi,
            IJobRepository jobStore,
            IFileSystem fileSystem,
            IOptions<DicomAdapterConfiguration> configuration)
        {
            _cleanupQueue = cleanupQueue ?? throw new ArgumentNullException(nameof(cleanupQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jobsApi = jobsApi ?? throw new ArgumentNullException(nameof(jobsApi));
            _payloadsApi = payloadsApi ?? throw new ArgumentNullException(nameof(payloadsApi));
            _jobStore = jobStore ?? throw new ArgumentNullException(nameof(jobStore));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var task = Task.Run(async () =>
            {
                await BackgroundProcessing(cancellationToken);
            });

            Status = ServiceStatus.Running;
            if (task.IsCompleted)
                return task;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Job Submitter Hosted Service is stopping.");
            Status = ServiceStatus.Stopped;
            return Task.CompletedTask;
        }

        private async Task BackgroundProcessing(CancellationToken cancellationToken)
        {
            _logger.Log(LogLevel.Information, "Job Submitter Hosted Service is running.");
            while (!cancellationToken.IsCancellationRequested)
            {
                InferenceJob job = null;
                try
                {
                    job = await _jobStore.Take(cancellationToken);
                    using (_logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", job.JobId }, { "PayloadId", job.PayloadId } }))
                    {
                        var files = _fileSystem.Directory.GetFiles(job.JobPayloadsStoragePath, "*", System.IO.SearchOption.AllDirectories);
                        await UploadFiles(job, job.JobPayloadsStoragePath, files);
                        await _jobsApi.Start(job);
                        await _jobStore.Update(job, InferenceJobStatus.Success);
                    }
                }
                catch (OperationCanceledException ex)
                {
                    _logger.Log(LogLevel.Warning, ex, "Job Store Service canceled: {0}");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.Log(LogLevel.Warning, ex, "Job Store Service may be disposed or Jobs API returned an error: {0}");
                }
                catch (PayloadUploadException ex)
                {
                    _logger.Log(LogLevel.Error, ex, "Error uploading payloads.");
                    if (job != null)
                    {
                        await _jobStore.Update(job, InferenceJobStatus.Fail);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, "Error starting job.");
                    if (job != null)
                    {
                        await _jobStore.Update(job, InferenceJobStatus.Fail);
                    }
                }
            }
            Status = ServiceStatus.Cancelled;
            _logger.Log(LogLevel.Information, "Cancellation requested.");
        }

        private async Task UploadFiles(Job job, string basePath, IList<string> filePaths)
        {
            Guard.Against.Null(job, nameof(job));
            Guard.Against.Null(basePath, nameof(basePath)); // allow empty
            Guard.Against.Null(filePaths, nameof(filePaths));

            if (!basePath.EndsWith(_fileSystem.Path.DirectorySeparatorChar))
            {
                basePath += _fileSystem.Path.DirectorySeparatorChar;
            }

            using var logger = _logger.BeginScope(new LogginDataDictionary<string, object> { { "BasePath", basePath }, { "JobId", job.JobId }, { "PayloadId", job.PayloadId } });

            _logger.Log(LogLevel.Information, "Uploading {0} files.", filePaths.Count);
            var failureCount = 0;

            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = _configuration.Value.Services.Platform.ParallelUploads
            };

            var block = new ActionBlock<string>(async (file) =>
            {
                try
                {
                    var name = file.Replace(basePath, "");
                    await _payloadsApi.Upload(job.PayloadId, name, file);

                    // remove file immediately upon success upload to avoid another upload on next retry
                    _cleanupQueue.QueueInstance(file);
                }
                catch (Exception)
                {
                    Interlocked.Increment(ref failureCount);
                }
            }, options);

            foreach (var file in filePaths)
            {
                block.Post(file);
            }

            block.Complete();
            await block.Completion;
            if (failureCount != 0)
            {
                throw new PayloadUploadException($"Failed to upload {failureCount} files.");
            }

            _logger.Log(LogLevel.Information, "Upload to payload completed.");
        }
    }
}