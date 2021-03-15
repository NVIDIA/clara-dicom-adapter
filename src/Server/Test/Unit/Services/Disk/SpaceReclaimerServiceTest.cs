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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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
        private IOptions<DicomAdapterConfiguration> _configuration;
        private SpaceReclaimerService _service;

        public SpaceReclaimerServiceTest()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _logger = new Mock<ILogger<SpaceReclaimerService>>();
            _queue = new Mock<IInstanceCleanupQueue>();
            _fileSystem = new MockFileSystem();

            _configuration = Options.Create(new DicomAdapterConfiguration());
            _configuration.Value.Storage.Temporary = "/payloads";
            _service = new SpaceReclaimerService(_queue.Object, _logger.Object, _configuration, _fileSystem);
        }

        [RetryFact(DisplayName = "Shall honor cancellation request")]
        public async Task ShallHonorCancellationRequest()
        {
            _queue.Setup(p => p.Dequeue(It.IsAny<CancellationToken>()))
                .Returns(default(string));

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
            var files = new List<string>() {
                "/payloads/dir1/file1",
                "/payloads/dir1/file2",
                "/payloads/dir2/file1.exe"
            };
            foreach (var file in files)
            {
                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(file));
                _fileSystem.File.Create(file);
            }

            var cancellationTokenSource = new CancellationTokenSource();
            var stack = new Stack<string>(files);
            _queue.Setup(p => p.Dequeue(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (stack.TryPop(out string result))
                        return result;

                    cancellationTokenSource.Cancel();
                    return null;
                });

            await _service.StartAsync(cancellationTokenSource.Token);
            while (!cancellationTokenSource.IsCancellationRequested)
                Thread.Sleep(100);

            _queue.Verify(p => p.Dequeue(It.IsAny<CancellationToken>()), Times.AtLeast(3));

            foreach (var file in files)
            {
                Assert.False(_fileSystem.File.Exists(file));
            }
            Assert.True(_fileSystem.Directory.Exists("/payloads"));

            _logger.VerifyLogging("Cancellation requested.", LogLevel.Information, Times.Once());
        }


        [RetryFact(DisplayName = "Shall delete directories if empty")]
        public async Task ShallDeleteDirectoriesIfEmpty()
        {
            var files = new List<string>() {
                "/payloads/dir1/dir1.1/file1",
                "/payloads/dir1/dir1.2/file2",
                "/payloads/dir1/dir1.2/dir1.2.1/file4",
                "/payloads/dir1/dir1.2/dir1.2.1/file5",
                "/payloads/dir1/dir1.2/dir1.2.2/file6",
                "/payloads/dir1/dir1.2/file3",
                "/payloads/dir1/dir1.3/file7",
                "/payloads/dir2/dir2.1/dir2.1.1/file1.exe"
            };
            foreach (var file in files)
            {
                _fileSystem.Directory.CreateDirectory(_fileSystem.Path.GetDirectoryName(file));
                _fileSystem.File.Create(file);
            }

            var cancellationTokenSource = new CancellationTokenSource();
            var stack = new Stack<string>(files.Where(p => !p.EndsWith("file3")));
            _queue.Setup(p => p.Dequeue(It.IsAny<CancellationToken>()))
                .Returns(() =>
                {
                    if (stack.TryPop(out string result))
                        return result;

                    cancellationTokenSource.Cancel();
                    return null;
                });

            await _service.StartAsync(cancellationTokenSource.Token);
            while (!cancellationTokenSource.IsCancellationRequested)
                Thread.Sleep(100);

            _queue.Verify(p => p.Dequeue(It.IsAny<CancellationToken>()), Times.AtLeast(7));

            Assert.False(_fileSystem.File.Exists("/payloads/dir1/dir1.1/file1"));
            Assert.False(_fileSystem.File.Exists("/payloads/dir1/dir1.2/file2"));
            Assert.False(_fileSystem.File.Exists("/payloads/dir1/dir1.2/dir1.2.1/file4"));
            Assert.False(_fileSystem.File.Exists("/payloads/dir1/dir1.2/dir1.2.1/file5"));
            Assert.False(_fileSystem.File.Exists("/payloads/dir1/dir1.2/dir1.2.2/file6"));
            Assert.False(_fileSystem.File.Exists("/payloads/dir2/dir2.1/dir2.1.1/file1.exe"));
            Assert.False(_fileSystem.File.Exists("/payloads/dir1/dir1.3/file7"));
            Assert.False(_fileSystem.Directory.Exists("/payloads/dir1/dir1.3"));
            Assert.False(_fileSystem.Directory.Exists("/payloads/dir2"));

            Assert.True(_fileSystem.File.Exists("/payloads/dir1/dir1.2/file3"));
            Assert.True(_fileSystem.Directory.Exists("/payloads/dir1/dir1.2"));
            Assert.True(_fileSystem.Directory.Exists("/payloads/dir1"));
            Assert.True(_fileSystem.Directory.Exists("/payloads"));

            _logger.VerifyLogging("Cancellation requested.", LogLevel.Information, Times.Once());
        }
    }
}