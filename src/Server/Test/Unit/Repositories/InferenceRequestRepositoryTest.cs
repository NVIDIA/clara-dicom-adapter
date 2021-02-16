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

using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class InferenceRequestRepositoryTest
    {
        private Mock<ILogger<InferenceRequestRepository>> _logger;
        private Mock<IJobs> _jobsApi;
        private Mock<IDicomAdapterRepository<InferenceRequest>> _inferenceRequestRepository;

        public InferenceRequestRepositoryTest()
        {
            _logger = new Mock<ILogger<InferenceRequestRepository>>();
            _jobsApi = new Mock<IJobs>();
            _inferenceRequestRepository = new Mock<IDicomAdapterRepository<InferenceRequest>>();
        }

        [RetryFact(DisplayName = "Constructor")]
        public void ConstructorTest()
        {
            Assert.Throws<ArgumentNullException>(() => new InferenceRequestRepository(null, null, null));
            Assert.Throws<ArgumentNullException>(() => new InferenceRequestRepository(_logger.Object, null, null));
            Assert.Throws<ArgumentNullException>(() => new InferenceRequestRepository(_logger.Object, _jobsApi.Object, null));

            new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);
        }

        [RetryFact(DisplayName = "Add - Shall retry on failure")]
        public async Task Add_ShallRetryOnFailure()
        {
            _inferenceRequestRepository.Setup(p => p.AddAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("error"));

            var inferenceRequest = new InferenceRequest();
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);

            await Assert.ThrowsAsync<Exception>(async () => await store.Add(inferenceRequest));

            _logger.VerifyLoggingMessageBeginsWith($"Error saving inference request", LogLevel.Error, Times.Exactly(3));
            _inferenceRequestRepository.Verify(p => p.AddAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()), Times.AtLeast(3));
        }

        [RetryFact(DisplayName = "Add - Shall add new job")]
        public async Task Add_ShallAddJob()
        {
            var inferenceRequest = new InferenceRequest();
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);
            await store.Add(inferenceRequest);

            _inferenceRequestRepository.Verify(p => p.AddAsync(It.IsAny<InferenceRequest>(), It.IsAny<CancellationToken>()), Times.Once());
            _inferenceRequestRepository.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
            _logger.VerifyLoggingMessageBeginsWith($"Inference request saved.", LogLevel.Debug, Times.Once());
        }

        [RetryFact(DisplayName = "Update - Shall retry on failure")]
        public async Task UpdateSuccess_ShallRetryOnFailure()
        {
            var inferenceRequest = new InferenceRequest();
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            _inferenceRequestRepository.Setup(p => p.SaveChangesAsync(It.IsAny<CancellationToken>())).Throws(new Exception("error"));

            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);

            await Assert.ThrowsAsync<Exception>(() => store.Update(inferenceRequest, InferenceRequestStatus.Success));

            _logger.VerifyLoggingMessageBeginsWith($"Error while updating inference request", LogLevel.Error, Times.Exactly(3));
            _inferenceRequestRepository.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.AtLeast(3));
        }

        [RetryFact(DisplayName = "Update - Shall save")]
        public async Task UpdateSuccess_ShallSave()
        {
            var inferenceRequest = new InferenceRequest();
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);

            await store.Update(inferenceRequest, InferenceRequestStatus.Fail);

            _logger.VerifyLogging($"Updating inference request.", LogLevel.Debug, Times.Once());
            _logger.VerifyLogging($"Inference request updated.", LogLevel.Information, Times.Once());
            _inferenceRequestRepository.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [RetryFact(DisplayName = "Take - Shall return next queued")]
        public async Task Take_ShallReturnQueuedItem()
        {
            var inferenceRequest = new InferenceRequest();
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();
            var cancellationSource = new CancellationTokenSource();

            _inferenceRequestRepository.SetupSequence(p => p.FirstOrDefault(It.IsAny<Func<InferenceRequest, bool>>()))
                .Returns(inferenceRequest);

            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);

            var result = await store.Take(cancellationSource.Token);

            Assert.Equal(result.JobId, inferenceRequest.JobId);
            _logger.VerifyLogging($"Updating request {inferenceRequest.JobId} to InProgress.", LogLevel.Debug, Times.AtLeastOnce());
        }

        [RetryFact(DisplayName = "Take - Shall throw when cancelled")]
        public async Task Take_ShallThrowWhenCancelled()
        {
            var cancellationSource = new CancellationTokenSource();
            _inferenceRequestRepository.Setup(p => p.FirstOrDefault(It.IsAny<Func<InferenceRequest, bool>>()))
                .Returns(default(InferenceRequest));

            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);
            cancellationSource.CancelAfter(100);
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await store.Take(cancellationSource.Token));
        }

        [Fact(DisplayName = "Get - throws if no arguments provided")]
        public void Get_ThrowsIfNoArgumentsProvided()
        {
            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);
            Assert.Throws<ArgumentNullException>(() => store.Get(string.Empty, " "));
        }

        [Fact(DisplayName = "Get - retrieves by jobId")]
        public void Get_RetrievesByJobId()
        {
            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);
            var jobId = Guid.NewGuid().ToString();
            var inferenceRequest = store.Get(jobId, "");
            _inferenceRequestRepository.Verify(p => p.AsQueryable(), Times.Once());
        }

        [Fact(DisplayName = "Get - retrieves by payloadId")]
        public void Get_RetrievesByPayloadId()
        {
            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);
            var payloadId = Guid.NewGuid().ToString();
            var inferenceRequest = store.Get(" ", payloadId);
            _inferenceRequestRepository.Verify(p => p.AsQueryable(), Times.Once());
        }

        [Fact(DisplayName = "Status - retrieves by transaction id")]
        public async Task Status_RetrievesByTransactionId()
        {
            var jobTime = DateTime.Now;
            Platform.JobId.TryParse("job id", out Platform.JobId jobId);
            _jobsApi.Setup(p => p.Status(It.IsAny<string>()))
                .Returns(Task.FromResult(new Platform.JobDetails
                {
                    DateCreated = jobTime,
                    DateStarted = jobTime,
                    DateStopped = jobTime,
                    JobId = jobId,
                    JobState = Platform.JobState.Running,
                    JobPriority = Platform.JobPriority.Higher,
                    JobStatus = Platform.JobStatus.Healthy,
                }));
            _inferenceRequestRepository.Setup(p => p.FirstOrDefault(It.IsAny<Func<InferenceRequest, bool>>()))
                .Returns(new InferenceRequest
                {
                    TransactionId = "My Transaction ID",
                    JobId = jobId.ToString()
                });

            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);
            var id = Guid.NewGuid().ToString();
            var status = await store.GetStatus(id);

            Assert.Equal("My Transaction ID", status.TransactionId);
            Assert.Equal(jobId.ToString(), status.Platform.JobId);
            Assert.Equal(Platform.JobState.Running, status.Platform.State);
            Assert.Equal(Platform.JobPriority.Higher, status.Platform.Priority);
            Assert.Equal(Platform.JobStatus.Healthy, status.Platform.Status);
            Assert.Equal(jobTime, status.Platform.Started);
            Assert.Equal(jobTime, status.Platform.Stopped);
            Assert.Equal(jobTime, status.Platform.Created);

            _inferenceRequestRepository.Verify(p => p.FirstOrDefault(It.IsAny<Func<InferenceRequest, bool>>()), Times.Once());
            _jobsApi.Verify(p => p.Status(It.IsAny<string>()), Times.Once());
        }

        [Fact(DisplayName = "Status - retrieves by job id")]
        public async Task Status_RetrievesByJobIdIfUnableToLocateByTransaction()
        {
            Platform.JobId.TryParse("job id", out Platform.JobId jobId);
            var jobTime = DateTime.Now;
            _jobsApi.Setup(p => p.Status(It.IsAny<string>()))
                .Returns(Task.FromResult(new Platform.JobDetails
                {
                    DateCreated = jobTime,
                    DateStarted = jobTime,
                    DateStopped = jobTime,
                    JobId = jobId,
                    JobState = Platform.JobState.Running,
                    JobPriority = Platform.JobPriority.Higher,
                    JobStatus = Platform.JobStatus.Healthy,
                }));
            _inferenceRequestRepository.SetupSequence(p => p.FirstOrDefault(It.IsAny<Func<InferenceRequest, bool>>()))
                .Returns(default(InferenceRequest))
                .Returns(new InferenceRequest
                {
                    TransactionId = "My Transaction ID",
                    JobId = jobId.ToString()
                });

            var store = new InferenceRequestRepository(_logger.Object, _jobsApi.Object, _inferenceRequestRepository.Object);
            var id = Guid.NewGuid().ToString();
            var status = await store.GetStatus(id);

            Assert.Equal("My Transaction ID", status.TransactionId);
            Assert.Equal(jobId.ToString(), status.Platform.JobId);
            Assert.Equal(Platform.JobState.Running, status.Platform.State);
            Assert.Equal(Platform.JobPriority.Higher, status.Platform.Priority);
            Assert.Equal(Platform.JobStatus.Healthy, status.Platform.Status);
            Assert.Equal(jobTime, status.Platform.Started);
            Assert.Equal(jobTime, status.Platform.Stopped);
            Assert.Equal(jobTime, status.Platform.Created);

            _inferenceRequestRepository.Verify(p => p.FirstOrDefault(It.IsAny<Func<InferenceRequest, bool>>()), Times.Exactly(2));
            _jobsApi.Verify(p => p.Status(It.IsAny<string>()), Times.Once());
        }
    }
}