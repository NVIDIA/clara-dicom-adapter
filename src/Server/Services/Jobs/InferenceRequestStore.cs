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
using Nvidia.Clara.Dicom.API.Rest;
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Jobs
{
    public class InferenceRequestStore : IHostedService, IInferenceRequestStore, IClaraService
    {
        private const int MaxRetryLimit = 3;
        private static readonly object SyncRoot = new Object();

        private readonly ILoggerFactory _loggerFactory;
        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        private readonly ILogger<InferenceRequestStore> _logger;
        private readonly IKubernetesWrapper _kubernetesClient;
        private readonly IJobs _jobsApi;
        private CustomResourceWatcher<InferenceRequestCustomResourceList, InferenceRequestCustomResource> _watcher;
        private readonly BlockingCollection<InferenceRequestCustomResource> _requests;
        private readonly HashSet<string> _cache;

        public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;

        public InferenceRequestStore(
            ILoggerFactory loggerFactory,
            IOptions<DicomAdapterConfiguration> configuration,
            IKubernetesWrapper kubernetesClient,
            IJobs jobsApi)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<InferenceRequestStore>();
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
            _jobsApi = jobsApi ?? throw new ArgumentNullException(nameof(jobsApi));
            _requests = new BlockingCollection<InferenceRequestCustomResource>();
            _cache = new HashSet<string>();
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
            _logger.Log(LogLevel.Information, "Inference Request Store Hosted Service is stopping.");
            Status = ServiceStatus.Stopped;
            _watcher.Stop();
            return Task.CompletedTask;
        }

        public async Task Add(InferenceRequest inferenceRequest)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            var crd = CreateFromRequest(inferenceRequest);
            var operationResponse = await Policy
                .Handle<HttpOperationException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                {
                    _logger.Log(LogLevel.Warning, exception, $"Failed to add new inference request with JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}. {(exception as HttpOperationException)?.Response?.Content}");
                })
                .ExecuteAsync(async () =>
                {
                    var result = await _kubernetesClient.CreateNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.InferenceRequestsCrd, crd);
                    _logger.Log(LogLevel.Information, $"Inference request saved. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}");
                    return result;
                })
                .ConfigureAwait(false);

            operationResponse.Response.EnsureSuccessStatusCode();
        }

        public async Task Update(InferenceRequest inferenceRequest, InferenceRequestStatus status)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            if (status == InferenceRequestStatus.Success)
            {
                _logger.Log(LogLevel.Information, $"Archiving inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}.");
                inferenceRequest.State = InferenceRequestState.Completed;
                inferenceRequest.Status = InferenceRequestStatus.Success;
                await MoveToArchive(inferenceRequest);
                await Delete(inferenceRequest);
            }
            else
            {
                if (++inferenceRequest.TryCount > MaxRetryLimit)
                {
                    _logger.Log(LogLevel.Information, $"Exceeded maximum retries; removing inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} from Inference Request store.");
                    inferenceRequest.State = InferenceRequestState.Completed;
                    inferenceRequest.Status = InferenceRequestStatus.Fail;
                    await MoveToArchive(inferenceRequest);
                    await Delete(inferenceRequest);
                }
                else
                {
                    _logger.Log(LogLevel.Information, $"Updating inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} to Queued.");
                    inferenceRequest.State = InferenceRequestState.Queued;
                    await UpdateInferenceRequest(inferenceRequest);
                    _logger.Log(LogLevel.Information, $"Inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} added back to Inference Request store for retry.");
                }
            }

            _cache.Remove(inferenceRequest.JobId);
        }

        public async Task<InferenceRequest> Take(CancellationToken cancellationToken)
        {
            var inferenceRequest = _requests.Take(cancellationToken).Spec;
            inferenceRequest.State = InferenceRequestState.InProcess;
            _logger.Log(LogLevel.Debug, $"Updating inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} to InProgress.");
            await UpdateInferenceRequest(inferenceRequest);
            return inferenceRequest;
        }

        public async Task<InferenceRequest> Get(string jobId, string payloadId)
        {
            var labels = new Dictionary<string, string>();
            if (!string.IsNullOrWhiteSpace(payloadId))
            {
                labels.Add("PayloadId", payloadId);
            }
            if (!string.IsNullOrWhiteSpace(jobId))
            {
                labels.Add("JobId", jobId);
            }

            if (labels.Count == 0)
            {
                throw new ArgumentException("At least one of the arguments must be provided.");
            }

            var items = await QueryInferenceRequests(CustomResourceDefinition.InferenceRequestArchivesCrd, labels);

            if (items.IsNullOrEmpty())
            {
                items = await QueryInferenceRequests(CustomResourceDefinition.InferenceRequestsCrd, labels);
            }

            if (items.IsNullOrEmpty())
            {
                return null;
            }

            return items.First().Spec;
        }

        public async Task<InferenceRequest> StatusByJobId(string jobId)
        {
            Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));

            var labels = new Dictionary<string, string>();
            labels.Add("JobId", jobId);
            var items = await QueryInferenceRequests(CustomResourceDefinition.InferenceRequestArchivesCrd, labels);
            if (items.IsNullOrEmpty())
            {
                items = await QueryInferenceRequests(CustomResourceDefinition.InferenceRequestsCrd, labels);
            }

            if (items.IsNullOrEmpty())
            {
                return null;
            }

            return items.First().Spec;
        }

        public async Task<InferenceRequest> StatusByTransactionId(string transactionId)
        {
            Guard.Against.NullOrWhiteSpace(transactionId, nameof(transactionId));

            var labels = new Dictionary<string, string>();
            labels.Add("TransactionId", transactionId);
            var items = await QueryInferenceRequests(CustomResourceDefinition.InferenceRequestArchivesCrd, labels);
            if (items.IsNullOrEmpty())
            {
                items = await QueryInferenceRequests(CustomResourceDefinition.InferenceRequestsCrd, labels);
            }

            if (items.IsNullOrEmpty())
            {
                return null;
            }

            return items.First().Spec;
        }

        public async Task<InferenceStatusResponse> GetStatus(string id)
        {
            Guard.Against.NullOrWhiteSpace(id, nameof(id));

            var response = new InferenceStatusResponse();
            var item = await StatusByTransactionId(id);
            if (item is null)
            {
                item = await StatusByJobId(id);
            }

            if (item is null)
            {
                return null;
            }

            response.TransactionId = item.TransactionId;
            response.Platform.JobId = item.JobId;
            response.Platform.PayloadId = item.PayloadId;
            response.Dicom.State = item.State;
            response.Dicom.Status = item.Status;

            try
            {
                var jobDetails = await _jobsApi.Status(item.JobId);
                response.Platform.Priority = jobDetails.JobPriority;
                response.Platform.State = jobDetails.JobState;
                response.Platform.Status = jobDetails.JobStatus;
                response.Platform.Started = jobDetails.DateStarted;
                response.Platform.Stopped = jobDetails.DateStopped;
                response.Platform.Created = jobDetails.DateCreated;
                response.Message = string.Join(Environment.NewLine, jobDetails.Messages);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Error retrieving job status.");
                response.Message = $"Error retrieving job from Clara Platform: {ex.Message}";
            }

            return response;
        }

        private async Task<List<InferenceRequestCustomResource>> QueryInferenceRequests(CustomResourceDefinition customResourceDefinition, Dictionary<string, string> labels)
        {
            var operationResponse = await Policy
                .Handle<HttpOperationException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Warning, exception, $"Failed to query inference requests in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}. {(exception as HttpOperationException)?.Response?.Content}");
                    })
                .ExecuteAsync(async () =>
                {
                    return await _kubernetesClient.ListNamespacedCustomObjectWithHttpMessagesAsync(
                        CustomResourceDefinition.InferenceRequestArchivesCrd,
                        labels);
                })
                .ConfigureAwait(false);

            try
            {
                operationResponse.Response.EnsureSuccessStatusCode();
                var inferenceRequests = await CustomResourceWatcher<InferenceRequestCustomResourceList, InferenceRequestCustomResource>.DeserializeData(operationResponse);
                return inferenceRequests.Items.ToList();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, $"Failed to query CRD {customResourceDefinition.Kind}.");
                return null;
            }
        }

        private async Task MoveToArchive(InferenceRequest inferenceRequest)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            var crd = CreateFromRequest(inferenceRequest, true);
            var operationResponse = await Policy
                 .Handle<HttpOperationException>()
                 .WaitAndRetryAsync(
                     3,
                     retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                     (exception, timeSpan, retryCount, context) =>
                     {
                         _logger.Log(LogLevel.Warning, exception, $"Failed to archive inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}. {(exception as HttpOperationException)?.Response?.Content}");
                     })
                 .ExecuteAsync(async () =>
                 {
                     var result = await _kubernetesClient.CreateNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.InferenceRequestArchivesCrd, crd);
                     _logger.Log(LogLevel.Information, $"Inference request archived. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}");
                     return result;
                 })
                 .ConfigureAwait(false);

            try
            {
                operationResponse.Response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                // drop the request if it has already reached max retries.
                _logger.Log(LogLevel.Error, ex, $"Failed to archive inference request after maximum attempts.  Request will be dropped. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD. {(ex as HttpOperationException)?.Response?.Content}");
            }
        }

        private async Task UpdateInferenceRequest(InferenceRequest inferenceRequest)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            var crd = CreateFromRequest(inferenceRequest);
            var operationResponse = await Policy
                 .Handle<HttpOperationException>()
                 .WaitAndRetryAsync(
                     3,
                     retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                     (exception, timeSpan, retryCount, context) =>
                     {
                         _logger.Log(LogLevel.Warning, exception, $"Failed to update inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}. {(exception as HttpOperationException)?.Response?.Content}");
                     })
                 .ExecuteAsync(async () =>
                 {
                     var result = await _kubernetesClient.PatchNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.InferenceRequestsCrd, crd, inferenceRequest.JobId);
                     _logger.Log(LogLevel.Information, $"Inference request updated. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}");
                     return result;
                 })
                 .ConfigureAwait(false);

            operationResponse.Response.EnsureSuccessStatusCode();
        }

        private InferenceRequestCustomResource CreateFromRequest(InferenceRequest inferenceRequest, bool toBeArchived = false)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            var crd = toBeArchived ? CustomResourceDefinition.InferenceRequestArchivesCrd : CustomResourceDefinition.InferenceRequestsCrd;

            return new InferenceRequestCustomResource
            {
                Kind = crd.Kind,

                ApiVersion = crd.ApiVersion,
                Metadata = new k8s.Models.V1ObjectMeta
                {
                    Name = inferenceRequest.JobId,
                    Labels = new Dictionary<string, string> {
                        { "JobId", inferenceRequest.JobId },
                        { "PayloadId", inferenceRequest.PayloadId },
                        { "TransactionId", inferenceRequest.TransactionId }
                    }
                },
                Spec = inferenceRequest,
                Status = InferenceRequestCrdStatus.Default
            };
        }

        private async Task Delete(InferenceRequest inferenceRequest)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            var operationResponse = await Policy
                .Handle<HttpOperationException>()
                .WaitAndRetryAsync(
                    3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Warning, exception, $"Failed to delete inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD. Waiting {timeSpan} before next retry. Retry attempt {retryCount}. {(exception as HttpOperationException)?.Response?.Content}");
                    })
                .ExecuteAsync(async () =>
                {
                    var result = await _kubernetesClient.DeleteNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.InferenceRequestsCrd, inferenceRequest.JobId);
                    _logger.Log(LogLevel.Information, $"Inference request deleted. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}");
                    return result;
                })
                .ConfigureAwait(false);

            operationResponse.Response.EnsureSuccessStatusCode();
            _logger.Log(LogLevel.Information, $"Inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} removed from job store.");
        }

        private async Task BackgroundProcessing(CancellationToken cancellationToken)
        {
            _logger.Log(LogLevel.Information, "Inference Request Store Hosted Service is running.");

            _watcher = new CustomResourceWatcher<InferenceRequestCustomResourceList, InferenceRequestCustomResource>(
                _loggerFactory.CreateLogger<CustomResourceWatcher<InferenceRequestCustomResourceList, InferenceRequestCustomResource>>(),
                _kubernetesClient,
                CustomResourceDefinition.InferenceRequestsCrd,
                cancellationToken,
                HandleRequestEvents);

            await Task.Run(() => _watcher.Start(_configuration.Value.CrdReadIntervals));
        }

        private void HandleRequestEvents(WatchEventType eventType, InferenceRequestCustomResource request)
        {
            Guard.Against.Null(request, nameof(request));

            lock (SyncRoot)
            {
                switch (eventType)
                {
                    case WatchEventType.Added:
                    case WatchEventType.Modified:
                        if (!_cache.Contains(request.Spec.JobId) &&
                            request.Spec.State == InferenceRequestState.Queued)
                        {
                            _requests.Add(request);
                            _cache.Add(request.Spec.JobId);
                            _logger.Log(LogLevel.Debug, $"Inference request added to queue {request.Spec.JobId}");
                        }
                        break;
                }
            }
        }
    }
}