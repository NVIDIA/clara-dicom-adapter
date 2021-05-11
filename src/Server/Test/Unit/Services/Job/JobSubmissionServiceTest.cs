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

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class JobSubmissionServiceTest
    {
        private Mock<IInstanceCleanupQueue> _instanceCleanupQueue;
        private Mock<ILogger<JobSubmissionService>> _logger;
        private Mock<IJobs> _jobsApi;
        private Mock<IPayloads> _payloadsApi;
        private Mock<IJobRepository> _jobStore;
        private Mock<IFileSystem> _fileSystem;
        private Mock<IJobMetadataBuilderFactory> _jobMetadataBuilderFactory;
        private Mock<IServiceScopeFactory> _serviceScopeFactory;
        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        private CancellationTokenSource _cancellationTokenSource;

        public JobSubmissionServiceTest()
        {
            _instanceCleanupQueue = new Mock<IInstanceCleanupQueue>();
            _logger = new Mock<ILogger<JobSubmissionService>>();
            _jobsApi = new Mock<IJobs>();
            _payloadsApi = new Mock<IPayloads>();
            _jobStore = new Mock<IJobRepository>();
            _fileSystem = new Mock<IFileSystem>();
            _jobMetadataBuilderFactory = new Mock<IJobMetadataBuilderFactory>();
            _configuration = Options.Create(new DicomAdapterConfiguration());
            _cancellationTokenSource = new CancellationTokenSource();
            _serviceScopeFactory = new Mock<IServiceScopeFactory>();

            _fileSystem.Setup(p => p.Path.DirectorySeparatorChar).Returns(Path.DirectorySeparatorChar);
            
            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(x => x.GetService(typeof(IJobRepository)))
                .Returns(_jobStore.Object);
            serviceProvider
                .Setup(x => x.GetService(typeof(IJobMetadataBuilderFactory)))
                .Returns(_jobMetadataBuilderFactory.Object);
            
            var scope = new Mock<IServiceScope>();
            scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

            _serviceScopeFactory.Setup(p => p.CreateScope()).Returns(scope.Object);
        }

        [Fact(DisplayName = "Constructor - throws on null params")]
        public void Constructor_ThrowsOnNullParams()
        {
            Assert.Throws<ArgumentNullException>(() => new JobSubmissionService(null, null, null, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new JobSubmissionService(_instanceCleanupQueue.Object, null, null, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new JobSubmissionService(_instanceCleanupQueue.Object, _logger.Object, null, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new JobSubmissionService(_instanceCleanupQueue.Object, _logger.Object, _jobsApi.Object, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new JobSubmissionService(_instanceCleanupQueue.Object, _logger.Object, _jobsApi.Object, _payloadsApi.Object, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new JobSubmissionService(_instanceCleanupQueue.Object, _logger.Object, _jobsApi.Object, _payloadsApi.Object, _serviceScopeFactory.Object, null, null));
            Assert.Throws<ArgumentNullException>(() => new JobSubmissionService(_instanceCleanupQueue.Object, _logger.Object, _jobsApi.Object, _payloadsApi.Object, _serviceScopeFactory.Object, _fileSystem.Object, null));
        }

        [RetryFact(DisplayName = "Shall stop processing if cancellation requested")]
        public async Task ShallStopProcessingIfCancellationRequested()
        {
            _cancellationTokenSource.Cancel();
            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);

            _logger.VerifyLogging("Job Submitter Hosted Service is running.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Cancellation requested.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "Shall handle OperationCanceledException")]
        public async Task ShallHandleOperationCanceledException()
        {
            _jobStore.Setup(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns((CancellationToken token) =>
                {
                    BlockUntilCanceled(token);
                    throw new OperationCanceledException("canceled");
                });

            _cancellationTokenSource.CancelAfter(250);
            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Job Submitter Hosted Service is running.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Cancellation requested.", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith("Job Store Service canceled:", LogLevel.Warning, Times.Once());
        }

        private void BlockUntilCanceled(CancellationToken token, int extraWaitTimeMs = 100)
        {
            while (!token.IsCancellationRequested)
            {
                Thread.Sleep(10 + extraWaitTimeMs);
            }
        }

        [RetryFact(DisplayName = "Shall handle InvalidOperationException")]
        public async Task ShallHandleInvalidOperationException()
        {
            _jobStore.Setup(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns((CancellationToken token) =>
                {
                    BlockUntilCanceled(token);
                    throw new InvalidOperationException("canceled");
                });

            _cancellationTokenSource.CancelAfter(250);
            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Job Submitter Hosted Service is running.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Cancellation requested.", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith("Job Store Service may be disposed", LogLevel.Warning, Times.Once());
        }

        [RetryFact(DisplayName = "Shall report failure on exception")]
        public async Task ShallReportFailureOnException()
        {
            var request = new InferenceJob
            {
                JobId = "1",
                PayloadId = "1",
                State = InferenceJobState.Creating,
                Source = "Source"
            };
            request.SetStoragePath("/job");
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.TransitionState(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>(), It.IsAny<CancellationToken>()));
            _jobsApi.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>(), It.IsAny<IDictionary<string, string>>()))
                .Throws(new Exception("error"));

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Error communicating with Clara Platform.", LogLevel.Error, Times.Once());
            _jobStore.Verify(p => p.TransitionState(request, InferenceJobStatus.Fail, It.IsAny<CancellationToken>()), Times.Once());
            _jobsApi.Verify(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>(), It.IsAny<IDictionary<string, string>>()), Times.Once());
        }

        [RetryFact(DisplayName = "Creates job and transitions state")]
        public async Task CreatesJobAndTransitionState()
        {
            var request = new InferenceJob
            {
                JobId = "1",
                PayloadId = "1",
                State = InferenceJobState.Creating,
                Source = "Source"
            };
            request.SetStoragePath("/job");
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.TransitionState(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>(), It.IsAny<CancellationToken>()));
            _jobsApi.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>(), It.IsAny<IDictionary<string, string>>()))
                .ReturnsAsync(new Job { JobId = "1", PayloadId = "2" });

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _jobStore.Verify(p => p.TransitionState(request, InferenceJobStatus.Success, It.IsAny<CancellationToken>()), Times.Once());
            _jobsApi.Verify(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>(), It.IsAny<IDictionary<string, string>>()), Times.Once());
            _logger.VerifyLogging($"New JobId={1}, PayloadId={2}.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "Uploads metadata and transitions state")]
        public async Task UploadsMetadataAndTransitionsState()
        {
            var request = new InferenceJob
            {
                JobId = "1",
                PayloadId = "1",
                State = InferenceJobState.MetadataUploading,
                Source = "Source"
            };
            request.SetStoragePath("/job");
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.TransitionState(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>(), It.IsAny<CancellationToken>()));
            _fileSystem.Setup(p => p.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()))
                .Returns(new string[] { "/file1", "/file2" });
            _jobMetadataBuilderFactory.Setup(p => p.Build(It.IsAny<bool>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<string>>()))
                .Returns(new JobMetadataBuilder() { { "Test", "TestValue" } });

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _jobStore.Verify(p => p.TransitionState(request, InferenceJobStatus.Success, It.IsAny<CancellationToken>()), Times.Once());
            _fileSystem.Verify(p => p.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<SearchOption>()), Times.Once());
            _jobMetadataBuilderFactory.Verify(p => p.Build(It.IsAny<bool>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<string>>()), Times.Once());
        }

        [RetryFact(DisplayName = "Shall fail job on PayloadUploadException")]
        public async Task ShallFailJobOnPayloadUploadException()
        {
            var request = new InferenceJob
            {
                JobId = "1",
                PayloadId = "1",
                State = InferenceJobState.PayloadUploading,
                Source = "Source"
            };
            request.SetStoragePath("/job");
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.TransitionState(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>(), It.IsAny<CancellationToken>()));
            _fileSystem.Setup(p => p.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), System.IO.SearchOption.AllDirectories))
                .Returns(new string[] { "/file1", "file2", "file3" });
            _payloadsApi.Setup(p => p.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new Exception("error"));
            _instanceCleanupQueue.Setup(p => p.QueueInstance(It.IsAny<string>()));

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLoggingMessageBeginsWith("Error uploading file:", LogLevel.Error, Times.Exactly(3));
            _logger.VerifyLogging($"Failed to upload {3} files.", LogLevel.Error, Times.Once());

            _jobStore.Verify(p => p.TransitionState(request, InferenceJobStatus.Fail, It.IsAny<CancellationToken>()), Times.Once());
            _instanceCleanupQueue.Verify(p => p.QueueInstance(It.IsAny<string>()), Times.Never());
        }

        [RetryFact(DisplayName = "Uploads payload and transitions state")]
        public async Task UploadsPayloadAndTransitionsState()
        {
            var request = new InferenceJob
            {
                JobId = "1",
                PayloadId = "1",
                State = InferenceJobState.PayloadUploading,
                Source = "Source"
            };
            request.SetStoragePath("/job");
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.TransitionState(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>(), It.IsAny<CancellationToken>()));
            _fileSystem.Setup(p => p.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), System.IO.SearchOption.AllDirectories))
                .Returns(new string[] { "/file1", "/file2", "/file3" });
            _payloadsApi.Setup(p => p.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));
            _instanceCleanupQueue.Setup(p => p.QueueInstance(It.IsAny<string>()));

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Uploading 3 files.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Upload to payload completed.", LogLevel.Information, Times.Once());

            _jobStore.Verify(p => p.TransitionState(request, InferenceJobStatus.Success, It.IsAny<CancellationToken>()), Times.Once());
            _jobsApi.Verify(p => p.AddMetadata(It.IsAny<Job>(), It.IsAny<Dictionary<string, string>>()), Times.Never());
            _instanceCleanupQueue.Verify(p => p.QueueInstance(It.IsAny<string>()), Times.Exactly(3));
        }

        [RetryFact(DisplayName = "starts job and transitions state")]
        public async Task StartsJobAndTransitionsState()
        {
            var request = new InferenceJob
            {
                JobId = "1",
                PayloadId = "1",
                State = InferenceJobState.Starting,
                Source = "Source"
            };
            request.SetStoragePath("/job");
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.TransitionState(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>(), It.IsAny<CancellationToken>()));
            _jobsApi.Setup(p => p.Start(It.IsAny<Job>()));

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _jobStore.Verify(p => p.TransitionState(request, InferenceJobStatus.Success, It.IsAny<CancellationToken>()), Times.Once());
            _jobsApi.Verify(p => p.Start(It.IsAny<Job>()), Times.Once());
        }

        [RetryFact(DisplayName = "Shall throw on unsupported job state and transitions state")]
        public async Task ShallThrowWithUnsupportedJobState()
        {
            var request = new InferenceJob
            {
                JobId = "1",
                PayloadId = "1",
                State = InferenceJobState.Created,
                Source = "Source"
            };
            request.SetStoragePath("/job");
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.TransitionState(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>(), It.IsAny<CancellationToken>()));

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _jobStore.Verify(p => p.TransitionState(request, InferenceJobStatus.Fail, It.IsAny<CancellationToken>()), Times.Once());
        }

        [RetryFact(DisplayName = "Shall log error on failures when transitioning job state")]
        public async Task ShallLogErrorOnJobTransitionError()
        {
            var request = new InferenceJob
            {
                JobId = "1",
                PayloadId = "1",
                State = InferenceJobState.Created,
                Source = "Source"
            };
            request.SetStoragePath("/job");
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.TransitionState(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>(), It.IsAny<CancellationToken>()))
                .Throws(new Exception("error"));

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _serviceScopeFactory.Object,
                _fileSystem.Object,
                _configuration);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _jobStore.Verify(p => p.TransitionState(request, InferenceJobStatus.Fail, It.IsAny<CancellationToken>()), Times.Once());
            _logger.VerifyLogging("Error while transitioning job state.", LogLevel.Error, Times.Once());
        }
    }
}