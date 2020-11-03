using Ardalis.GuardClauses;
using k8s;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.API;
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
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Jobs
{
    public class JobItemStatus
    {
        internal static readonly JobItemStatus Default = new JobItemStatus();
    }

    public class JobStore : IHostedService, IJobStore
    {
        private const int MaxRetryCount = 3;
        private static readonly object SyncRoot = new Object();

        private readonly ILoggerFactory _loggerFactory;
        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        private readonly ILogger<JobStore> _logger;
        private readonly IKubernetesWrapper _kubernetesClient;
        private readonly IFileSystem _fileSystem;
        private CustomResourceWatcher<JobCustomResourceList, JobCustomResource> _watcher;
        private readonly BlockingCollection<JobCustomResource> _jobs;
        private readonly HashSet<string> _jobIds;

        public JobStore(
            ILoggerFactory loggerFactory,
            ILogger<JobStore> logger,
            IOptions<DicomAdapterConfiguration> configuration,
            IKubernetesWrapper kubernetesClinet,
            IFileSystem fileSystem)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _kubernetesClient = kubernetesClinet ?? throw new ArgumentNullException(nameof(kubernetesClinet));
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

            if (task.IsCompleted)
                return task;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.Log(LogLevel.Information, "Job Store Hosted Service is stopping.");
            return Task.CompletedTask;
        }

        public async Task New(Job job, string jobName, IList<InstanceStorageInfo> instances)
        {
            Guard.Against.Null(job, nameof(job));
            Guard.Against.Null(jobName, nameof(jobName));
            Guard.Against.Null(instances, nameof(instances));

            var inferenceRequest = CreateInferenceRequest(job, jobName, instances);
            MakeACopyOfPayload(inferenceRequest);

            var crd = CreateFromItem(inferenceRequest);
            var operationResponse = await Policy
                .Handle<HttpOperationException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                {
                    _logger.Log(LogLevel.Warning, exception, $"Failed to add save new job {inferenceRequest.JobId} in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}");
                })
                .ExecuteAsync(() => _kubernetesClient.CreateNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.JobsCrd, crd));

            operationResponse.Response.EnsureSuccessStatusCode();
        }

        public async Task Complete(InferenceRequest item)
        {
            _logger.Log(LogLevel.Information, $"Removing job {item.JobId} from job store as completed.");
            await Delete(item);
        }

        public async Task Fail(InferenceRequest item)
        {
            if (++item.TryCount > MaxRetryCount)
            {
                _logger.Log(LogLevel.Information, $"Exceeded maximum job submission retries; removing job {item.JobId} from job store.");
                await Delete(item);
            }
            else
            {
                _logger.Log(LogLevel.Debug, $"Adding job {item.JobId} back to job store for retry.");
                var crd = CreateFromItem(item);
                var operationResponse = await Policy
                    .Handle<HttpOperationException>()
                    .WaitAndRetryAsync(
                        3,
                        retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                        (exception, timeSpan, retryCount, context) =>
                        {
                            _logger.Log(LogLevel.Warning, exception, $"Failed to delete job {item.JobId} in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}");
                        })
                    .ExecuteAsync(() => _kubernetesClient.UpdateNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.JobsCrd, crd, item.JobId));

                operationResponse.Response.EnsureSuccessStatusCode();
                _logger.Log(LogLevel.Information, $"Job {item.JobId} added back to job store for retry.");
            }
        }

        public InferenceRequest Take(CancellationToken cancellationToken)
        {
            return _jobs.Take(cancellationToken).Spec;
        }

        private InferenceRequest CreateInferenceRequest(Job job, string jobName, IList<InstanceStorageInfo> instances)
        {
            Guard.Against.Null(job, nameof(job));
            Guard.Against.Null(jobName, nameof(jobName));
            Guard.Against.Null(instances, nameof(instances));

            var targetStoragePath = GenerateJobPayloadsDirectory(job.JobId);
            return new InferenceRequest(targetStoragePath, job) { Instances = instances };
        }

        private string GenerateJobPayloadsDirectory(string jobId)
        {
            Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));

            var tryCount = 0;
            var path = string.Empty;
            do
            {
                path = _fileSystem.Path.Combine(_configuration.Value.Storage.Temporary, "jobs", $"{jobId}-{DateTime.UtcNow.Millisecond}");
                try
                {
                    _fileSystem.Directory.CreateDirectory(path);
                    break;
                }
                catch (Exception ex)
                {
                    if (++tryCount > 3)
                    {
                        throw;
                    }
                    _logger.Log(LogLevel.Warning, ex, $"Failed to create job payload directory {path}");
                }
            } while (true);

            _logger.Log(LogLevel.Information, $"Job payloads directory set to {path}");
            return path;
        }

        private void MakeACopyOfPayload(InferenceRequest request)
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
                        _logger.Log(LogLevel.Warning, ex, $"Error copying file to {request.JobPayloadsStoragePath}; destination may be out of disk space.  Exceeded maximum retries.");
                        break;
                    }
                    _logger.Log(LogLevel.Warning, ex, $"Error copying file to {request.JobPayloadsStoragePath}; destination may be out of disk space, will retry in {retrySleepMs}ms");
                    Thread.Sleep(retryCount * retrySleepMs);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, $"Failed to copy file {request.JobPayloadsStoragePath}");
                    break;
                }
            }
            
            _logger.Log(
                files.Count == 0 ? LogLevel.Information : LogLevel.Warning, 
                $"Copied {request.Instances.Count - files.Count} files to {request.JobPayloadsStoragePath}.");
        }

        private JobCustomResource CreateFromItem(InferenceRequest item)
        {
            return new JobCustomResource
            {
                Kind = CustomResourceDefinition.JobsCrd.Kind,
                ApiVersion = CustomResourceDefinition.JobsCrd.ApiVersion,
                Metadata = new k8s.Models.V1ObjectMeta
                {
                    Name = item.JobId
                },
                Spec = item,
                Status = JobItemStatus.Default
            };
        }

        private async Task Delete(InferenceRequest item)
        {
            var operationResponse = await Policy
                .Handle<HttpOperationException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Warning, exception, $"Failed to delete job {item.JobId} in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}");
                    })
                .ExecuteAsync(() => _kubernetesClient.DeleteNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.JobsCrd, item.JobId));

            operationResponse.Response.EnsureSuccessStatusCode();
            _logger.Log(LogLevel.Information, $"Job {item.JobId} removed from job store.");
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

        private void HandleJobEvents(WatchEventType eventType, JobCustomResource item)
        {
            lock (SyncRoot)
            {
                switch (eventType)
                {
                    case WatchEventType.Added:
                        if (!_jobIds.Contains(item.Spec.JobId))
                        {
                            _jobs.Add(item);
                            _jobIds.Add(item.Spec.JobId);
                            _logger.Log(LogLevel.Debug, $"Job added to queue {item.Spec.JobId}");
                        }
                        break;
                }
            }
        }
    }
}
