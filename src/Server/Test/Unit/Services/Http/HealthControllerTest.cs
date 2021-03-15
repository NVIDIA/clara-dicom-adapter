/*
 * Apache License, Version 2.0
 * Copyright 2019-2021 NVIDIA Corporation
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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Http;
using System;
using System.Net;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class HealthControllerTest
    {
        private HealthController _controller;
        private Mock<IServiceProvider> _serviceProvider;
        private Mock<ProblemDetailsFactory> _problemDetailsFactory;
        private Mock<ILogger<HealthController>> _logger;
        private IOptions<DicomAdapterConfiguration> _configuration;

        public HealthControllerTest()
        {
            _serviceProvider = new Mock<IServiceProvider>();
            _logger = new Mock<ILogger<HealthController>>();
            _configuration = Options.Create(new DicomAdapterConfiguration());

            _problemDetailsFactory = new Mock<ProblemDetailsFactory>();
            _problemDetailsFactory.Setup(_ => _.CreateProblemDetails(
                    It.IsAny<HttpContext>(),
                    It.IsAny<int?>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>(),
                    It.IsAny<string>())
                )
                .Returns((HttpContext httpContext, int? statusCode, string title, string type, string detail, string instance) =>
                {
                    return new ProblemDetails
                    {
                        Status = statusCode,
                        Title = title,
                        Type = type,
                        Detail = detail,
                        Instance = instance
                    };
                });

            _controller = new HealthController(
                 _configuration,
                 _logger.Object,
                 _serviceProvider.Object)
            {
                ProblemDetailsFactory = _problemDetailsFactory.Object
            };
        }

        #region Status

        [RetryFact(DisplayName = "Status -Unknown service status")]
        public void Status_ReturnsUnknownStatus()
        {
            _serviceProvider.Setup(p => p.GetService(It.IsAny<Type>()))
                    .Returns(() =>
                    {
                        return null;
                    });

            var result = _controller.Status();
            var resposne = result.Value as HealthStatusResponse;
            Assert.NotNull(resposne);
            Assert.Equal(0, resposne.ActiveDimseConnections);

            foreach (var service in resposne.Services.Keys)
            {
                Assert.Equal(ServiceStatus.Unknown, resposne.Services[service]);
            }
        }

        [RetryFact(DisplayName = "Status -Shall return actual service status")]
        public void Status_ReturnsActualServiceStatus()
        {
            var claraService = new Mock<IClaraService>();
            claraService.Setup(p => p.Status).Returns(ServiceStatus.Running);
            _serviceProvider.Setup(p => p.GetService(It.IsAny<Type>()))
                    .Returns(claraService.Object);

            var result = _controller.Status();
            var resposne = result.Value as HealthStatusResponse;
            Assert.NotNull(resposne);
            Assert.Equal(0, resposne.ActiveDimseConnections);

            foreach (var service in resposne.Services.Keys)
            {
                Assert.Equal(ServiceStatus.Running, resposne.Services[service]);
            }
        }

        [RetryFact(DisplayName = "Status - Shall return problem on failure")]
        public void Status_ShallReturnProblemOnFailure()
        {
            _serviceProvider.Setup(p => p.GetService(It.IsAny<Type>()))
                    .Throws(new Exception("error"));

            var result = _controller.Status();
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Error collecting system status.", problem.Title);
            Assert.Equal("error", problem.Detail);
            Assert.Equal((int)HttpStatusCode.InternalServerError, problem.Status);
        }

        #endregion Status

        #region Ready

        [RetryFact(DisplayName = "Ready - Shall return Unhealthy")]
        public void Ready_ShallReturnUnhealthy()
        {
            _serviceProvider.Setup(p => p.GetService(It.IsAny<Type>()))
                    .Returns(() =>
                    {
                        return null;
                    });

            var readyResult = _controller.Ready();
            var objectResult = readyResult as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal("Unhealthy", objectResult.Value);
            Assert.Equal((int)HttpStatusCode.ServiceUnavailable, objectResult.StatusCode);
        }

        [RetryFact(DisplayName = "Ready - Shall return Healthy")]
        public void Ready_ShallReturnHealthy()
        {
            var claraService = new Mock<IClaraService>();
            claraService.Setup(p => p.Status).Returns(ServiceStatus.Running);
            _serviceProvider.Setup(p => p.GetService(It.IsAny<Type>()))
                    .Returns(claraService.Object);

            var readyResult = _controller.Ready();
            var objectResult = readyResult as ObjectResult;
            Assert.NotNull(objectResult);
            Assert.Equal("Healthy", objectResult.Value);
            Assert.Equal((int)HttpStatusCode.OK, objectResult.StatusCode);
        }

        [RetryFact(DisplayName = "Ready - Shall return problem on failure")]
        public void Ready_ShallReturnProblemOnFailure()
        {
            _serviceProvider.Setup(p => p.GetService(It.IsAny<Type>()))
                    .Throws(new Exception("error"));

            var result = _controller.Ready();
            var objectResult = result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.NotNull(problem);
            Assert.Equal("Error collecting system status.", problem.Title);
            Assert.Equal("error", problem.Detail);
            Assert.Equal((int)HttpStatusCode.InternalServerError, problem.Status);
        }

        #endregion Ready
    }
}