using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using System.Collections.Generic;
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
        private Mock<IJobStore> _jobStore;
        private Mock<IFileSystem> _fileSystem;
        private CancellationTokenSource _cancellationTokenSource;

        public JobSubmissionServiceTest()
        {
            _instanceCleanupQueue = new Mock<IInstanceCleanupQueue>();
            _logger = new Mock<ILogger<JobSubmissionService>>();
            _jobsApi = new Mock<IJobs>();
            _payloadsApi = new Mock<IPayloads>();
            _jobStore = new Mock<IJobStore>();
            _fileSystem = new Mock<IFileSystem>();
            _cancellationTokenSource = new CancellationTokenSource();
        }

        [Fact(DisplayName = "Shall stop processing if cancellation requested")]
        public async Task ShallStopProcessingIfCancellationRequested()
        {
            _cancellationTokenSource.Cancel();
            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _jobStore.Object,
                _fileSystem.Object);

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
                _fileSystem.Object);

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

        [Fact(DisplayName = "Shall handle InvalidOperationException")]
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
                _fileSystem.Object);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Job Submitter Hosted Service is running.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Cancellation requested.", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith("Job Store Service may be disposed:", LogLevel.Warning, Times.Once());
        }

        [Fact(DisplayName = "Shall fail the job on exception")]
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
                _fileSystem.Object);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Error uploading payloads/starting job.", LogLevel.Error, Times.Once());

            _jobStore.Verify(p => p.Update(request, InferenceJobStatus.Fail), Times.Once());
        }

        [Fact(DisplayName = "Shall complete request")]
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
            _payloadsApi.Setup(p => p.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()));
            _jobsApi.Setup(p => p.Start(It.IsAny<Job>()));
            _instanceCleanupQueue.Setup(p => p.QueueInstance(It.IsAny<string>()));

            var service = new JobSubmissionService(
                _instanceCleanupQueue.Object,
                _logger.Object,
                _jobsApi.Object,
                _payloadsApi.Object,
                _jobStore.Object,
                _fileSystem.Object);

            await service.StartAsync(_cancellationTokenSource.Token);
            BlockUntilCanceled(_cancellationTokenSource.Token);
            _logger.VerifyLogging("Uploading 3 files.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Upload to payload completed.", LogLevel.Information, Times.Once());

            _jobsApi.Verify(p => p.Start(request), Times.Once());
            _jobStore.Verify(p => p.Update(request, InferenceJobStatus.Success), Times.Once());
            _instanceCleanupQueue.Verify(p => p.QueueInstance(It.IsAny<string>()), Times.Exactly(3));
        }
    }
}