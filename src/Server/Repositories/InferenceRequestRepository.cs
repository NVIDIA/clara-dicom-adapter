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
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Polly;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Repositories
{
    public class InferenceRequestRepository : IInferenceRequestRepository
    {
        private const int MaxRetryLimit = 3;

        private readonly ILogger<InferenceRequestRepository> _logger;
        private readonly IJobs _jobsApi;
        private readonly IDicomAdapterRepository<InferenceRequest> _inferenceRequestRepository;

        public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;

        public InferenceRequestRepository(
            ILogger<InferenceRequestRepository> logger,
            IJobs jobsApi,
            IDicomAdapterRepository<InferenceRequest> inferenceRequestRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _jobsApi = jobsApi ?? throw new ArgumentNullException(nameof(jobsApi));
            _inferenceRequestRepository = inferenceRequestRepository ?? throw new ArgumentNullException(nameof(inferenceRequestRepository));
        }

        public async Task Add(InferenceRequest inferenceRequest)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", inferenceRequest.JobId }, { "TransactionId", inferenceRequest.TransactionId } });
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
                    await _inferenceRequestRepository.AddAsync(inferenceRequest);
                    await _inferenceRequestRepository.SaveChangesAsync();
                    _logger.Log(LogLevel.Debug, $"Inference request saved.");
                })
                .ConfigureAwait(false);
        }

        public async Task Update(InferenceRequest inferenceRequest, InferenceRequestStatus status)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", inferenceRequest.JobId }, { "TransactionId", inferenceRequest.TransactionId } });

            if (status == InferenceRequestStatus.Success)
            {
                inferenceRequest.State = InferenceRequestState.Completed;
                inferenceRequest.Status = InferenceRequestStatus.Success;
            }
            else
            {
                if (++inferenceRequest.TryCount > MaxRetryLimit)
                {
                    _logger.Log(LogLevel.Information, $"Exceeded maximum retries.");
                    inferenceRequest.State = InferenceRequestState.Completed;
                    inferenceRequest.Status = InferenceRequestStatus.Fail;
                }
                else
                {
                    _logger.Log(LogLevel.Information, $"Will retry later.");
                    inferenceRequest.State = InferenceRequestState.Queued;
                }
            }

            await Save(inferenceRequest);
        }

        public async Task<InferenceRequest> Take(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var inferenceRequest = _inferenceRequestRepository.FirstOrDefault(p => p.State == InferenceRequestState.Queued);

                if (!(inferenceRequest is null))
                {
                    using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", inferenceRequest.JobId }, { "TransactionId", inferenceRequest.TransactionId } });
                    inferenceRequest.State = InferenceRequestState.InProcess;
                    _logger.Log(LogLevel.Debug, $"Updating request {inferenceRequest.JobId} to InProgress.");
                    await Save(inferenceRequest);
                    return inferenceRequest;
                }
                await Task.Delay(250);
            }

            throw new OperationCanceledException("cancellation requsted");
        }

        public InferenceRequest Get(string jobId, string payloadId)
        {
            if (string.IsNullOrWhiteSpace(jobId) && string.IsNullOrWhiteSpace(payloadId))
            {
                throw new ArgumentNullException($"at least one of {nameof(jobId)} or {nameof(payloadId)} must be provided.");
            }
            var query = _inferenceRequestRepository.AsQueryable();
            if (!string.IsNullOrWhiteSpace(payloadId))
            {
                query = query.Where(p => p.PayloadId.Equals(payloadId));
            }
            if (!string.IsNullOrWhiteSpace(jobId))
            {
                query = query.Where(p => p.JobId.Equals(jobId));
            }

            return query.FirstOrDefault();
        }

        internal InferenceRequest GetByJobId(string jobId)
        {
            Guard.Against.NullOrWhiteSpace(jobId, nameof(jobId));

            return _inferenceRequestRepository.FirstOrDefault(p => p.JobId.Equals(jobId));
        }

        internal InferenceRequest GetByTransactionId(string transactionId)
        {
            Guard.Against.NullOrWhiteSpace(transactionId, nameof(transactionId));

            return _inferenceRequestRepository.FirstOrDefault(p => p.TransactionId.Equals(transactionId));
        }

        public async Task<InferenceStatusResponse> GetStatus(string id)
        {
            Guard.Against.NullOrWhiteSpace(id, nameof(id));

            var response = new InferenceStatusResponse();
            var item = GetByTransactionId(id);
            if (item is null)
            {
                item = GetByJobId(id);
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

        private async Task Save(InferenceRequest inferenceRequest)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            await Policy
                 .Handle<Exception>()
                 .WaitAndRetryAsync(
                     3,
                     retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                     (exception, timeSpan, retryCount, context) =>
                     {
                         _logger.Log(LogLevel.Error, exception, $"Error while updating inference request. Waiting {timeSpan} before next retry. Retry attempt {retryCount}.");
                     })
                 .ExecuteAsync(async () =>
                 {
                     _logger.Log(LogLevel.Debug, $"Updating inference request.");
                     await _inferenceRequestRepository.SaveChangesAsync();
                     _logger.Log(LogLevel.Information, $"Inference request updated.");
                 })
                 .ConfigureAwait(false);
        }
    }
}