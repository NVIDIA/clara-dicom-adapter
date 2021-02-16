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
using System.IO.Abstractions.TestingHelpers;
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
            var job = new Job();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            _inferenceJobRepository.Setup(p => p.AddAsync(It.IsAny<InferenceJob>(), It.IsAny<CancellationToken>())).Throws(new Exception("error"));

            var instance = InstanceGenerator.GenerateInstance("./aet", "aet", fileSystem: _fileSystem);
            await Assert.ThrowsAsync<Exception>(async () => await jobStore.Add(job, "job-name", new List<InstanceStorageInfo> { instance }));

            _logger.VerifyLoggingMessageBeginsWith($"Error saving inference request.", LogLevel.Error, Times.Exactly(3));
        }

        [RetryFact(DisplayName = "Add - Shall add new job")]
        public async Task Add_ShallAddItem()
        {
            var job = new Job();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            var instance = InstanceGenerator.GenerateInstance("./aet", "aet", fileSystem: _fileSystem);
            await jobStore.Add(job, "job-name", new List<InstanceStorageInfo> { instance });

            _inferenceJobRepository.Verify(p => p.AddAsync(It.IsAny<InferenceJob>(), It.IsAny<CancellationToken>()), Times.Once());
            _inferenceJobRepository.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [RetryFact(DisplayName = "Update (Success) - Shall retry on failure")]
        public async Task UpdateSuccess_ShallRetryOnFailure()
        {
            _inferenceJobRepository.Setup(p => p.Remove(It.IsAny<InferenceJob>())).Throws(new Exception("error"));

            var item = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            await Assert.ThrowsAsync<Exception>(async () => await jobStore.Update(item, InferenceJobStatus.Success));

            _logger.VerifyLoggingMessageBeginsWith($"Failed to delete job.", LogLevel.Error, Times.Exactly(3));
        }

        [RetryFact(DisplayName = "Update (Success) - Shall delete job")]
        public async Task UpdateSuccess_ShallDeleteJob()
        {
            var item = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            await jobStore.Update(item, InferenceJobStatus.Success);

            _inferenceJobRepository.Verify(p => p.Remove(item), Times.Once());
            _inferenceJobRepository.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
            _logger.VerifyLogging($"Job removed from job store.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "Update (Fail) - Shall delete job if exceeds max retry")]
        public async Task UpdateFail_ShallDeleteJobIfExceedsMaxRetry()
        {
            var item = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });
            item.TryCount = 3;

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            await jobStore.Update(item, InferenceJobStatus.Fail);

            _inferenceJobRepository.Verify(p => p.Remove(item), Times.Once());
            _inferenceJobRepository.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
            _logger.VerifyLogging($"Job removed from job store.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "Update (Fail) - Shall update count and put back in queue")]
        public async Task UpdateFail_ShallUpdateCountAndUpdateJob()
        {
            var item = new InferenceJob("/path/to/job",
                                        new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });
            item.TryCount = 2;

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            await jobStore.Update(item, InferenceJobStatus.Fail);

            _inferenceJobRepository.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [RetryFact(DisplayName = "Take - Shall take job")]
        public async Task Take_ShallReturnAJob()
        {
            var item = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() })
            {
                State = InferenceJobState.Queued
            };

            var cancellationSource = new CancellationTokenSource();
            _inferenceJobRepository.SetupSequence(p => p.FirstOrDefault(It.IsAny<Func<InferenceJob, bool>>()))
                .Returns(item);

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);

            var result = await jobStore.Take(cancellationSource.Token);

            Assert.Equal(item, result);
        }

        [RetryFact(DisplayName = "Take - Shall throw when cancelled")]
        public async Task Take_ShallThrowWhenCancelled()
        {
            var cancellationSource = new CancellationTokenSource();
            _inferenceJobRepository.Setup(p => p.FirstOrDefault(It.IsAny<Func<InferenceJob, bool>>()))
                .Returns(default(InferenceJob));

            var jobStore = new ClaraJobRepository(
                _logger.Object,
                _configuration,
                _fileSystem,
                _inferenceJobRepository.Object);
            cancellationSource.CancelAfter(100);
            await Assert.ThrowsAsync<OperationCanceledException>(async () => await jobStore.Take(cancellationSource.Token));
        }
    }
}