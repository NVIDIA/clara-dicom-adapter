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
using Moq.Protected;
using Nvidia.Clara.Dicom.DicomWeb.Client;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nvidia.Clara.Dicom.DicomWebClient.Test
{
    public class StowServiceTest : IClassFixture<DicomFileGeneratorFixture>
    {
        private const string BaseUri = "http://dummy/api/";
        private DicomFileGeneratorFixture _fixture;
        private Mock<ILogger> _logger;

        public StowServiceTest(DicomFileGeneratorFixture fixture)
        {
            _fixture = fixture;
            _logger = new Mock<ILogger>();
        }

        [Fact(DisplayName = "Store - throws if input is null or empty")]
        public async Task Store_ShallThrowIfNoFilesSpecified()
        {
            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK
            };

            var httpClient = new HttpClient();
            var service = new StowService(httpClient, _logger.Object);

            await Assert.ThrowsAsync<ArgumentNullException>(async () => await service.Store(null));
            await Assert.ThrowsAsync<ArgumentException>(async () => await service.Store(new List<DicomFile>()));
        }

        [Fact(DisplayName = "Store - throws if no files match study instance UID")]
        public async Task Store_ShallThrowIfNoFilesMatchStudyInstanceUid()
        {
            var studyInstanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instances = _fixture.GenerateDicomFiles(3, studyInstanceUid);

            var httpClient = new HttpClient();
            var service = new StowService(httpClient, _logger.Object);

            var otherStudyInstanceUid = "1.2.3.4.5";
            await Assert.ThrowsAsync<ArgumentException>(async () => await service.Store(otherStudyInstanceUid, instances));
        }

        [Fact(DisplayName = "Store - handles SendAsync failures")]
        public async Task Store_HandlesSendAsyncFailures()
        {
            var studyInstanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instances = _fixture.GenerateDicomFiles(1, studyInstanceUid);

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
                .Protected()
                .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
                .Throws(new Exception("unknown"));
            var httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri(BaseUri)
            };

            var service = new StowService(httpClient, _logger.Object);

            var exception = await Assert.ThrowsAsync<DicomWebClientException>(async () => await service.Store(instances));

            Assert.Null(exception.StatusCode);
        }

        [Theory(DisplayName = "Store - handles responses")]
        [InlineData(HttpStatusCode.OK, "response content")]
        [InlineData(HttpStatusCode.Conflict, "error content")]
        public async Task Store_HandlesResponses(HttpStatusCode status, string message)
        {
            var studyInstanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            var instances = _fixture.GenerateDicomFiles(3, studyInstanceUid);

            var response = new HttpResponseMessage
            {
                StatusCode = status,
                Content = new StringContent(message)
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var service = new StowService(httpClient, _logger.Object);

            var dicomWebResponse = await service.Store(instances);

            Assert.IsType<DicomWebResponse<string>>(dicomWebResponse);
            Assert.Equal(message, dicomWebResponse.Result);
            Assert.Equal(status, dicomWebResponse.StatusCode);

            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Post &&
                req.Content is MultipartContent &&
                req.RequestUri.ToString().Equals($"{BaseUri}studies/")),
               ItExpr.IsAny<CancellationToken>());
        }

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
            httpClient = new HttpClient(handlerMock.Object)
            {
                BaseAddress = new Uri(BaseUri)
            };
        }
    }
}