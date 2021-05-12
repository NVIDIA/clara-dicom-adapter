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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using System;
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
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IFileSystem _fileSystem;
        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;

        public JobSubmissionService(
            IInstanceCleanupQueue cleanupQueue,
            ILogger<JobSubmissionService> logger,
            IServiceScopeFactory serviceScopeFactory,
            IFileSystem fileSystem,
            IOptions<DicomAdapterConfiguration> configuration)
        {
            _cleanupQueue = cleanupQueue ?? throw new ArgumentNullException(nameof(cleanupQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
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
            using var scope = _serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            var jobsApi = scope.ServiceProvider.GetRequiredService<IJobs>();

            while (!cancellationToken.IsCancellationRequested)
            {
                await ResetStates(repository);
                await ProcessNextJob(repository, jobsApi, cancellationToken);
            }
            Status = ServiceStatus.Cancelled;
            _logger.Log(LogLevel.Information, "Cancellation requested.");
        }

        private async Task ProcessNextJob(IJobRepository repository, IJobs jobsApi, CancellationToken cancellationToken)
        {
            InferenceJob job = null;
            InferenceJobStatus status = InferenceJobStatus.Fail;
            try
            {
                _logger.Log(LogLevel.Debug, $"Waiting for new job...");
                job = await repository.Take(cancellationToken);
                using (_logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", job.JobId }, { "PayloadId", job.PayloadId } }))
                {
                    switch (job.State)
                    {
                        case InferenceJobState.Creating:
                            await CreateJob(job);
                            break;

                        case InferenceJobState.MetadataUploading:
                            await UploadMetadata(job);
                            break;

                        case InferenceJobState.PayloadUploading:
                            await UploadFiles(job, job.JobPayloadsStoragePath);
                            break;

                        case InferenceJobState.Starting:
                            await jobsApi.Start(job);
                            break;

                        default:
                            throw new InvalidOperationException($"Unsupported job state {job.State}.");
                    }
                    status = InferenceJobStatus.Success;
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
                _logger.Log(LogLevel.Error, ex, ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Error communicating with Clara Platform.");
            }
            finally
            {
                if (job != null)
                {
                    try
                    {
                        var updatedJob = await repository.TransitionState(job, status, cancellationToken);
                        if (updatedJob.State == InferenceJobState.Completed ||
                            updatedJob.State == InferenceJobState.Faulted)
                        {
                            CleanupJobFiles(updatedJob);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, ex, "Error while transitioning job state.");
                    }
                }
            }
        }

        private async Task ResetStates(IJobRepository repository)
        {
            await repository.ResetJobState();
        }

        private void CleanupJobFiles(InferenceJob job)
        {
            Guard.Against.Null(job, nameof(job));

            if (!_fileSystem.Directory.Exists(job.JobPayloadsStoragePath))
            {
                return;
            }

            using var _ = (_logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", job.JobId }, { "PayloadId", job.PayloadId } }));
            var filePaths = _fileSystem.Directory.GetFiles(job.JobPayloadsStoragePath, "*", System.IO.SearchOption.AllDirectories);
            _logger.Log(LogLevel.Debug, $"Notifying Disk Reclaimer to delete {filePaths.LongLength} files.");
            foreach (var file in filePaths)
            {
                _cleanupQueue.QueueInstance(file);
            }
            _logger.Log(LogLevel.Information, $"Notified Disk Reclaimer to delete {filePaths.LongLength} files.");
        }

        private async Task UploadMetadata(InferenceJob job)
        {
            Guard.Against.Null(job, nameof(job));

            using var scope = _serviceScopeFactory.CreateScope();
            var files = _fileSystem.Directory.GetFiles(job.JobPayloadsStoragePath, "*", System.IO.SearchOption.AllDirectories);

            var jobsMetadataFactory = scope.ServiceProvider.GetRequiredService<IJobMetadataBuilderFactory>();

            var metadata = jobsMetadataFactory.Build(
                _configuration.Value.Services.Platform.UploadMetadata,
                _configuration.Value.Services.Platform.MetadataDicomSource,
                files);

            if (!metadata.IsNullOrEmpty())
            {
                var jobsApi = scope.ServiceProvider.GetRequiredService<IJobs>();
                await jobsApi.AddMetadata(job, metadata);
            }
        }

        private async Task CreateJob(InferenceJob job)
        {
            Guard.Against.Null(job, nameof(job));

            var metadata = new JobMetadataBuilder();
            metadata.AddSourceName(job.Source);

            using var scope = _serviceScopeFactory.CreateScope();
            var jobsApi = scope.ServiceProvider.GetRequiredService<IJobs>();
            var createdJob = await jobsApi.Create(job.PipelineId, job.JobName, job.Priority, metadata);
            job.JobId = createdJob.JobId;
            job.PayloadId = createdJob.PayloadId;
            _logger.Log(LogLevel.Information, $"New JobId={job.JobId}, PayloadId={job.PayloadId}.");
        }

        private async Task UploadFiles(InferenceJob job, string basePath)
        {
            Guard.Against.Null(job, nameof(job));
            Guard.Against.Null(basePath, nameof(basePath)); // allow empty

            var filePaths = _fileSystem.Directory.GetFiles(job.JobPayloadsStoragePath, "*", System.IO.SearchOption.AllDirectories);

            if (!basePath.EndsWith(_fileSystem.Path.DirectorySeparatorChar))
            {
                basePath += _fileSystem.Path.DirectorySeparatorChar;
            }

            using var logger = _logger.BeginScope(new LogginDataDictionary<string, object> { { "BasePath", basePath }, { "JobId", job.JobId }, { "PayloadId", job.PayloadId } });

            _logger.Log(LogLevel.Information, "Uploading {0} files.", filePaths.LongLength);
            var failureCount = 0;

            var options = new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = _configuration.Value.Services.Platform.ParallelUploads
            };

            var block = new ActionBlock<string>(async (file) =>
            {
                try
                {
                    using var scope = _serviceScopeFactory.CreateScope();
                    var payloadsApi = scope.ServiceProvider.GetRequiredService<IPayloads>();
                    var name = file.Replace(basePath, "");
                    await payloadsApi.Upload(job.PayloadId, name, file);

                    // remove file immediately upon success upload to avoid another upload on next retry
                    _cleanupQueue.QueueInstance(file);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, $"Error uploading file: {file}.");
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