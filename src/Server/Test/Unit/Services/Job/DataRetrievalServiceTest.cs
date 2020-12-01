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

using Dicom;
using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.DicomWeb.Client.API;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class DataRetrievalServiceTest
    {
        private Mock<IDicomWebClientFactory> _dicomWebClientFactory;
        private Mock<ILogger<DataRetrievalService>> _logger;
        private Mock<IInferenceRequestStore> _inferenceRequestStore;
        private Mock<IDicomToolkit> _dicomToolkit;
        private Mock<IJobStore> _jobStore;
        private MockFileSystem _fileSystem;

        public DataRetrievalServiceTest()
        {
            _dicomWebClientFactory = new Mock<IDicomWebClientFactory>();
            _logger = new Mock<ILogger<DataRetrievalService>>();
            _inferenceRequestStore = new Mock<IInferenceRequestStore>();
            _dicomToolkit = new Mock<IDicomToolkit>();
            _jobStore = new Mock<IJobStore>();
            _fileSystem = new MockFileSystem();
        }

        [RetryFact(DisplayName = "Constructor")]
        public void ConstructorTest()
        {
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(null, null, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_dicomWebClientFactory.Object, null, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_dicomWebClientFactory.Object, _logger.Object, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_dicomWebClientFactory.Object, _logger.Object, _inferenceRequestStore.Object, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_dicomWebClientFactory.Object, _logger.Object, _inferenceRequestStore.Object, _fileSystem, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_dicomWebClientFactory.Object, _logger.Object, _inferenceRequestStore.Object, _fileSystem, _dicomToolkit.Object, null));

            new DataRetrievalService(
                _dicomWebClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object);
        }

        [RetryFact(DisplayName = "Cancellation token shall stop the service")]
        public void CancellationTokenShallCancelTheService()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            var store = new DataRetrievalService(
                _dicomWebClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object);

            store.StartAsync(cancellationTokenSource.Token);
            store.StopAsync(cancellationTokenSource.Token);

            _logger.VerifyLogging($"Data Retriever Hosted Service is running.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Data Retriever Hosted Service is stopping.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "ProcessRequest - Shall restore previously retrieved DICOM files")]
        public async Task ProcessorRequest_ShallRestorePreviouslyRetrievedFiles()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var storagePath = "/store";
            _fileSystem.Directory.CreateDirectory(storagePath);
            _fileSystem.File.Create(_fileSystem.Path.Combine(storagePath, "file1.dcm"));
            _fileSystem.File.Create(_fileSystem.Path.Combine(storagePath, "file2.dcm"));
            _fileSystem.File.Create(_fileSystem.Path.Combine(storagePath, "file3.dcm"));
            _fileSystem.File.Create(_fileSystem.Path.Combine(storagePath, "corrupted.dcm"));
            _fileSystem.File.Create(_fileSystem.Path.Combine(storagePath, "text.txt"));
            var request = new InferenceRequest
            {
                PayloadId = Guid.NewGuid().ToString(),
                JobId = Guid.NewGuid().ToString(),
                TransactionId = Guid.NewGuid().ToString()
            };
            request.InputResources.Add(
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.Algorithm,
                    ConnectionDetails = new InputConnectionDetails()
                });
            request.ConfigureTemporaryStorageLocation(storagePath);

            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>()))
                .Returns((string filename) =>
                {
                    if (filename.EndsWith("text.txt"))
                    {
                        return false;
                    }
                    else if (filename.EndsWith("corrupted.dcm"))
                    {
                        return false;
                    }
                    return true;
                });

            _dicomToolkit.Setup(p => p.Open(It.IsAny<string>()))
                .Returns(() => InstanceGenerator.GenerateDicomFile());

            _inferenceRequestStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    cancellationTokenSource.Cancel();
                    throw new OperationCanceledException("canceled");
                });

            _jobStore.Setup(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()));

            var store = new DataRetrievalService(
                _dicomWebClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object);

            await store.StartAsync(cancellationTokenSource.Token);

            BlockUntilCancelled(cancellationTokenSource.Token);

            _logger.VerifyLoggingMessageBeginsWith($"Restored previously retrieved instance", LogLevel.Debug, Times.Exactly(3));
            _logger.VerifyLoggingMessageBeginsWith($"Restored previously retrieved instance", LogLevel.Debug, Times.Exactly(3));
            _logger.VerifyLoggingMessageBeginsWith($"Unable to restore previously retrieved instance from", LogLevel.Warning, Times.Once());
            _jobStore.Verify(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()), Times.Once());
        }

        [RetryFact(DisplayName = "ProcessRequest - Shall retrieve via DICOMweb with DICOM UIDs")]
        public async Task ProcessorRequest_ShallRetrieveViaDicomWebWithDicomUid()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var storagePath = "/store";
            _fileSystem.Directory.CreateDirectory(storagePath);

            #region Test Data

            var request = new InferenceRequest
            {
                PayloadId = Guid.NewGuid().ToString(),
                JobId = Guid.NewGuid().ToString(),
                TransactionId = Guid.NewGuid().ToString()
            };
            request.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.DicomUid,
                    Studies = new List<RequestedStudy>
                    {
                         new RequestedStudy
                         {
                              StudyInstanceUid = "1",
                              Series = new List<RequestedSeries>
                              {
                                  new RequestedSeries
                                  {
                                       SeriesInstanceUid = "1.1",
                                       Instances = new List<RequestedInstance>
                                       {
                                           new RequestedInstance
                                           {
                                                SopInstanceUid = new List<string>
                                                {
                                                    "1.1.2",
                                                    "1.1.3"
                                                }
                                           }
                                       }
                                  }
                              }
                         },
                         new RequestedStudy
                         {
                              StudyInstanceUid = "2",
                              Series = new List<RequestedSeries>
                              {
                                  new RequestedSeries
                                  {
                                       SeriesInstanceUid = "2.1"
                                  }
                              }
                         },
                         new RequestedStudy
                         {
                              StudyInstanceUid = "3"
                         },
                    }
                }
            };
            request.InputResources.Add(
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.Algorithm,
                    ConnectionDetails = new InputConnectionDetails()
                });
            request.InputResources.Add(
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.DicomWeb,
                    ConnectionDetails = new InputConnectionDetails
                    {
                        AuthId = "token",
                        AuthType = ConnectionAuthType.Basic,
                        Uri = "http://uri.test/"
                    }
                });

            #endregion Test Data

            request.ConfigureTemporaryStorageLocation(storagePath);

            _dicomToolkit.Setup(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()));

            _inferenceRequestStore.SetupSequence(p => p.Take(It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(request))
                .Returns(() =>
                {
                    cancellationTokenSource.Cancel();
                    throw new OperationCanceledException("canceled");
                });

            _jobStore.Setup(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()));
            var dicomWebClient = new Mock<IDicomWebClient>();
            _dicomWebClientFactory.Setup(p => p.CreateDicomWebClient(
                    It.IsAny<Uri>(),
                    It.IsAny<AuthenticationHeaderValue>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>()))
                .Returns(dicomWebClient.Object);

            dicomWebClient.Setup(p => p.Wado.Retrieve(It.IsAny<string>(), It.IsAny<DicomTransferSyntax[]>()))
                .Returns((string studyInstanceUid, DicomTransferSyntax[] dicomTransferSyntaxes) => GenerateInstance(studyInstanceUid));
            dicomWebClient.Setup(p => p.Wado.Retrieve(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DicomTransferSyntax[]>()))
                .Returns((string studyInstanceUid, string seriesInstanceUid, DicomTransferSyntax[] dicomTransferSyntaxes) => GenerateInstance(studyInstanceUid, seriesInstanceUid));
            dicomWebClient.Setup(p => p.Wado.Retrieve(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DicomTransferSyntax[]>()))
                .Returns((string studyInstanceUid, string seriesInstanceUid, string sopInstanceUid, DicomTransferSyntax[] dicomTransferSyntaxes) =>
                {
                    return Task.FromResult(InstanceGenerator.GenerateDicomFile(studyInstanceUid, seriesInstanceUid, sopInstanceUid, _fileSystem));
                });

            var store = new DataRetrievalService(
                _dicomWebClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object);

            await store.StartAsync(cancellationTokenSource.Token);

            BlockUntilCancelled(cancellationTokenSource.Token);

            _jobStore.Verify(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()), Times.Once());
            dicomWebClient.Verify(p => p.Wado.Retrieve(It.IsAny<string>(), It.IsAny<DicomTransferSyntax[]>()), Times.Once());
            dicomWebClient.Verify(p => p.Wado.Retrieve(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DicomTransferSyntax[]>()), Times.Once());
            dicomWebClient.Verify(p => p.Wado.Retrieve(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DicomTransferSyntax[]>()), Times.Exactly(2));
            _dicomToolkit.Verify(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()), Times.Exactly(4));
        }

        private async IAsyncEnumerable<DicomFile> GenerateInstance(string studyInstanceUid, string seriesInstanceUid = null)
        {
            yield return InstanceGenerator.GenerateDicomFile(studyInstanceUid, seriesInstanceUid, fileSystem: _fileSystem);

            await Task.CompletedTask;
        }

        private void BlockUntilCancelled(CancellationToken token)
        {
            WaitHandle.WaitAll(new[] { token.WaitHandle });
        }
    }
}