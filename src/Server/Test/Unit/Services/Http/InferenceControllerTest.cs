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
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Http;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class InferenceControllerTest
    {
        private readonly Mock<IInferenceRequestRepository> _inferenceRequestStore;
        private readonly DicomAdapterConfiguration _dicomAdapterConfiguration;
        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        private readonly Mock<ILogger<InferenceController>> _logger;
        private readonly Mock<IJobs> _jobsApi;
        private readonly IFileSystem _fileSystem;
        private readonly InferenceController _controller;
        private Mock<ProblemDetailsFactory> _problemDetailsFactory;

        public InferenceControllerTest()
        {
            _inferenceRequestStore = new Mock<IInferenceRequestRepository>();
            _dicomAdapterConfiguration = new DicomAdapterConfiguration();
            _configuration = Options.Create(_dicomAdapterConfiguration);
            _logger = new Mock<ILogger<InferenceController>>();
            _jobsApi = new Mock<IJobs>();
            _fileSystem = new MockFileSystem();
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
            _controller = new InferenceController(_inferenceRequestStore.Object, _configuration, _logger.Object, _jobsApi.Object, _fileSystem)
            {
                ProblemDetailsFactory = _problemDetailsFactory.Object
            };
        }

        [RetryFact(DisplayName = "NewInferenceRequest - shall return problem if input is invalid")]
        public void NewInferenceRequest_ShallReturnProblemIfInputIsInvalid()
        {
            var input = new InferenceRequest();
            input.TransactionId = Guid.NewGuid().ToString();

            var result = _controller.NewInferenceRequest(input);

            Assert.NotNull(result);
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Invalid request", problem.Title);
            Assert.Equal(422, problem.Status);
        }

        [RetryFact(DisplayName = "NewInferenceRequest - shall return problem if output is invalid")]
        public void NewInferenceRequest_ShallReturnProblemIfOutputIsInvalid()
        {
            var input = new InferenceRequest();
            input.TransactionId = Guid.NewGuid().ToString();
            input.InputResources = new List<RequestInputDataResource>()
            {
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.Algorithm,
                    ConnectionDetails = new InputConnectionDetails()
                },
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.DicomWeb,
                    ConnectionDetails = new InputConnectionDetails()
                }
            };
            input.OutputResources = new List<RequestOutputDataResource>()
            {
                new RequestOutputDataResource
                {
                    Interface = InputInterfaceType.DicomWeb,
                    ConnectionDetails = new InputConnectionDetails
                    {
                         AuthType = ConnectionAuthType.Bearer
                    }
                }
            };

            var result = _controller.NewInferenceRequest(input);

            Assert.NotNull(result);
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Invalid request", problem.Title);
            Assert.Equal(422, problem.Status);
        }

        [RetryFact(DisplayName = "NewInferenceRequest - shall return problem if failed to create job")]
        public void NewInferenceRequest_ShallReturnProblemIfFailedToCreateJob()
        {
            _jobsApi.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>()))
                .Throws(new Exception("error"));

            var input = new InferenceRequest();
            input.TransactionId = Guid.NewGuid().ToString();
            input.InputResources = new List<RequestInputDataResource>()
            {
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.Algorithm,
                    ConnectionDetails = new InputConnectionDetails()
                },
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.DicomWeb,
                    ConnectionDetails = new InputConnectionDetails
                    {
                        Uri = "http://my.svc/api"
                    }
                }
            };
            input.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.DicomUid,
                    Studies = new List<RequestedStudy>
                    {
                        new RequestedStudy
                        {
                            StudyInstanceUid = "1"
                        }
                    }
                }
            };

            var result = _controller.NewInferenceRequest(input);

            Assert.NotNull(result);
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Failed to create job", problem.Title);
            Assert.Equal(500, problem.Status);
        }

        [RetryFact(DisplayName = "NewInferenceRequest - shall return problem if failed to add job")]
        public void NewInferenceRequest_ShallReturnProblemIfFailedToAddJob()
        {
            _jobsApi.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>()))
                .Returns(Task.FromResult(new Job
                {
                    JobId = "JOBID",
                    PayloadId = "PAYLOADID"
                }));
            _inferenceRequestStore.Setup(p => p.Add(It.IsAny<InferenceRequest>()))
                .Throws(new Exception("error"));

            var input = new InferenceRequest();
            input.TransactionId = Guid.NewGuid().ToString();
            input.InputResources = new List<RequestInputDataResource>()
            {
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.Algorithm,
                    ConnectionDetails = new InputConnectionDetails()
                },
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.DicomWeb,
                    ConnectionDetails = new InputConnectionDetails
                    {
                        Uri = "http://my.svc/api"
                    }
                }
            };
            input.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.DicomUid,
                    Studies = new List<RequestedStudy>
                    {
                        new RequestedStudy
                        {
                            StudyInstanceUid = "1"
                        }
                    }
                }
            };

            var result = _controller.NewInferenceRequest(input);

            Assert.NotNull(result);
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Failed to save request", problem.Title);
            Assert.Equal(500, problem.Status);
        }

        [RetryFact(DisplayName = "NewInferenceRequest - shall accept inference request")]
        public void NewInferenceRequest_ShallAcceptInferenceRequest()
        {
            _jobsApi.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>()))
                .Returns(Task.FromResult(new Job
                {
                    JobId = "JOBID",
                    PayloadId = "PAYLOADID"
                }));
            _inferenceRequestStore.Setup(p => p.Add(It.IsAny<InferenceRequest>()));

            var input = new InferenceRequest();
            input.TransactionId = Guid.NewGuid().ToString();
            input.InputResources = new List<RequestInputDataResource>()
            {
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.Algorithm,
                    ConnectionDetails = new InputConnectionDetails()
                },
                new RequestInputDataResource
                {
                    Interface = InputInterfaceType.DicomWeb,
                    ConnectionDetails = new InputConnectionDetails
                    {
                        Uri = "http://my.svc/api"
                    }
                }
            };
            input.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.DicomUid,
                    Studies = new List<RequestedStudy>
                    {
                        new RequestedStudy
                        {
                            StudyInstanceUid = "1"
                        }
                    }
                }
            };

            var result = _controller.NewInferenceRequest(input);

            _inferenceRequestStore.Verify(p => p.Add(input), Times.Once());

            Assert.NotNull(result);
            var objectResult = result.Result as OkObjectResult;
            Assert.NotNull(objectResult);
            var response = objectResult.Value as InferenceRequestResponse;
            Assert.NotNull(response);
            Assert.Equal("JOBID", response.JobId);
            Assert.Equal("PAYLOADID", response.PayloadId);
            Assert.Equal(input.TransactionId, response.TransactionId);
        }

        [RetryFact(DisplayName = "Status - return 404 if not found")]
        public void Status_NotFound()
        {
            _inferenceRequestStore.Setup(p => p.GetStatus(It.IsAny<string>()))
                .Returns(Task.FromResult((InferenceStatusResponse)null));

            var jobId = Guid.NewGuid().ToString();
            var result = _controller.JobStatus(jobId);

            _inferenceRequestStore.Verify(p => p.GetStatus(jobId), Times.Once());

            Assert.NotNull(result);
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Inference request not found.", problem.Title);
            Assert.Equal(404, problem.Status);
        }

        [RetryFact(DisplayName = "Status - return 500 on error")]
        public void Status_ShallReturnProblemException()
        {
            _inferenceRequestStore.Setup(p => p.GetStatus(It.IsAny<string>()))
                .Throws(new Exception("error"));

            var jobId = Guid.NewGuid().ToString();
            var result = _controller.JobStatus(jobId);

            _inferenceRequestStore.Verify(p => p.GetStatus(jobId), Times.Once());

            Assert.NotNull(result);
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Failed to retrieve inference request status.", problem.Title);
            Assert.Equal(500, problem.Status);
        }

        [RetryFact(DisplayName = "Status - returns 200")]
        public void Status_ReturnsStatus()
        {
            _inferenceRequestStore.Setup(p => p.GetStatus(It.IsAny<string>()))
                .Returns(Task.FromResult(
                    new InferenceStatusResponse
                    {
                        TransactionId = "TRANSACTIONID",
                        Platform = new InferenceStatusResponse.PlatformStatus
                        {
                            JobId = "JOBID",
                            PayloadId = "PAYLOADID"
                        }
                    }));

            var jobId = Guid.NewGuid().ToString();
            var result = _controller.JobStatus(jobId);

            _inferenceRequestStore.Verify(p => p.GetStatus(jobId), Times.Once());

            Assert.NotNull(result);
            var objectResult = result.Result as OkObjectResult;
            Assert.NotNull(objectResult);
            var response = objectResult.Value as InferenceStatusResponse;
            Assert.NotNull(response);
            Assert.Equal("JOBID", response.Platform.JobId);
            Assert.Equal("PAYLOADID", response.Platform.PayloadId);
            Assert.Equal("TRANSACTIONID", response.TransactionId);
        }
    }
}