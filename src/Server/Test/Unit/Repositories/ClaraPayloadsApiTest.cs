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

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.Platform;
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
        public void Upload_ShallThrowWithBadPayloadId()
        {
            var mockClient = new Mock<IPayloadsClient>();
            var mockLogger = new Mock<ILogger<ClaraPayloadsApi>>();

            var service = new ClaraPayloadsApi(mockClient.Object, mockLogger.Object, fileSystem);

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.Upload("bad payload id", "/base", new[] { "/base/path/file" }).Wait();
            });

            mockLogger.VerifyLogging(LogLevel.Error, Times.Never());
        }

        [RetryFact(DisplayName = "Upload shall respect retry policy on failures")]
        public void Upload_ShallRespectRetryPolicyOnFailure()
        {
            var mockClient = new Mock<IPayloadsClient>();
            var mockLogger = new Mock<ILogger<ClaraPayloadsApi>>();

            mockClient.Setup(p => p.UploadTo(It.IsAny<PayloadId>(), It.IsAny<List<(uint mode, string name, Stream stream)>>()))
                .Throws(new PayloadUploadFailedException("error"));

            var service = new ClaraPayloadsApi(mockClient.Object, mockLogger.Object, fileSystem);

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.Upload("good-payload-id", "/", new[] { "/dir1/dir2/file1" }).Wait();
            });

            mockLogger.VerifyLogging(LogLevel.Error, Times.Exactly(3));

            mockClient.Verify(p => p.UploadTo(It.IsAny<PayloadId>(), It.IsAny<List<(uint mode, string name, Stream stream)>>()),
                Times.Exactly(4));
        }

        [Theory(DisplayName = "Upload shall upload files")]
        [InlineData("/dir1/dir2/file1")]
        [InlineData("/dir1/dir2/file2", "/dir1/dir3/file3")]
        public void Upload_ShallUploadFilesAfterRetries(params string[] filenames)
        {
            var mockClient = new Mock<IPayloadsClient>();
            var mockLogger = new Mock<ILogger<ClaraPayloadsApi>>();

            var callCount = 0;
            mockClient.Setup(p => p.UploadTo(It.IsAny<PayloadId>(), It.IsAny<List<(uint mode, string name, Stream stream)>>()))
                .Returns((PayloadId id, List<(uint mode, string name, Stream stream)> list) =>
                {
                    IList<PayloadFileDetails> completedFiles = new List<PayloadFileDetails>();
                    if (callCount++ == 0)
                    {
                        completedFiles.Add(new PayloadFileDetails { Name = list.First().name });
                        throw new PayloadUploadFailedException("error", completedFiles, new List<Exception>());
                    }
                    foreach (var file in list)
                    {
                        completedFiles.Add(new PayloadFileDetails { Name = file.name });
                    }
                    return Task.FromResult(completedFiles);
                });
            var payloadId = "good-payload-id";
            var service = new ClaraPayloadsApi(mockClient.Object, mockLogger.Object, fileSystem);
            service.Upload(payloadId, "/dir1", filenames).Wait();

            if (filenames.Count() > 1)
                mockLogger.VerifyLogging(LogLevel.Error, Times.Once());

            mockLogger.VerifyLogging($"{filenames.Count()} files uploaded to PayloadId {payloadId}", LogLevel.Information, Times.Once());
            mockClient.Verify(p => p.UploadTo(It.IsAny<PayloadId>(), It.IsAny<List<(uint mode, string name, Stream stream)>>()), Times.Exactly(1 + (filenames.Count() > 1 ? 1 : 0)));
        }

        [RetryFact(DisplayName = "Download shall throw on bad payloadId")]
        public void Download_ShallThrowWithBadPayloadId()
        {
            var mockClient = new Mock<IPayloadsClient>();
            var mockLogger = new Mock<ILogger<ClaraPayloadsApi>>();

            var service = new ClaraPayloadsApi(mockClient.Object, mockLogger.Object, fileSystem);

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.Download("bad payload id", "/base/path/file").Wait();
            });

            Assert.IsType<ApplicationException>(exception.InnerException);

            mockLogger.VerifyLogging(LogLevel.Error, Times.Exactly(3));
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

            var file = await service.Download("payload-id", "/base/path/file");

            Assert.Equal(filename, file.Name);
            Assert.Equal(fileSystem.File.ReadAllBytes(filename), file.Data);
        }
    }
}
