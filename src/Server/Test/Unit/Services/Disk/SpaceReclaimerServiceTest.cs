/*
 * Apache License, Version 2.0
 * Copyright 2019-2020 NVIDIA Corporation
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

using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Threading;
using System.Threading.Tasks;
using Dicom;
using Dicom.Network;
using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class SpaceReclaimerServiceTest
    {
        private Mock<ILogger<SpaceReclaimerService>> _logger;
        private Mock<IInstanceCleanupQueue> _queue;
        private CancellationTokenSource _cancellationTokenSource;
        private IFileSystem _fileSystem;
        private SpaceReclaimerService _service;

        public SpaceReclaimerServiceTest()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _logger = new Mock<ILogger<SpaceReclaimerService>>();
            _queue = new Mock<IInstanceCleanupQueue>();
            _fileSystem = new MockFileSystem();
            _service = new SpaceReclaimerService(_queue.Object, _logger.Object, _fileSystem);
        }

        [RetryFact(DisplayName = "Shall honor cancellation request")]
        public async Task ShallHonorCancellationRequest()
        {
            _queue.Setup(p => p.Dequeue(It.IsAny<CancellationToken>()))
                .Returns(default(InstanceStorageInfo));

            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            await Task.Run(async () =>
            {
                await Task.Delay(150);
                await _service.StopAsync(cancellationTokenSource.Token);
            });
            await _service.StartAsync(cancellationTokenSource.Token);

            _queue.Verify(p => p.Dequeue(It.IsAny<CancellationToken>()), Times.Never());
            _logger.VerifyLogging("Cancellation requested.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "Shall delete files")]
        public async Task ShallDeleteFiles()
        {
            var files = new List<InstanceStorageInfo>() {
                InstanceStorageInfo.CreateInstanceStorageInfo(GenerateRequest(), "/test", "AET", _fileSystem),
                InstanceStorageInfo.CreateInstanceStorageInfo(GenerateRequest(), "/test", "AET", _fileSystem),
                InstanceStorageInfo.CreateInstanceStorageInfo(GenerateRequest(), "/test", "AET", _fileSystem)
            };
            foreach (var file in files)
            {
                _fileSystem.File.Create(file.InstanceStorageFullPath);
            }

            var cancellationTokenSource = new CancellationTokenSource();
            var stack = new Stack<InstanceStorageInfo>(files);
            _queue.Setup(p => p.Dequeue(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (stack.TryPop(out InstanceStorageInfo result))
                        return result;

                    cancellationTokenSource.Cancel();
                    return null;
                });

            await _service.StartAsync(cancellationTokenSource.Token);
            while(!cancellationTokenSource.IsCancellationRequested)
                Thread.Sleep(100);

            _queue.Verify(p => p.Dequeue(It.IsAny<CancellationToken>()), Times.AtLeast(3));

            foreach (var file in files)
            {
                Assert.False(_fileSystem.File.Exists(file.InstanceStorageFullPath));
            }
            _logger.VerifyLogging("Cancellation requested.", LogLevel.Information, Times.Once());
        }

        private DicomCStoreRequest GenerateRequest()
        {
            var dataset = new DicomDataset();
            dataset.Add(DicomTag.PatientID, "PID");
            dataset.Add(DicomTag.StudyInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.Add(DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.Add(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.Add(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage.UID);
            var file = new DicomFile(dataset);
            return new DicomCStoreRequest(file);
        }
    }
}
