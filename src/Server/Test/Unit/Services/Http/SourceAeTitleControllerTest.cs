﻿/*
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

using k8s.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Moq;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Processors;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;
using xRetry;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class SourceAeTitleControllerTest
    {
        private SourceAeTitleController _controller;
        private Mock<IServiceProvider> _serviceProvider;
        private Mock<ProblemDetailsFactory> _problemDetailsFactory;
        private Mock<ILogger<SourceAeTitleController>> _logger;
        private Mock<ILogger<ConfigurationValidator>> _validationLogger;
        private Mock<IKubernetesWrapper> _kubernetesClient;
        private IOptions<DicomAdapterConfiguration> _configuration;
        private ConfigurationValidator _configurationValidator;

        public SourceAeTitleControllerTest()
        {
            _serviceProvider = new Mock<IServiceProvider>();
            _logger = new Mock<ILogger<SourceAeTitleController>>();
            _validationLogger = new Mock<ILogger<ConfigurationValidator>>();
            _kubernetesClient = new Mock<IKubernetesWrapper>();
            _configurationValidator = new ConfigurationValidator(_validationLogger.Object);
            _configuration = Options.Create(new DicomAdapterConfiguration());
            _controller = new SourceAeTitleController(_serviceProvider.Object, _logger.Object, _kubernetesClient.Object, _configurationValidator, _configuration);
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

            _controller = new SourceAeTitleController(_serviceProvider.Object, _logger.Object, _kubernetesClient.Object, _configurationValidator, _configuration)
            {
                ProblemDetailsFactory = _problemDetailsFactory.Object
            };
        }

        [RetryFact(DisplayName = "Get - Shall return available CRDs")]
        public async void Get_ShallReturnAvailableCrds()
        {
            var claraAeTitles = new SourceApplicationEntityCustomResourceList
            {
                Items = new List<SourceApplicationEntityCustomResource>
                {
                    new SourceApplicationEntityCustomResource
                    {
                        Spec = new SourceApplicationEntity
                        {
                            AeTitle = "AETITLE",
                            HostIp = "localhost"
                        },
                        Status = new AeTitleStatus { Enabled = true },
                        Metadata = new V1ObjectMeta { ResourceVersion = "1", Name = "AETITLE" }
                    }
                }
            };
            _kubernetesClient.Setup(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.SourceAeTitleCrd))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(claraAeTitles)) }
                }));

            var result = await _controller.Get();

            _kubernetesClient.Verify(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.SourceAeTitleCrd), Times.Once());

            var data = JsonConvert.DeserializeObject<SourceApplicationEntityCustomResourceList>((result.Result as ContentResult).Content);
            Assert.Equal(1, data.Items.Count);

            foreach (var item in claraAeTitles.Items)
            {
                var actualItem = data.Items.FirstOrDefault(p => p.Spec.AeTitle.Equals(item.Spec.AeTitle));
                Assert.NotNull(actualItem);
                Assert.Equal(item.Spec.AeTitle, actualItem.Spec.AeTitle);
                Assert.Equal(item.Spec.HostIp, actualItem.Spec.HostIp);
            }
        }

        [RetryFact(DisplayName = "Create - Shall return ServiceUnavailable when read from CRD is disabled")]
        public async void Create_ShallReturnServiceUnavailableWHenCrdIsDisabled()
        {
            var claraAeTitle = new SourceApplicationEntity
            {
                AeTitle = "AET",
                HostIp = "localhost"
            };

            _configuration.Value.ReadAeTitlesFromCrd = false;
            var result = await _controller.Create(claraAeTitle);

            Assert.NotNull(result);
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Reading AE Titles from Kubernetes CRD is not enabled.", problem.Title);
            Assert.Equal(503, problem.Status);
        }

        [Theory(DisplayName = "Create - Shall return BadRequest when validation fails")]
        [InlineData("AeTitleIsTooooooLooooong", "localhost")]
        [InlineData("GoodSCP", "")]
        public async void Create_ShallReturnBadRequestWHenCrdIsDisabled(string aeTitle, string hostIp)
        {
            var claraAeTitle = new SourceApplicationEntity
            {
                AeTitle = aeTitle,
                HostIp = hostIp
            };
            var result = await _controller.Create(claraAeTitle);

            Assert.NotNull(result);
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Invalid source AE Title specs provided", problem.Title);
            Assert.Equal((int)HttpStatusCode.InternalServerError, problem.Status);
        }

        [RetryFact(DisplayName = "Create - Shall have error from K8s propagate back to caller")]
        public async void Create_ShallPropagateErrorBackToCaller()
        {
            var response = new HttpOperationResponse<object>();
            response.Response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Response.Content = new StringContent("Go!Clara!");
            _kubernetesClient
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()))
                .Throws(new HttpOperationException("error message")
                {
                    Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Conflict), "error content")
                });

            var sourceAeTitle = new SourceApplicationEntity
            {
                AeTitle = "MySCP",
                HostIp = "localhost"
            };

            var result = await _controller.Create(sourceAeTitle);

            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Once());

            Assert.NotNull(result);
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("error message", problem.Detail);
            Assert.Equal("error content", problem.Title);
            Assert.Equal((int)HttpStatusCode.Conflict, problem.Status.Value);
        }

        [RetryFact(DisplayName = "Create - Shall return created JSON")]
        public async void Create_ShallReturnCreatedJson()
        {
            var mockLogger = new Mock<ILogger<AeTitleJobProcessorValidator>>();
            _serviceProvider.Setup(p => p.GetService(typeof(ILogger<AeTitleJobProcessorValidator>))).Returns(mockLogger.Object);

            var response = new HttpOperationResponse<object>();
            response.Response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Response.Content = new StringContent("Go!Clara");
            _kubernetesClient
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()))
                .Returns(() =>
                {
                    return Task.FromResult(response);
                });

            var claraAeTitle = new SourceApplicationEntity
            {
                AeTitle = "AET",
                HostIp = "localhost"
            };

            var result = await _controller.Create(claraAeTitle);

            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Once());
            Assert.NotNull(result);
            var contentResult = result.Result as ContentResult;
            Assert.NotNull(contentResult);
            Assert.Equal(response.Response.Content.AsString(), contentResult.Content);
        }

        [RetryFact(DisplayName = "Create - Shall return deleted response")]
        public async void Delete_ShallReturnDeletedResponse()
        {
            var response = new HttpOperationResponse<object>();
            response.Response = new HttpResponseMessage(HttpStatusCode.OK);
            response.Response.Content = new StringContent("Go!Clara");
            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Returns(() =>
                {
                    return Task.FromResult(response);
                });

            var name = "delete-me";
            var result = await _controller.Delete(name);

            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), name), Times.Once());
            Assert.NotNull(result);
            var contentResult = result.Result as ContentResult;
            Assert.NotNull(contentResult);
            Assert.Equal(response.Response.Content.AsString(), contentResult.Content);
        }
    }
}