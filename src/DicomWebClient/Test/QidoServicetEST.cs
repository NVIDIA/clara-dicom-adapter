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
using Nvidia.Clara.DicomAdapter.DicomWeb.Client;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nvidia.Clara.Dicom.DicomWebClient.Test
{
    public class QidoServiceTest : IClassFixture<DicomFileGeneratorFixture>
    {
        private DicomFileGeneratorFixture _fixture;

        public QidoServiceTest(DicomFileGeneratorFixture fixture)
        {
            _fixture = fixture;
        }

        #region SearchForStudies

        [Fact(DisplayName = "SearchForStudies - all studies returns JSON string")]
        public async Task SearchForStudies_AllStudies()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateInstancesAsJson(1, studyUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var qido = new QidoService(httpClient, new Uri("http://dummy/api/"));

            var count = 0;
            await foreach (var instance in qido.SearchForStudies())
            {
                count++;
                Assert.IsType<string>(instance);
            }

            Assert.Equal(1, count);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req => req.Method == HttpMethod.Get),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "SearchForStudies - queryParameters - returns JSON string")]
        public async Task SearchForStudies_WithQueryParameters()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateInstancesAsJson(1, studyUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var qido = new QidoService(httpClient, new Uri("http://dummy/api/"));

            var queryParameters = new Dictionary<string, string>();
            queryParameters.Add("11112222", "value");

            var count = 0;
            await foreach (var instance in qido.SearchForStudies(queryParameters))
            {
                count++;
                Assert.IsType<string>(instance);
            }

            Assert.Equal(1, count);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri.Query.Contains("11112222=value")),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "SearchForStudies - queryParameters, fields - returns JSON string")]
        public async Task SearchForStudies_WithQueryParametersAndFields()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateInstancesAsJson(1, studyUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var qido = new QidoService(httpClient, new Uri("http://dummy/api/"));

            var queryParameters = new Dictionary<string, string>();
            queryParameters.Add("11112222", "value");
            var fields = new List<string>();
            fields.Add("1234");

            var count = 0;
            await foreach (var instance in qido.SearchForStudies(queryParameters, fields))
            {
                count++;
                Assert.IsType<string>(instance);
            }

            Assert.Equal(1, count);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri.Query.Contains("includefield=1234") &&
                req.RequestUri.Query.Contains("11112222=value")),
               ItExpr.IsAny<CancellationToken>());
        }

        [Fact(DisplayName = "SearchForStudies - all arguments - returns JSON string")]
        public async Task SearchForStudies_AllArguments()
        {
            var studyUid = DicomUIDGenerator.GenerateDerivedFromUUID();

            var response = new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = _fixture.GenerateInstancesAsJson(1, studyUid),
            };

            Mock<HttpMessageHandler> handlerMock;
            HttpClient httpClient;
            GenerateHttpClient(response, out handlerMock, out httpClient);

            var qido = new QidoService(httpClient, new Uri("http://dummy/api/"));

            var queryParameters = new Dictionary<string, string>();
            queryParameters.Add("11112222", "value");
            var fields = new List<string>();
            fields.Add("1234");

            var count = 0;
            await foreach (var instance in qido.SearchForStudies(queryParameters, fields, true, 1, 1))
            {
                count++;
                Assert.IsType<string>(instance);
            }

            Assert.Equal(1, count);
            handlerMock.Protected().Verify(
               "SendAsync",
               Times.Exactly(1),
               ItExpr.Is<HttpRequestMessage>(req =>
                req.Method == HttpMethod.Get &&
                req.RequestUri.Query.Contains("includefield=1234") &&
                req.RequestUri.Query.Contains("fuzzymatching=true") &&
                req.RequestUri.Query.Contains("limit=1") &&
                req.RequestUri.Query.Contains("offset=1") &&
                req.RequestUri.Query.Contains("11112222=value")),
               ItExpr.IsAny<CancellationToken>());
        }

        #endregion SearchForStudies

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