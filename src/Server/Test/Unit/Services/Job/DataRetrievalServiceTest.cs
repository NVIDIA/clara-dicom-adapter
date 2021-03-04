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
using FellowOakDicom.Serialization;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;
using Newtonsoft.Json;
using Nvidia.Clara.Dicom.DicomWeb.Client;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class DataRetrievalServiceTest
    {
        private readonly Mock<ILoggerFactory> _loggerFactory;
        private readonly Mock<IHttpClientFactory> _httpClientFactory;
        private Mock<ILogger<DicomWebClient>> _loggerDicomWebClient;
        private Mock<ILogger<DataRetrievalService>> _logger;
        private Mock<IInferenceRequestRepository> _inferenceRequestStore;
        private Mock<IDicomToolkit> _dicomToolkit;
        private Mock<IJobRepository> _jobStore;
        private MockFileSystem _fileSystem;
        private Mock<HttpMessageHandler> _handlerMock;
        private readonly Mock<IStorageInfoProvider> _storageInfoProvider;

        public DataRetrievalServiceTest()
        {
            _loggerFactory = new Mock<ILoggerFactory>();
            _httpClientFactory = new Mock<IHttpClientFactory>();
            _logger = new Mock<ILogger<DataRetrievalService>>();
            _inferenceRequestStore = new Mock<IInferenceRequestRepository>();
            _dicomToolkit = new Mock<IDicomToolkit>();
            _jobStore = new Mock<IJobRepository>();
            _fileSystem = new MockFileSystem();
            _loggerDicomWebClient = new Mock<ILogger<DicomWebClient>>();
            _storageInfoProvider = new Mock<IStorageInfoProvider>();

            _loggerFactory.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns((string type) =>
            {
                return _loggerDicomWebClient.Object;
            });
        }

        [RetryFact(DisplayName = "Constructor")]
        public void ConstructorTest()
        {
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(null, null, null, null, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_loggerFactory.Object, null, null, null, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_loggerFactory.Object, _httpClientFactory.Object, _logger.Object, null, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_loggerFactory.Object, _httpClientFactory.Object, _logger.Object, _inferenceRequestStore.Object, null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_loggerFactory.Object, _httpClientFactory.Object, _logger.Object, _inferenceRequestStore.Object, _fileSystem, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_loggerFactory.Object, _httpClientFactory.Object, _logger.Object, _inferenceRequestStore.Object, _fileSystem, _dicomToolkit.Object, null, null));
            Assert.Throws<ArgumentNullException>(() => new DataRetrievalService(_loggerFactory.Object, _httpClientFactory.Object, _logger.Object, _inferenceRequestStore.Object, _fileSystem, _dicomToolkit.Object, _jobStore.Object, null));

            new DataRetrievalService(
                _loggerFactory.Object,
                _httpClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object,
                _storageInfoProvider.Object);
        }

        [RetryFact(DisplayName = "Cancellation token shall stop the service")]
        public async Task CancellationTokenShallCancelTheService()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableToRetrieve).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            var store = new DataRetrievalService(
                _loggerFactory.Object,
                _httpClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object,
                _storageInfoProvider.Object);

            await store.StartAsync(cancellationTokenSource.Token);
            Thread.Sleep(250);
            await store.StopAsync(cancellationTokenSource.Token);
            Thread.Sleep(500);

            _logger.VerifyLogging($"Data Retriever Hosted Service is running.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Data Retriever Hosted Service is stopping.", LogLevel.Information, Times.Once());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableToRetrieve, Times.Never());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        [RetryFact(DisplayName = "Insufficient storage space")]
        public async Task InsufficientStorageSpace()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.CancelAfter(1000);
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableToRetrieve).Returns(false);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            var store = new DataRetrievalService(
                _loggerFactory.Object,
                _httpClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object,
                _storageInfoProvider.Object);

            await store.StartAsync(cancellationTokenSource.Token);
            Thread.Sleep(250);
            await store.StopAsync(cancellationTokenSource.Token);

            _logger.VerifyLogging($"Data Retriever Hosted Service is running.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Data Retriever Hosted Service is stopping.", LogLevel.Information, Times.Once());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableToRetrieve, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.AtLeastOnce());
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
                TransactionId = Guid.NewGuid().ToString(),
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
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableToRetrieve).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            var store = new DataRetrievalService(
                _loggerFactory.Object,
                _httpClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object,
                _storageInfoProvider.Object);

            await store.StartAsync(cancellationTokenSource.Token);

            BlockUntilCancelled(cancellationTokenSource.Token);

            _logger.VerifyLoggingMessageBeginsWith($"Restored previously retrieved instance", LogLevel.Debug, Times.Exactly(3));
            _logger.VerifyLoggingMessageBeginsWith($"Restored previously retrieved instance", LogLevel.Debug, Times.Exactly(3));
            _logger.VerifyLoggingMessageBeginsWith($"Unable to restore previously retrieved instance from", LogLevel.Warning, Times.Once());
            _jobStore.Verify(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()), Times.Once());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableToRetrieve, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        [RetryFact(DisplayName = "ProcessRequest - Shall retrieve via DICOMweb with DICOM UIDs")]
        public async Task ProcessorRequest_ShallRetrieveViaDicomWebWithDicomUid()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var storagePath = "/store";
            _fileSystem.Directory.CreateDirectory(storagePath);

            #region Test Data

            var url = "http://uri.test/";
            var request = new InferenceRequest
            {
                PayloadId = Guid.NewGuid().ToString(),
                JobId = Guid.NewGuid().ToString(),
                TransactionId = Guid.NewGuid().ToString(),
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
                        Uri = url
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

            _handlerMock = new Mock<HttpMessageHandler>();
            _handlerMock
            .Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.IsAny<HttpRequestMessage>(),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    return GenerateMultipartResponse();
                });

            _httpClientFactory.Setup(p => p.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(_handlerMock.Object));
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableToRetrieve).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            var store = new DataRetrievalService(
                _loggerFactory.Object,
                _httpClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object,
                _storageInfoProvider.Object);

            await store.StartAsync(cancellationTokenSource.Token);

            BlockUntilCancelled(cancellationTokenSource.Token);

            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(4),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri.ToString().StartsWith($"{url}studies/")),
               ItExpr.IsAny<CancellationToken>());

            _jobStore.Verify(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()), Times.Once());

            _dicomToolkit.Verify(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()), Times.Exactly(4));
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableToRetrieve, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        [RetryFact(DisplayName = "ProcessRequest - Shall query by PatientId and retrieve")]
        public async Task ProcessorRequest_ShallQueryByPatientIdAndRetrieve()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var storagePath = "/store";
            _fileSystem.Directory.CreateDirectory(storagePath);

            #region Test Data

            var url = "http://uri.test/";
            var request = new InferenceRequest
            {
                PayloadId = Guid.NewGuid().ToString(),
                JobId = Guid.NewGuid().ToString(),
                TransactionId = Guid.NewGuid().ToString(),
            };
            request.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.DicomPatientId,
                    PatientId = "ABC"
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
                        Uri = url
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

            var studyInstanceUids = new List<string>()
            {
                DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                DicomUIDGenerator.GenerateDerivedFromUUID().UID
            };
            _handlerMock = new Mock<HttpMessageHandler>();
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(p => p.RequestUri.Query.Contains("ABC")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    return GenerateQueryResult(DicomTag.PatientID, "ABC", studyInstanceUids);
                });
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(p => !p.RequestUri.Query.Contains("ABC")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    return GenerateMultipartResponse();
                });

            _httpClientFactory.Setup(p => p.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(_handlerMock.Object));
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableToRetrieve).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            var store = new DataRetrievalService(
                _loggerFactory.Object,
                _httpClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object,
                _storageInfoProvider.Object);

            await store.StartAsync(cancellationTokenSource.Token);

            BlockUntilCancelled(cancellationTokenSource.Token);

            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Once(),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri.Query.Contains("00100020=ABC")),
               ItExpr.IsAny<CancellationToken>());

            foreach (var studyInstanceUid in studyInstanceUids)
            {
                _handlerMock.Protected().Verify(
                   "SendAsync",
                   Times.Once(),
                   ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().StartsWith($"{url}studies/{studyInstanceUid}")),
                   ItExpr.IsAny<CancellationToken>());
            }
            _jobStore.Verify(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()), Times.Once());

            _dicomToolkit.Verify(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()), Times.Exactly(studyInstanceUids.Count));
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableToRetrieve, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        [RetryFact(DisplayName = "ProcessRequest - Shall query by AccessionNumber and retrieve")]
        public async Task ProcessorRequest_ShallQueryByAccessionNumberAndRetrieve()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            var storagePath = "/store";
            _fileSystem.Directory.CreateDirectory(storagePath);

            #region Test Data

            var url = "http://uri.test/";
            var request = new InferenceRequest
            {
                PayloadId = Guid.NewGuid().ToString(),
                JobId = Guid.NewGuid().ToString(),
                TransactionId = Guid.NewGuid().ToString(),
            };
            request.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.AccessionNumber,
                    AccessionNumber = new List<string>() { "ABC" }
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
                        Uri = url
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

            var studyInstanceUids = new List<string>()
            {
                DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                DicomUIDGenerator.GenerateDerivedFromUUID().UID
            };
            _handlerMock = new Mock<HttpMessageHandler>();
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(p => p.RequestUri.Query.Contains("ABC")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    return GenerateQueryResult(DicomTag.AccessionNumber, "ABC", studyInstanceUids);
                });
            _handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>(
                    "SendAsync",
                    ItExpr.Is<HttpRequestMessage>(p => !p.RequestUri.Query.Contains("ABC")),
                    ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(() =>
                {
                    return GenerateMultipartResponse();
                });

            _httpClientFactory.Setup(p => p.CreateClient(It.IsAny<string>()))
                .Returns(new HttpClient(_handlerMock.Object));
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableToRetrieve).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            var store = new DataRetrievalService(
                _loggerFactory.Object,
                _httpClientFactory.Object,
                _logger.Object,
                _inferenceRequestStore.Object,
                _fileSystem,
                _dicomToolkit.Object,
                _jobStore.Object,
                _storageInfoProvider.Object);

            await store.StartAsync(cancellationTokenSource.Token);

            BlockUntilCancelled(cancellationTokenSource.Token);

            _handlerMock.Protected().Verify(
               "SendAsync",
               Times.Once(),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri.Query.Contains("00080050=ABC")),
               ItExpr.IsAny<CancellationToken>());

            foreach (var studyInstanceUid in studyInstanceUids)
            {
                _handlerMock.Protected().Verify(
                   "SendAsync",
                   Times.Once(),
                   ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get &&
                    req.RequestUri.ToString().StartsWith($"{url}studies/{studyInstanceUid}")),
                   ItExpr.IsAny<CancellationToken>());
            }
            _jobStore.Verify(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()), Times.Once());

            _dicomToolkit.Verify(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()), Times.Exactly(studyInstanceUids.Count));
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableToRetrieve, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        private HttpResponseMessage GenerateQueryResult(DicomTag dicomTag, string queryValue, List<string> studyInstanceUids)
        {
            var set = new List<DicomDataset>();
            foreach (var studyInstanceUid in studyInstanceUids)
            {
                var dataset = new DicomDataset();
                dataset.Add(dicomTag, queryValue);
                dataset.Add(DicomTag.StudyInstanceUID, studyInstanceUid);
                set.Add(dataset);
            }

            var json = JsonConvert.SerializeObject(set, new JsonDicomConverter());
            var stringContent = new StringContent(json);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK) { Content = stringContent };
        }

        private HttpResponseMessage GenerateMultipartResponse()
        {
            var data = InstanceGenerator.GenerateDicomData();
            var content = new MultipartContent("related");
            content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("type", $"\"application/dicom\""));
            var byteContent = new StreamContent(new MemoryStream(data));
            byteContent.Headers.ContentType = new MediaTypeHeaderValue("application/dicom");
            content.Add(byteContent);
            return new HttpResponseMessage() { Content = content };
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