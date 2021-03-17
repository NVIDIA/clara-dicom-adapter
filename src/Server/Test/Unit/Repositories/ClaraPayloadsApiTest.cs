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
using Moq;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class ClaraPayloadsApiTest
    {
        private IFileSystem fileSystem;

        public ClaraPayloadsApiTest()
        {
            fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { "/dir1/dir2/file1", new MockFileData("Testing is meh.") },
                { "/dir1/dir2/file2", new MockFileData("some js") },
                { "/dir1/dir3/file3", new MockFileData(new byte[] { 0x12, 0x34, 0x56, 0xd2 }) }
            });
        }

        [RetryFact(DisplayName = "Upload shall throw on bad payloadId")]
        public async Task Upload_ShallThrowWithBadPayloadId()
        {
            var mockClient = new Mock<IPayloadsClient>();
            var mockLogger = new Mock<ILogger<ClaraPayloadsApi>>();

            var service = new ClaraPayloadsApi(mockClient.Object, mockLogger.Object, fileSystem);

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await service.Upload("bad payload id", "/base", "/base/path/file");
            });

            mockLogger.VerifyLogging(LogLevel.Error, Times.Never());
        }

        [RetryFact(DisplayName = "Upload shall respect retry policy on failures")]
        public async Task Upload_ShallRespectRetryPolicyOnFailure()
        {
            var mockClient = new Mock<IPayloadsClient>();
            var mockLogger = new Mock<ILogger<ClaraPayloadsApi>>();

            mockClient.Setup(p => p.UploadTo(It.IsAny<PayloadId>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<Stream>()))
                .Throws(new PayloadUploadFailedException("error"));

            var service = new ClaraPayloadsApi(mockClient.Object, mockLogger.Object, fileSystem);

            var exception = await Assert.ThrowsAsync<PayloadUploadFailedException>(async () =>
            {
                await service.Upload(Guid.NewGuid().ToString("N"), "/", "/dir1/dir2/file1");
            });

            mockLogger.VerifyLogging("Error uploading file.", LogLevel.Error, Times.Exactly(4));

            mockClient.Verify(p => p.UploadTo(It.IsAny<PayloadId>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<Stream>()), Times.Exactly(4));
        }

        [Theory(DisplayName = "Upload shall upload files")]
        [InlineData("/dir1/dir2/file1")]
        [InlineData("/dir1/dir2/file2")]
        [InlineData("/dir1/dir3/file3")]
        public async Task Upload_ShallUploadFilesAfterRetries(string filename)
        {
            var mockClient = new Mock<IPayloadsClient>();
            var mockLogger = new Mock<ILogger<ClaraPayloadsApi>>();

            mockClient.Setup(p => p.UploadTo(It.IsAny<PayloadId>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<Stream>()))
                .Returns((PayloadId id, uint mode, string name, Stream stream) =>
                {
                    return Task.FromResult(new PayloadFileDetails { Name = filename });
                });
            var payloadId = Guid.NewGuid().ToString("N");
            var service = new ClaraPayloadsApi(mockClient.Object, mockLogger.Object, fileSystem);
            await service.Upload(payloadId, "/dir1", filename);

            mockLogger.VerifyLogging("File uploaded sucessfully.", LogLevel.Debug, Times.Once());
            mockClient.Verify(p => p.UploadTo(It.IsAny<PayloadId>(), It.IsAny<uint>(), It.IsAny<string>(), It.IsAny<Stream>()), Times.Once());
        }

        [RetryFact(DisplayName = "Download shall throw on bad payloadId")]
        public async Task Download_ShallThrowWithBadPayloadId()
        {
            var mockClient = new Mock<IPayloadsClient>();
            var mockLogger = new Mock<ILogger<ClaraPayloadsApi>>();

            var service = new ClaraPayloadsApi(mockClient.Object, mockLogger.Object, fileSystem);

            var exception = await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await service.Download("bad payload id", "/base/path/file");
            });
        }

        [RetryFact(DisplayName = "Download shall be able to download file")]
        public async void Download_ShallDownloadFile()
        {
            var filename = "/dir1/dir2/file1";
            var mockClient = new Mock<IPayloadsClient>();
            var mockLogger = new Mock<ILogger<ClaraPayloadsApi>>();

            mockClient.Setup(p => p.DownloadFrom(It.IsAny<PayloadId>(), It.IsAny<string>(), It.IsAny<Stream>()))
                .Callback((PayloadId payloadId, string name, Stream stream) =>
                {
                    fileSystem.File.OpenRead(filename).CopyTo(stream);
                })
                .ReturnsAsync(new PayloadFileDetails
                {
                    Mode = 0,
                    Name = filename,
                    Size = 1
                });
            var service = new ClaraPayloadsApi(mockClient.Object, mockLogger.Object, fileSystem);

            var file = await service.Download(Guid.NewGuid().ToString("N"), "/base/path/file");

            Assert.Equal(filename, file.Name);
            Assert.Equal(fileSystem.File.ReadAllBytes(filename), file.Data);
        }
    }
}