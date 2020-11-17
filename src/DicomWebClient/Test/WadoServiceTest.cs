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
using Moq;
using Moq.Protected;
using Nvidia.Clara.Dicom.DicomWeb.Client.Common;
using Nvidia.Clara.DicomAdapter.DicomWeb.Client;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nvidia.Clara.Dicom.DicomWebClient.Test
{
    public class WadoServiceTest : IClassFixture<DicomFileGeneratorFixture>
    {
        private DicomFileGeneratorFixture _fixture;

        public WadoServiceTest(DicomFileGeneratorFixture fixture)
        {
            _fixture = fixture;
        }

        #region Retrieve (studies)

        [Fact(DisplayName = "Retrieve Studies - shall throw on bad uid")]
        public async Task Retrieve_Study_BadStudyUid()
        {
            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var instance in wado.Retrieve(studyInstanceUid: null)) { }
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await foreach (var instance in wado.Retrieve(studyInstanceUid: "bad uid")) { }
            });
        }

        [Theory(DisplayName = "Retrieve Studies - shall support different transfer syntaxes")]
        [InlineData(null)]
        [InlineData("1.2.840.10008.1.2.5")]
        [InlineData("1.2.840.10008.1.2.4.70")]
        [InlineData("1.2.840.10008.1.2.4.91")]
        public async Task Retrieve_Study_WithCustomTransferSyntax(string transferSyntaxUid)
        {
            var transferSyntax = transferSyntaxUid != null ? DicomTransferSyntax.Parse(transferSyntaxUid) : null;
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = await _fixture.GenerateInstances(2, studyUid, transferSynx: transferSyntax),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var count = 0;
            await foreach (var instance in wado.Retrieve(studyUid.UID, transferSyntax))
            {
                count++;
            }

            Assert.Equal(2, count);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().Parameters.Any(
                    p => p.Value.Contains(transferSyntaxUid ?? DicomTransferSyntax.ExplicitVRLittleEndian.UID.UID))),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "Retrieve Studies - shall throw on unsupported transfer syntax")]
        public async Task Retrieve_Study_BadTransferSyntax()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = await _fixture.GenerateInstances(1, studyUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await foreach (var instance in wado.Retrieve(studyUid.UID, DicomTransferSyntax.ImplicitVRLittleEndian)) { }
            });
        }

        #endregion Retrieve (studies)

        #region RetrieveMetadata (studies)

        [Fact(DisplayName = "RetrieveMetadata Studies - shall throw on bad uid")]
        public async Task RetrieveMetadata_Study_BadStudyUid()
        {
            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var instance in wado.RetrieveMetadata<string>(null)) { }
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await foreach (var instance in wado.RetrieveMetadata<string>("bad uid")) { }
            });
        }

        [Fact(DisplayName = "RetrieveMetadata Studies - shall throw on invalid return data type")]
        public async Task RetrieveMetadata_Study_InvalidReturnType()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<UnsupportedReturnTypeException>(async () =>
            {
                await foreach (var instance in wado.RetrieveMetadata<int>(studyUid.UID)) { }
            });
        }

        [Fact(DisplayName = "RetrieveMetadata Studies - returns JSON string")]
        public async Task RetrieveMetadata_Study_Json()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateInstancesAsJson(2, studyUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var count = 0;
            await foreach (var instance in wado.RetrieveMetadata<string>(studyUid.UID))
            {
                count++;
                Assert.IsType<string>(instance);
            }

            Assert.Equal(2, count);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().MediaType.Equals(DicomFileGeneratorFixture.MimeApplicationDicomJson)),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "RetrieveMetadata Studies - returns DicomDataset")]
        public async Task RetrieveMetadata_Study_DicomDataset()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateInstancesAsJson(2, studyUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var count = 0;
            await foreach (var instance in wado.RetrieveMetadata<DicomDataset>(studyUid.UID))
            {
                count++;
                Assert.IsType<DicomDataset>(instance);
            }

            Assert.Equal(2, count);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().MediaType.Equals(DicomFileGeneratorFixture.MimeApplicationDicomJson)),
               ItExpr.IsAny<CancellationToken>());
        }

        #endregion RetrieveMetadata (studies)

        #region Retrieve (series)

        [Fact(DisplayName = "Retrieve Series - shall throw on bad uid")]
        public async Task Retrieve_Series_BadUids()
        {
            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var instance in wado.Retrieve(null, seriesInstanceUid: null)) { }
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await foreach (var instance in wado.Retrieve("bad uid", seriesInstanceUid: null)) { }
            });

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var instance in
                    wado.Retrieve(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: null)) { }
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await foreach (var instance in
                    wado.Retrieve(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: "bad uid")) { }
            });
        }

        [Theory(DisplayName = "Retrieve Series - shall support different transfer syntaxes")]
        [InlineData(null)]
        [InlineData("1.2.840.10008.1.2.5")]
        [InlineData("1.2.840.10008.1.2.4.70")]
        [InlineData("1.2.840.10008.1.2.4.91")]
        public async Task Retrieve_Series_WithCustomTransferSyntax(string transferSyntaxUid)
        {
            var transferSyntax = transferSyntaxUid != null ? DicomTransferSyntax.Parse(transferSyntaxUid) : null;
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = await _fixture.GenerateInstances(2, studyUid, seriesUid, transferSynx: transferSyntax),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var count = 0;
            await foreach (var instance in wado.Retrieve(studyUid.UID, seriesUid.UID, transferSyntax))
            {
                count++;
            }

            Assert.Equal(2, count);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().Parameters.Any(
                    p => p.Value.Contains(transferSyntaxUid ?? DicomTransferSyntax.ExplicitVRLittleEndian.UID.UID))),
               ItExpr.IsAny<CancellationToken>());
        }

        #endregion Retrieve (series)

        #region RetrieveMetadata (series)

        [Fact(DisplayName = "RetrieveMetadata Series - shall throw on bad uid")]
        public async Task RetrieveMetadata_Series_BadUids()
        {
            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var instance in wado.RetrieveMetadata<string>(null, seriesInstanceUid: null)) { }
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await foreach (var instance in wado.RetrieveMetadata<string>("bad uid", seriesInstanceUid: null)) { }
            });

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var instance in wado.RetrieveMetadata<string>(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: null)) { }
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await foreach (var instance in wado.RetrieveMetadata<string>(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: "bad uid")) { }
            });
        }

        [Fact(DisplayName = "RetrieveMetadata Series - shall throw on invalid return data type")]
        public async Task RetrieveMetadata_Series_InvalidReturnType()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<UnsupportedReturnTypeException>(async () =>
            {
                await foreach (var instance in wado.RetrieveMetadata<int>(studyUid.UID, seriesUid.UID)) { }
            });
        }

        [Fact(DisplayName = "RetrieveMetadata Series - returns JSON string")]
        public async Task RetrieveMetadata_Series_Json()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateInstancesAsJson(2, studyUid, seriesUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var count = 0;
            await foreach (var instance in wado.RetrieveMetadata<string>(studyUid.UID, seriesUid.UID))
            {
                count++;
                Assert.IsType<string>(instance);
            }

            Assert.Equal(2, count);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().MediaType.Equals(DicomFileGeneratorFixture.MimeApplicationDicomJson)),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "RetrieveMetadata Series - returns DicomDataset")]
        public async Task RetrieveMetadata_Series_DicomDataset()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateInstancesAsJson(2, studyUid, seriesUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var count = 0;
            await foreach (var instance in wado.RetrieveMetadata<DicomDataset>(studyUid.UID, seriesUid.UID))
            {
                count++;
                Assert.IsType<DicomDataset>(instance);
            }

            Assert.Equal(2, count);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().MediaType.Equals(DicomFileGeneratorFixture.MimeApplicationDicomJson)),
               ItExpr.IsAny<CancellationToken>());
        }

        #endregion RetrieveMetadata (series)

        #region Retrieve (instances)

        [Fact(DisplayName = "Retrieve Instance - shall throw on bad uid")]
        public async Task Retrieve_Instance_BadUids()
        {
            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await wado.Retrieve(null, seriesInstanceUid: null, sopInstanceUid: null);
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await wado.Retrieve("bad uid", seriesInstanceUid: null, sopInstanceUid: null);
            });

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await wado.Retrieve(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: null, sopInstanceUid: null);
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await wado.Retrieve(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: "bad id", sopInstanceUid: null);
            });

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await wado.Retrieve(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: DicomUIDGenerator.GenerateDerivedFromUUID().UID, sopInstanceUid: null);
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await wado.Retrieve(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: DicomUIDGenerator.GenerateDerivedFromUUID().UID, sopInstanceUid: "bad uid");
            });
        }

        [Theory(DisplayName = "Retrieve Instance - shall support different transfer syntaxes")]
        [InlineData(null)]
        [InlineData("1.2.840.10008.1.2.5")]
        [InlineData("1.2.840.10008.1.2.4.70")]
        [InlineData("1.2.840.10008.1.2.4.91")]
        public async Task Retrieve_Instance_WithCustomTransferSyntax(string transferSyntaxUid)
        {
            var transferSyntax = transferSyntaxUid != null ? DicomTransferSyntax.Parse(transferSyntaxUid) : null;
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = await _fixture.GenerateInstances(2, studyUid, seriesUid, instanceUid, transferSynx: transferSyntax),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await wado.Retrieve(studyUid.UID, seriesUid.UID, instanceUid.UID, transferSyntax);

            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().Parameters.Any(
                    p => p.Value.Contains(transferSyntaxUid ?? DicomTransferSyntax.ExplicitVRLittleEndian.UID.UID))),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "Retrieve Instance - shall return null when remote returns wrong HTTP code with null data")]
        public async Task Retrieve_Instance_HandleNullDataWithHttpOk()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = null
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var result = await wado.Retrieve(studyUid.UID, seriesUid.UID, instanceUid.UID);

            Assert.Null(result);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().Parameters.Any(
                    p => p.Value.Contains(DicomTransferSyntax.ExplicitVRLittleEndian.UID.UID))),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "Retrieve Instance - shall return null when remote returns wrong HTTP code with empty data")]
        public async Task Retrieve_Instance_HandleEmptyDataWithHttpOk()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("")
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var result = await wado.Retrieve(studyUid.UID, seriesUid.UID, instanceUid.UID);

            Assert.Null(result);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().Parameters.Any(
                    p => p.Value.Contains(DicomTransferSyntax.ExplicitVRLittleEndian.UID.UID))),
               ItExpr.IsAny<CancellationToken>());
        }

        #endregion Retrieve (instances)

        #region RetrieveMetadata (instances)

        [Fact(DisplayName = "RetrieveMetadata Instance - shall throw on bad uid")]
        public async Task RetrieveMetadata_Instance_BadUids()
        {
            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await wado.RetrieveMetadata<string>(null, seriesInstanceUid: null, sopInstanceUid: null);
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await wado.RetrieveMetadata<string>("bad uid", seriesInstanceUid: null, sopInstanceUid: null);
            });

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await wado.RetrieveMetadata<string>(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: null, sopInstanceUid: null);
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await wado.RetrieveMetadata<string>(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: "bad id", sopInstanceUid: "bad id");
            });

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await wado.RetrieveMetadata<string>(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: DicomUIDGenerator.GenerateDerivedFromUUID().UID, sopInstanceUid: null);
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await wado.RetrieveMetadata<string>(DicomUIDGenerator.GenerateDerivedFromUUID().UID, seriesInstanceUid: DicomUIDGenerator.GenerateDerivedFromUUID().UID, sopInstanceUid: "bad id");
            });
        }

        [Fact(DisplayName = "RetrieveMetadata Instance - shall throw on invalid return data type")]
        public async Task RetrieveMetadata_Instance_InvalidReturnType()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<UnsupportedReturnTypeException>(async () =>
            {
                await wado.RetrieveMetadata<int>(studyUid.UID, seriesUid.UID, instanceUid.UID);
            });
        }

        [Fact(DisplayName = "RetrieveMetadata Instance - returns JSON string")]
        public async Task RetrieveMetadata_Instance_Json()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateInstancesAsJson(2, studyUid, seriesUid, instanceUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var instance = await wado.RetrieveMetadata<string>(studyUid.UID, seriesUid.UID, instanceUid.UID);
            Assert.IsType<string>(instance);

            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().MediaType.Equals(DicomFileGeneratorFixture.MimeApplicationDicomJson)),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "RetrieveMetadata Instance - returns DicomDataset")]
        public async Task RetrieveMetadata_Instance_DicomDataset()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateInstancesAsJson(2, studyUid, seriesUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var instance = await wado.RetrieveMetadata<string>(studyUid.UID, seriesUid.UID, instanceUid.UID);
            Assert.IsType<string>(instance);

            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().MediaType.Equals(DicomFileGeneratorFixture.MimeApplicationDicomJson)),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "RetrieveMetadata Instance - shall return null when remote returns wrong HTTP code with null data")]
        public async Task RetrieveMetadata_Instance_HandleNullDataWithHttpOk()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = null
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var result = await wado.RetrieveMetadata<string>(studyUid.UID, seriesUid.UID, instanceUid.UID);

            Assert.Null(result);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().MediaType.Equals(DicomFileGeneratorFixture.MimeApplicationDicomJson)),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "RetrieveMetadata Instance - shall return null when remote returns wrong HTTP code with empty data")]
        public async Task RetrieveMetadata_Instance_HandleEmptyDataWithHttpOk()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent("")
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            var result = await wado.RetrieveMetadata<string>(studyUid.UID, seriesUid.UID, instanceUid.UID);

            Assert.Null(result);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().MediaType.Equals(DicomFileGeneratorFixture.MimeApplicationDicomJson)),
               ItExpr.IsAny<CancellationToken>());
        }

        #endregion RetrieveMetadata (instances)

        #region Retrieve (frames)

        [Fact(DisplayName = "Retrieve Frame - shall throw")]
        public async Task Retrieve_Frames_ShallTHrow()
        {
            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<NotImplementedException>(async () =>
            {
                await wado.Retrieve(
                    DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                    DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                    DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                    new uint[] { 1, 2, 3 });
            });
        }

        #endregion Retrieve (frames)

        #region Retrieve (bulkdata)

        [Fact(DisplayName = "Retrieve Bulkdata - shall throw on bad uid")]
        public async Task Retrieve_Bulkdata_BadUid()
        {
            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await wado.Retrieve(
                    null,
                    seriesInstanceUid: null,
                    sopInstanceUid: null,
                    DicomTag.PixelData);
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await wado.Retrieve(
                    "bad uid",
                    seriesInstanceUid: null,
                    sopInstanceUid: null,
                    DicomTag.PixelData);
            });

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await wado.Retrieve(
                    DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                    seriesInstanceUid: null,
                    sopInstanceUid: null,
                    DicomTag.PixelData);
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await wado.Retrieve(
                    DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                    seriesInstanceUid: "bad id",
                    sopInstanceUid: null,
                    DicomTag.PixelData);
            });

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await wado.Retrieve(
                    DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                    seriesInstanceUid: DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                    sopInstanceUid: null,
                    DicomTag.PixelData);
            });

            await Assert.ThrowsAsync<DicomValidationException>(async () =>
            {
                await wado.Retrieve(
                    DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                    seriesInstanceUid: DicomUIDGenerator.GenerateDerivedFromUUID().UID,
                    sopInstanceUid: "bad uid",
                    DicomTag.PixelData);
            });
        }

        [Fact(DisplayName = "Retrieve Bulkdata - shall thorw on bad uri")]
        public async Task Retrieve_Bulkdata_BadUri()
        {
            var httpClient = new HttpClient();
            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await wado.Retrieve(
                    bulkdataUri: null);
            });

            await Assert.ThrowsAsync<UriFormatException>(async () =>
            {
                await wado.Retrieve(
                    bulkdataUri: new Uri("www.contoso.com/api/"));
            });
        }

        [Theory(DisplayName = "Retrieve Bulkdata - shall support different transfer syntaxes")]
        [InlineData(null)]
        [InlineData("1.2.840.10008.1.2.5")]
        [InlineData("1.2.840.10008.1.2.4.70")]
        [InlineData("1.2.840.10008.1.2.4.91")]
        public async Task Retrieve_Bulkdata_WithCustomTransferSyntax(string transferSyntaxUid)
        {
            var transferSyntax = transferSyntaxUid != null ? DicomTransferSyntax.Parse(transferSyntaxUid) : null;
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateByteData(),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await wado.Retrieve(studyUid.UID, seriesUid.UID, instanceUid.UID, DicomTag.PixelData, transferSyntaxes: transferSyntax);

            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().Parameters.Any(
                    p => p.Value.Contains(transferSyntaxUid ?? DicomTransferSyntax.ExplicitVRLittleEndian.UID.UID))),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "Retrieve Bulkdata - shall include range in request headers")]
        public async Task Retrieve_Bulkdata_IncludesRangeHeaders()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateByteData(),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var wado = new WadoService(httpClient, new Uri("http://dummy/api/"));

            await wado.Retrieve(
                studyUid.UID,
                seriesUid.UID,
                instanceUid.UID,
                DicomTag.PixelData,
                new Tuple<int, int?>(1, 3));

            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.Headers.Accept.First().Parameters.Any(
                    p => p.Value.Contains(DicomTransferSyntax.ExplicitVRLittleEndian.UID.UID)) &&
                req.Headers.Range.ToString() == "byte=1-3"),
               ItExpr.IsAny<CancellationToken>());
        }

        #endregion Retrieve (bulkdata)

        private static void GenerateHttpClient(HttpResponseMessage response, out Mock<HttpMessageHandler> handlerMock, out HttpClient httpClient)
        {
            handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(response);
            httpClient = new HttpClient(handlerMock.Object);
        }
    }
}