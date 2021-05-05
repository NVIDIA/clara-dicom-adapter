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
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class ClaraJobRepositoryTest
    {
        private IOptions<DicomAdapterConfiguration> _configuration;
        private Mock<ILogger<ClaraJobRepository>> _logger;
        private MockFileSystem _fileSystem;
        private Mock<IDicomAdapterRepository<InferenceJob>> _inferenceJobRepository;

        public ClaraJobRepositoryTest()
        {
            _logger = new Mock<ILogger<ClaraJobRepository>>();
            _configuration = Options.Create(new DicomAdapterConfiguration());
            _fileSystem = new MockFileSystem();
            _inferenceJobRepository = new Mock<IDicomAdapterRepository<InferenceJob>>();
        }

        [RetryFact(DisplayName = "Add - Shall retry on failure")]
        public async Task Add_ShallRetryOnFailure()
        {
            var job = new InferenceJob();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();
            job.Instances.Add(InstanceGenerator.GenerateInstance("./aet", "aet", fileSystem: _fileSystem));

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            _inferenceJobRepository.Setup(p => p.AddAsync(It.IsAny<InferenceJob>(), It.IsAny<CancellationToken>())).Throws(new Exception("error"));

            await Assert.ThrowsAsync<Exception>(async () => await jobStore.Add(job));

            _logger.VerifyLoggingMessageBeginsWith($"Error saving inference job.", LogLevel.Error, Times.Exactly(3));
        }

        [RetryFact(DisplayName = "Add - Shall add new job")]
        public async Task Add_ShallAddItem()
        {
            var job = new InferenceJob();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();
            job.Instances.Add(InstanceGenerator.GenerateInstance("./aet", "aet", fileSystem: _fileSystem));

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            await jobStore.Add(job);

            _inferenceJobRepository.Verify(p => p.AddAsync(It.IsAny<InferenceJob>(), It.IsAny<CancellationToken>()), Times.Once());
            _inferenceJobRepository.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [RetryFact(DisplayName = "Add - Shall retry copy when disk is full then throw")]
        public async Task Add_ShallRetryCopyThenThrow()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(p => p.Directory).Returns(_fileSystem.Directory);
            fileSystem.Setup(p => p.Path).Returns(_fileSystem.Path);
            fileSystem.Setup(p => p.File.Create(It.IsAny<string>()))
                .Returns((string path) => _fileSystem.File.Create(path));
            fileSystem.Setup(p => p.File.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
                .Throws(new IOException("error", ClaraJobRepository.ERROR_DISK_FULL));

            var job = new InferenceJob();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();
            job.SetStoragePath("/path/to/job");
            job.Instances.Add(InstanceGenerator.GenerateInstance("./aet", "aet", fileSystem: fileSystem.Object));
            _configuration.Value.Storage.Temporary = "./aet";

            var cancellationSource = new CancellationTokenSource();
            _inferenceJobRepository.SetupSequence(p => p.AsQueryable())
                .Returns((new List<InferenceJob>() { job }).AsQueryable());

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                fileSystem.Object,
                _inferenceJobRepository.Object);

            await Assert.ThrowsAsync<IOException>(async () => await jobStore.Add(job));

            _logger.VerifyLoggingMessageBeginsWith($"Error copying file to {job.JobPayloadsStoragePath}; destination may be out of disk space, will retry in {1000}ms.", LogLevel.Error, Times.Exactly(3));
            _logger.VerifyLoggingMessageBeginsWith($"Error copying file to {job.JobPayloadsStoragePath}; destination may be out of disk space.  Exceeded maximum retries.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "Add - Throws upon payload copy failure")]
        public async Task Add_ThrowsWhenFailToCopy()
        {
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(p => p.Directory).Returns(_fileSystem.Directory);
            fileSystem.Setup(p => p.Path).Returns(_fileSystem.Path);
            fileSystem.Setup(p => p.File.Create(It.IsAny<string>()))
                .Returns((string path) => _fileSystem.File.Create(path));
            fileSystem.Setup(p => p.File.Copy(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>())).Throws(new Exception("error"));

            var job = new InferenceJob();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();
            job.SetStoragePath("/path/to/job");
            job.Instances.Add(InstanceGenerator.GenerateInstance("./aet", "aet", fileSystem: fileSystem.Object));
            _configuration.Value.Storage.Temporary = "./aet";

            var cancellationSource = new CancellationTokenSource();
            _inferenceJobRepository.SetupSequence(p => p.AsQueryable())
                .Returns((new List<InferenceJob>() { job }).AsQueryable());

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                fileSystem.Object,
                _inferenceJobRepository.Object);

            await Assert.ThrowsAsync<Exception>(async () => await jobStore.Add(job));

            _logger.VerifyLoggingMessageBeginsWith($"Failed to copy file {job.JobPayloadsStoragePath}.", LogLevel.Error, Times.Once());
        }

        [RetryTheory(DisplayName = "Take - Shall return a job with updated state")]
        [InlineData(InferenceJobState.Queued, InferenceJobState.Creating)]
        [InlineData(InferenceJobState.Created, InferenceJobState.MetadataUploading)]
        [InlineData(InferenceJobState.MetadataUploaded, InferenceJobState.PayloadUploading)]
        [InlineData(InferenceJobState.PayloadUploaded, InferenceJobState.Starting)]
        public async Task Take_ShallReturnAJob(InferenceJobState initalState, InferenceJobState endingState)
        {
            var job = new InferenceJob();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();
            job.SetStoragePath("/path/to/job");
            job.State = initalState;

            var cancellationSource = new CancellationTokenSource();
            _inferenceJobRepository.SetupSequence(p => p.AsQueryable())
                .Returns((new List<InferenceJob>() { job }).AsQueryable());

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            var result = await jobStore.Take(cancellationSource.Token);

            Assert.Equal(job, result);
            Assert.Equal(endingState, job.State);
            _logger.VerifyLoggingMessageBeginsWith($"Updating inference job {job.JobId} from {initalState } to {endingState}.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "Take - Shall throw when cancelled")]
        public async Task Take_ShallThrowWhenCancelled()
        {
            var cancellationSource = new CancellationTokenSource();
            _inferenceJobRepository.Setup(p => p.AsQueryable())
                .Returns((new List<InferenceJob>()).AsQueryable());

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);
            cancellationSource.CancelAfter(100);
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await jobStore.Take(cancellationSource.Token));
        }

        [RetryTheory(DisplayName = "TransitionState Success - Shall continue on to next state")]
        [InlineData(InferenceJobState.Creating, InferenceJobState.Created)]
        [InlineData(InferenceJobState.MetadataUploading, InferenceJobState.MetadataUploaded)]
        [InlineData(InferenceJobState.PayloadUploading, InferenceJobState.PayloadUploaded)]
        [InlineData(InferenceJobState.Starting, InferenceJobState.Completed)]
        public async Task TransitionState_Success_ShallTransitionJob(InferenceJobState initalState, InferenceJobState endingState)
        {
            var job = new InferenceJob();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();
            job.SetStoragePath("/path/to/job");
            job.State = initalState;
            job.TryCount = 3;

            var cancellationSource = new CancellationTokenSource();
            _inferenceJobRepository.SetupSequence(p => p.AsQueryable())
                .Returns((new List<InferenceJob>() { job }).AsQueryable());
            _inferenceJobRepository.Setup(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()));
            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            var result = await jobStore.TransitionState(job, InferenceJobStatus.Success, cancellationSource.Token);

            Assert.Equal(job, result);
            Assert.Equal(0, result.TryCount);
            Assert.Equal(endingState, endingState);
            _logger.VerifyLoggingMessageBeginsWith($"Updating inference job state {job.JobId} from {initalState } to {endingState}.", LogLevel.Information, Times.Once());
            _inferenceJobRepository.Verify(p => p.SaveChangesAsync(cancellationSource.Token), Times.Once());
        }

        [RetryTheory(DisplayName = "TransitionState Fail - Shall roll back to previous state")]
        [InlineData(InferenceJobState.Creating, InferenceJobState.Queued)]
        [InlineData(InferenceJobState.MetadataUploading, InferenceJobState.Created)]
        [InlineData(InferenceJobState.PayloadUploading, InferenceJobState.MetadataUploaded)]
        [InlineData(InferenceJobState.Starting, InferenceJobState.PayloadUploaded)]
        public async Task TransitionState_Fail_ShallTransitionJob(InferenceJobState initalState, InferenceJobState endingState)
        {
            var job = new InferenceJob();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();
            job.SetStoragePath("/path/to/job");
            job.State = initalState;
            job.TryCount = 1;

            var cancellationSource = new CancellationTokenSource();
            _inferenceJobRepository.SetupSequence(p => p.AsQueryable())
                .Returns((new List<InferenceJob>() { job }).AsQueryable());
            _inferenceJobRepository.Setup(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()));
            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            var result = await jobStore.TransitionState(job, InferenceJobStatus.Fail, cancellationSource.Token);

            Assert.Equal(job, result);
            Assert.Equal(endingState, endingState);
            Assert.Equal(2, result.TryCount);
            _logger.VerifyLoggingMessageBeginsWith($"Putting inference job {job.JobId} back to {endingState} state for retry.", LogLevel.Information, Times.Once());
            _inferenceJobRepository.Verify(p => p.SaveChangesAsync(cancellationSource.Token), Times.Once());
        }

        [RetryFact(DisplayName = "TransitionState Fail - Shall put job in faulted state")]
        public async Task TransitionState_Fail_ShallPutJobInFaultedState()
        {
            var job = new InferenceJob();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();
            job.SetStoragePath("/path/to/job");
            job.State = InferenceJobState.Creating;
            job.TryCount = 3;

            var cancellationSource = new CancellationTokenSource();
            _inferenceJobRepository.SetupSequence(p => p.AsQueryable())
                .Returns((new List<InferenceJob>() { job }).AsQueryable());
            _inferenceJobRepository.Setup(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()));
            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            var result = await jobStore.TransitionState(job, InferenceJobStatus.Fail, cancellationSource.Token);

            Assert.Equal(job, result);
            Assert.Equal(InferenceJobState.Faulted, result.State);
            Assert.Equal(4, result.TryCount);
            _logger.VerifyLoggingMessageBeginsWith($"Job {job.JobId} exceeded maximum number of retries.", LogLevel.Warning, Times.Once());
            _inferenceJobRepository.Verify(p => p.SaveChangesAsync(cancellationSource.Token), Times.Once());
        }
    }
}