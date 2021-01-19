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
using Moq.Protected;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class ResultsApiTest
    {
        [RetryFact(DisplayName = "GetPendingJobs shall return null on API call failures")]
        public async Task GetPendingJobs_ShallReturnNullOnCallFailures()
        {
            // ARRANGE
            var config = Options.Create(new DicomAdapterConfiguration());
            config.Value.Services.ResultsServiceEndpoint = "http://test.com/";
            config.Value.Dicom.Scu.AeTitle = "clarascu";

            var mockLogger = new Mock<ILogger<ResultsApi>>();
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHttpMessageHandler
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ThrowsAsync(new HttpRequestException())
               .Verifiable();

            // use real http client with mocked handler here
            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://test.com/"),
            };
            var subjectUnderTest = new ResultsApi(config, httpClient, mockLogger.Object);

            // ACT
            var result = await subjectUnderTest.GetPendingJobs(CancellationToken.None, 10);

            // ASSERT
            Assert.Empty(result);
            // also check the 'http' call was like we expected it
            var expectedUri = new Uri($"{config.Value.Services.ResultsServiceEndpoint}api/tasks/{config.Value.Dicom.Scu.AeTitle}/pending?size=10");

            mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(4), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get  // we expected a GET request
                    && req.RequestUri == expectedUri // to this uri
                ),
                ItExpr.IsAny<CancellationToken>());
        }

        [RetryFact(DisplayName = "GetPendingJobs shall be able to return data correctly")]
        public async Task GetPendingJobs_ShallBeAbleToReturnDataCorrectly()
        {
            // ARRANGE
            var config = Options.Create(new DicomAdapterConfiguration());
            config.Value.Services.ResultsServiceEndpoint = "http://test.com/";
            config.Value.Dicom.Scu.AeTitle = "clarascu";

            var mockLogger = new Mock<ILogger<ResultsApi>>();
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHttpMessageHandler
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
                   Content = ReadContentFrom("GetPendingTest.json"),
               })
               .Verifiable();

            // use real http client with mocked handler here
            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://test.com/"),
            };

            var subjectUnderTest = new ResultsApi(config, httpClient, mockLogger.Object);

            // ACT
            var result = await subjectUnderTest.GetPendingJobs(CancellationToken.None, 10);

            // ASSERT
            Assert.NotNull(result);
            Assert.Equal(3, result.Count);
            // also check the 'http' call was like we expected it
            var expectedUri = new Uri($"{config.Value.Services.ResultsServiceEndpoint}api/tasks/{config.Value.Dicom.Scu.AeTitle}/pending?size=10");

            mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Get  // we expected a GET request
                    && req.RequestUri == expectedUri // to this uri
                ),
                ItExpr.IsAny<CancellationToken>());
        }

        [RetryFact(DisplayName = "ReportFailure shall return false on API call failures")]
        public async Task ReportFailure_ShallReturnFalseOnCallFailures()
        {
            // ARRANGE
            var config = Options.Create(new DicomAdapterConfiguration());
            config.Value.Services.ResultsServiceEndpoint = "http://test.com/";
            config.Value.Dicom.Scu.AeTitle = "clarascu";

            var mockLogger = new Mock<ILogger<ResultsApi>>();
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHttpMessageHandler
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ThrowsAsync(new HttpRequestException())
               .Verifiable();

            // use real http client with mocked handler here
            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://test.com/"),
            };
            var subjectUnderTest = new ResultsApi(config, httpClient, mockLogger.Object);
            var taskId = Guid.NewGuid();

            // ACT
            var result = await subjectUnderTest.ReportFailure(taskId, true, CancellationToken.None);

            // ASSERT
            Assert.False(result);
            // also check the 'http' call was like we expected it
            var expectedUri = new Uri($"{config.Value.Services.ResultsServiceEndpoint}api/tasks/failure/{taskId}");

            mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(4), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put  // we expected a GET request
                    && req.RequestUri == expectedUri // to this uri
                    && req.Content.ReadAsStringAsync().Result == JsonConvert.SerializeObject(new { RetryLater = true })
                ),
                ItExpr.IsAny<CancellationToken>());
        }

        [Theory(DisplayName = "ReportFailure shall be able to return data correctly")]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ReportFailure_ShallBeAbleToReturnDataCorrectly(bool retry)
        {
            // ARRANGE
            var config = Options.Create(new DicomAdapterConfiguration());
            config.Value.Services.ResultsServiceEndpoint = "http://test.com/";
            config.Value.Dicom.Scu.AeTitle = "clarascu";

            var mockLogger = new Mock<ILogger<ResultsApi>>();
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHttpMessageHandler
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
               })
               .Verifiable();

            // use real http client with mocked handler here
            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://test.com/"),
            };

            var subjectUnderTest = new ResultsApi(config, httpClient, mockLogger.Object);
            var taskId = Guid.NewGuid();

            // ACT
            var result = await subjectUnderTest.ReportFailure(taskId, retry, CancellationToken.None);

            // ASSERT
            Assert.True(result);
            // also check the 'http' call was like we expected it
            var expectedUri = new Uri($"{config.Value.Services.ResultsServiceEndpoint}api/tasks/failure/{taskId}");

            mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put  // we expected a GET request
                    && req.RequestUri == expectedUri // to this uri
                    && req.Content.ReadAsStringAsync().Result == JsonConvert.SerializeObject(new { RetryLater = retry })
                ),
                ItExpr.IsAny<CancellationToken>());
        }

        [RetryFact(DisplayName = "ReportSuccess shall return false on API call failures")]
        public async Task ReportSuccess_ShallReturnFalseOnCallFailures()
        {
            // ARRANGE
            var config = Options.Create(new DicomAdapterConfiguration());
            config.Value.Services.ResultsServiceEndpoint = "http://test.com/";
            config.Value.Dicom.Scu.AeTitle = "clarascu";

            var mockLogger = new Mock<ILogger<ResultsApi>>();
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHttpMessageHandler
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ThrowsAsync(new HttpRequestException())
               .Verifiable();

            // use real http client with mocked handler here
            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://test.com/"),
            };
            var subjectUnderTest = new ResultsApi(config, httpClient, mockLogger.Object);
            var taskId = Guid.NewGuid();

            // ACT
            var result = await subjectUnderTest.ReportSuccess(taskId, CancellationToken.None);

            // ASSERT
            Assert.False(result);
            // also check the 'http' call was like we expected it
            var expectedUri = new Uri($"{config.Value.Services.ResultsServiceEndpoint}api/tasks/success/{taskId}");

            mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(4), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put  // we expected a GET request
                    && req.RequestUri == expectedUri // to this uri
                ),
                ItExpr.IsAny<CancellationToken>());
        }

        [RetryFact(DisplayName = "ReportSuccess shall be able to return data correctly")]
        public async Task ReportSuccess_ShallBeAbleToReturnDataCorrectly()
        {
            // ARRANGE
            var config = Options.Create(new DicomAdapterConfiguration());
            config.Value.Services.ResultsServiceEndpoint = "http://test.com/";
            config.Value.Dicom.Scu.AeTitle = "clarascu";

            var mockLogger = new Mock<ILogger<ResultsApi>>();
            var mockHttpMessageHandler = new Mock<HttpMessageHandler>(MockBehavior.Strict);
            mockHttpMessageHandler
               .Protected()
               // Setup the PROTECTED method to mock
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               // prepare the expected response of the mocked http call
               .ReturnsAsync(new HttpResponseMessage()
               {
                   StatusCode = HttpStatusCode.OK,
               })
               .Verifiable();

            // use real http client with mocked handler here
            var httpClient = new HttpClient(mockHttpMessageHandler.Object)
            {
                BaseAddress = new Uri("http://test.com/"),
            };

            var subjectUnderTest = new ResultsApi(config, httpClient, mockLogger.Object);
            var taskId = Guid.NewGuid();

            // ACT
            var result = await subjectUnderTest.ReportSuccess(taskId, CancellationToken.None);

            // ASSERT
            Assert.True(result);
            // also check the 'http' call was like we expected it
            var expectedUri = new Uri($"{config.Value.Services.ResultsServiceEndpoint}api/tasks/success/{taskId}");

            mockHttpMessageHandler.Protected().Verify(
               "SendAsync",
               Times.Exactly(1), // we expected a single external request
               ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Put  // we expected a GET request
                    && req.RequestUri == expectedUri // to this uri
                ),
                ItExpr.IsAny<CancellationToken>());
        }

        private HttpContent ReadContentFrom(string path) => new StringContent(File.ReadAllText(path));
    }
}