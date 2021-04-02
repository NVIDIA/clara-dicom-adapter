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
using Microsoft.Extensions.Options;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using xRetry;

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

            _fileSystem.Setup(p => p.Path.DirectorySeparatorChar).Returns(Path.DirectorySeparatorChar);
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
                _jobStore.Object,
                _fileSystem.Object,
                _configuration,
                _jobMetadataBuilderFactory.Object);

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
                _jobStore.Object,
                _fileSystem.Object,
                _configuration,
                _jobMetadataBuilderFactory.Object);

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
                _jobStore.Object,
                _fileSystem.Object,
                _configuration,
                _jobMetadataBuilderFactory.Object);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Job Submitter Hosted Service is running.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Cancellation requested.", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith("Job Store Service may be disposed", LogLevel.Warning, Times.Once());
        }

        [RetryFact(DisplayName = "Shall fail the job on exception")]
        public async Task ShallFailJobOnException()
        {
            var request = new InferenceJob("/job", new Job { JobId = "1", PayloadId = "1" });
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.Update(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>()));

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _jobStore.Object,
                _fileSystem.Object,
                _configuration,
                _jobMetadataBuilderFactory.Object);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Error starting job.", LogLevel.Error, Times.Once());

            _jobStore.Verify(p => p.Update(request, InferenceJobStatus.Fail), Times.Once());
        }

        [RetryFact(DisplayName = "Shall fail job on PayloadUploadException")]
        public async Task ShallFailJobOnPayloadUploadException()
        {
            var request = new InferenceJob("/job", new Job { JobId = "JID", PayloadId = "PID" });
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.Update(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>()));
            _fileSystem.Setup(p => p.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), System.IO.SearchOption.AllDirectories))
                .Returns(new string[] { "/file1", "file2", "file3" });
            _payloadsApi.Setup(p => p.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .Throws(new Exception("error"));
            _jobsApi.Setup(p => p.Start(It.IsAny<Job>()));
            _jobsApi.Setup(p => p.AddMetadata(It.IsAny<Job>(), It.IsAny<Dictionary<string, string>>()));
            _instanceCleanupQueue.Setup(p => p.QueueInstance(It.IsAny<string>()));

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _jobStore.Object,
                _fileSystem.Object,
                _configuration,
                _jobMetadataBuilderFactory.Object);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLoggingMessageBeginsWith("Error uploading file:", LogLevel.Error, Times.Exactly(3));
            _logger.VerifyLogging($"Failed to upload {3} files.", LogLevel.Error, Times.Once());

            _jobsApi.Verify(p => p.Start(request), Times.Never());
            _jobsApi.Verify(p => p.AddMetadata(It.IsAny<Job>(), It.IsAny<Dictionary<string, string>>()), Times.Never());
            _jobStore.Verify(p => p.Update(request, InferenceJobStatus.Fail), Times.Once());
            _instanceCleanupQueue.Verify(p => p.QueueInstance(It.IsAny<string>()), Times.Never());
            _jobMetadataBuilderFactory.Verify(p => p.Build(It.IsAny<bool>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<string>>()), Times.Once());
        }

        [RetryFact(DisplayName = "Shall upload metadata if not null or empty")]
        public async Task ShallExtractDicomTags()
        {
            _configuration.Value.Services.Platform.UploadMetadata = true;
            _configuration.Value.Services.Platform.MetadataDicomSource.Add("0010,0010");
            _configuration.Value.Services.Platform.MetadataDicomSource.Add("EEEE,FFFF");

            var request = new InferenceJob("/job", new Job { JobId = "JID", PayloadId = "PID" });
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.Update(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>()));
            _fileSystem.Setup(p => p.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), System.IO.SearchOption.AllDirectories))
                .Returns(new string[] { "/file1", "file2", "file3" });
            _payloadsApi.Setup(p => p.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));
            _jobsApi.Setup(p => p.Start(It.IsAny<Job>()));
            _jobsApi.Setup(p => p.AddMetadata(It.IsAny<Job>(), It.IsAny<Dictionary<string, string>>()));
            _instanceCleanupQueue.Setup(p => p.QueueInstance(It.IsAny<string>()));

            _jobMetadataBuilderFactory.Setup(p => p.Build(It.IsAny<bool>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<string>>()))
                .Returns(new JobMetadataBuilder() { { "Key", "Value" } });

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _jobStore.Object,
                _fileSystem.Object,
                _configuration,
                _jobMetadataBuilderFactory.Object);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Uploading 3 files.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Upload to payload completed.", LogLevel.Information, Times.Once());

            _jobsApi.Verify(p => p.Start(request), Times.Once());
            _jobsApi.Verify(p => p.AddMetadata(It.IsAny<Job>(), It.IsAny<Dictionary<string, string>>()), Times.Once());
            _jobStore.Verify(p => p.Update(request, InferenceJobStatus.Success), Times.Once());
            _instanceCleanupQueue.Verify(p => p.QueueInstance(It.IsAny<string>()), Times.Exactly(3));
            _jobMetadataBuilderFactory.Verify(p => p.Build(It.IsAny<bool>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<string>>()), Times.Once());
        }

        [RetryFact(DisplayName = "Shall complete request")]
        public async Task ShallCompleteRequest()
        {
            var request = new InferenceJob("/job", new Job { JobId = "JID", PayloadId = "PID" });
            _jobStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    _cancellationTokenSource.Cancel();
                    throw new OperationCanceledException();
                });
            _jobStore.Setup(p => p.Update(It.IsAny<InferenceJob>(), It.IsAny<InferenceJobStatus>()));
            _fileSystem.Setup(p => p.Directory.GetFiles(It.IsAny<string>(), It.IsAny<string>(), System.IO.SearchOption.AllDirectories))
                .Returns(new string[] { "/file1", "file2", "file3" });
            _payloadsApi.Setup(p => p.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()));
            _jobsApi.Setup(p => p.Start(It.IsAny<Job>()));
            _jobsApi.Setup(p => p.AddMetadata(It.IsAny<Job>(), It.IsAny<Dictionary<string, string>>()));
            _instanceCleanupQueue.Setup(p => p.QueueInstance(It.IsAny<string>()));

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _jobStore.Object,
                _fileSystem.Object,
                _configuration,
                _jobMetadataBuilderFactory.Object);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Uploading 3 files.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Upload to payload completed.", LogLevel.Information, Times.Once());

            _jobsApi.Verify(p => p.Start(request), Times.Once());
            _jobsApi.Verify(p => p.AddMetadata(It.IsAny<Job>(), It.IsAny<Dictionary<string, string>>()), Times.Never());
            _jobStore.Verify(p => p.Update(request, InferenceJobStatus.Success), Times.Once());
            _instanceCleanupQueue.Verify(p => p.QueueInstance(It.IsAny<string>()), Times.Exactly(3));
            _jobMetadataBuilderFactory.Verify(p => p.Build(It.IsAny<bool>(), It.IsAny<IReadOnlyList<string>>(), It.IsAny<IReadOnlyList<string>>()), Times.Once());
        }
    }
}