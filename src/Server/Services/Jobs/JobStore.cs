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
using k8s;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Polly;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Jobs
{
    public class InferenceJobCrdStatus
    {
        internal static readonly InferenceJobCrdStatus Default = new InferenceJobCrdStatus();
    }

    public class JobStore : IHostedService, IJobStore, IClaraService
    {
        private const int MaxRetryLimit = 3;
        private static readonly object SyncRoot = new Object();

        private readonly ILoggerFactory _loggerFactory;
        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        private readonly ILogger<JobStore> _logger;
        private readonly IKubernetesWrapper _kubernetesClient;
        private readonly IFileSystem _fileSystem;
        private CustomResourceWatcher<JobCustomResourceList, JobCustomResource> _watcher;
        private readonly BlockingCollection<JobCustomResource> _jobs;
        private readonly HashSet<string> _jobIds;

        public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;

        public JobStore(
            ILoggerFactory loggerFactory,
            IOptions<DicomAdapterConfiguration> configuration,
            IKubernetesWrapper kubernetesClient,
            IFileSystem fileSystem)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = _loggerFactory.CreateLogger<JobStore>();
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _jobs = new BlockingCollection<JobCustomResource>();
            _jobIds = new HashSet<string>();
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
            _logger.Log(LogLevel.Information, "Job Store Hosted Service is stopping.");
            Status = ServiceStatus.Stopped;
            _watcher.Stop();
            return Task.CompletedTask;
        }

        public async Task Add(Job job, string jobName, IList<InstanceStorageInfo> instances)
        {
            Guard.Against.Null(job, nameof(job));
            Guard.Against.Null(jobName, nameof(jobName));
            Guard.Against.NullOrEmpty(instances, nameof(instances));

            using (_logger.BeginScope(new Dictionary<string, object> { { "JobId", job.JobId }, { "PayloadId", job.PayloadId } }))
            {
                var inferenceJob = CreateInferenceJob(job, jobName, instances);

                // Makes a copy of the payload to support multiple pipelines per AE Title.
                // Future, consider use of persisted payloads.
                MakeACopyOfPayload(inferenceJob);

                var crd = CreateFromRequest(inferenceJob);
                var operationResponse = await Policy
                    .Handle<HttpOperationException>()
                    .WaitAndRetryAsync(
                        3,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Warning, exception, $"Failed to add new job {inferenceJob.JobId} in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}. {(exception as HttpOperationException)?.Response?.Content}");
                    })
                    .ExecuteAsync(async () => await _kubernetesClient.CreateNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.JobsCrd, crd))
                    .ConfigureAwait(false);

                operationResponse.Response.EnsureSuccessStatusCode();
            }
        }

        public async Task Update(InferenceJob request, InferenceJobStatus status)
        {
            if (status == InferenceJobStatus.Success)
            {
                _logger.Log(LogLevel.Information, $"Removing job {request.JobId} from job store as completed.");
                await Delete(request);
            }
            else
            {
                if (++request.TryCount > MaxRetryLimit)
                {
                    _logger.Log(LogLevel.Information, $"Exceeded maximum job submission retries; removing job {request.JobId} from job store.");
                    await Delete(request);
                }
                else
                {
                    _logger.Log(LogLevel.Debug, $"Adding job {request.JobId} back to job store for retry.");
                    request.State = InferenceJobState.Queued;
                    _logger.Log(LogLevel.Debug, $"Updating request {request.JobId} to Queued.");
                    await UpdateInferenceJob(request);
                    _logger.Log(LogLevel.Information, $"Job {request.JobId} added back to job store for retry.");
                }
            }
        }

        public async Task<InferenceJob> Take(CancellationToken cancellationToken)
        {
            var request = _jobs.Take(cancellationToken).Spec;
            request.State = InferenceJobState.InProcess;
            _logger.Log(LogLevel.Debug, $"Updating request {request.JobId} to InProgress.");
            await UpdateInferenceJob(request);
            return request;
        }

        private async Task UpdateInferenceJob(InferenceJob request)
        {
            var crd = CreateFromRequest(request);
            var operationResponse = await Policy
                 .Handle<HttpOperationException>()
                 .WaitAndRetryAsync(
                     3,
                     retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                     (exception, timeSpan, retryCount, context) =>
                     {
                         _logger.Log(LogLevel.Warning, exception, $"Failed to update job {request.JobId} in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}. {(exception as HttpOperationException)?.Response?.Content}");
                     })
                 .ExecuteAsync(async () => await _kubernetesClient.PatchNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.JobsCrd, crd, request.JobId))
                 .ConfigureAwait(false);

            operationResponse.Response.EnsureSuccessStatusCode();
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

        private JobCustomResource CreateFromRequest(InferenceJob request)
        {
            return new JobCustomResource
            {
                Kind = CustomResourceDefinition.JobsCrd.Kind,
                ApiVersion = CustomResourceDefinition.JobsCrd.ApiVersion,
                Metadata = new k8s.Models.V1ObjectMeta
                {
                    Name = request.JobId
                },
                Spec = request,
                Status = InferenceJobCrdStatus.Default
            };
        }

        private async Task Delete(InferenceJob request)
        {
            var operationResponse = await Policy
                .Handle<HttpOperationException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Warning, exception, $"Failed to delete job {request.JobId} in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}. {(exception as HttpOperationException)?.Response?.Content}");
                    })
                .ExecuteAsync(async () => await _kubernetesClient.DeleteNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.JobsCrd, request.JobId))
                .ConfigureAwait(false);

            operationResponse.Response.EnsureSuccessStatusCode();
            _logger.Log(LogLevel.Information, $"Job {request.JobId} removed from job store.");
        }

        private async Task BackgroundProcessing(CancellationToken cancellationToken)
        {
            _logger.Log(LogLevel.Information, "Job Store Hosted Service is running.");

            _watcher = new CustomResourceWatcher<JobCustomResourceList, JobCustomResource>(
                _loggerFactory.CreateLogger<CustomResourceWatcher<JobCustomResourceList, JobCustomResource>>(),
                _kubernetesClient,
                CustomResourceDefinition.JobsCrd,
                cancellationToken,
                HandleJobEvents);

            await Task.Run(() => _watcher.Start(_configuration.Value.CrdReadIntervals));
        }

        private void HandleJobEvents(WatchEventType eventType, JobCustomResource request)
        {
            lock (SyncRoot)
            {
                switch (eventType)
                {
                    case WatchEventType.Added:
                    case WatchEventType.Modified:
                        if (!_jobIds.Contains(request.Spec.JobId) &&
                            request.Spec.State == InferenceJobState.Queued)
                        {
                            _jobs.Add(request);
                            _jobIds.Add(request.Spec.JobId);
                            _logger.Log(LogLevel.Debug, $"Job added to queue {request.Spec.JobId}");
                        }
                        break;
                }
            }
        }
    }
}