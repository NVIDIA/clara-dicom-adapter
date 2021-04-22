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
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Http;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class ClaraAeTitleControllerTest
    {
        private ClaraAeTitleController _controller;
        private Mock<IServiceProvider> _serviceProvider;
        private Mock<ProblemDetailsFactory> _problemDetailsFactory;
        private Mock<ILogger<ClaraAeTitleController>> _logger;
        private Mock<ILogger<ConfigurationValidator>> _validationLogger;
        private Mock<IClaraAeChangedNotificationService> _aeChangedNotificationService;
        private IOptions<DicomAdapterConfiguration> _configuration;
        private ConfigurationValidator _configurationValidator;
        private Mock<IDicomAdapterRepository<ClaraApplicationEntity>> _repository;

        public ClaraAeTitleControllerTest()
        {
            _serviceProvider = new Mock<IServiceProvider>();
            _logger = new Mock<ILogger<ClaraAeTitleController>>();
            _validationLogger = new Mock<ILogger<ConfigurationValidator>>();
            _aeChangedNotificationService = new Mock<IClaraAeChangedNotificationService>();
            _configurationValidator = new ConfigurationValidator(_validationLogger.Object);
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

            _repository = new Mock<IDicomAdapterRepository<ClaraApplicationEntity>>();

            _controller = new ClaraAeTitleController(
                 _serviceProvider.Object,
                 _logger.Object,
                 _configurationValidator,
                 _configuration,
                 _aeChangedNotificationService.Object,
                 _repository.Object)
            {
                ProblemDetailsFactory = _problemDetailsFactory.Object
            };
        }

        #region Get

        [RetryFact(DisplayName = "Get - Shall return available Clara AETs")]
        public async void Get_ShallReturnAllClaraAets()
        {
            var data = new List<ClaraApplicationEntity>();
            for (int i = 1; i <= 5; i++)
            {
                data.Add(new ClaraApplicationEntity()
                {
                    AeTitle = $"AET{i}",
                    Name = $"AET{i}",
                    Processor = typeof(MockJobProcessor).AssemblyQualifiedName,
                    IgnoredSopClasses = new List<string>() { $"{i}" },
                    ProcessorSettings = new Dictionary<string, string>(),
                    OverwriteSameInstance = (i % 2 == 0)
                });
            }

            _repository.Setup(p => p.ToListAsync()).Returns(Task.FromResult(data));

            var result = await _controller.Get();
            Assert.Equal(data.Count, result.Value.Count());
            _repository.Verify(p => p.ToListAsync(), Times.Once());
        }

        [RetryFact(DisplayName = "Get - Shall return problem on failure")]
        public async void Get_ShallReturnProblemOnFailure()
        {
            _repository.Setup(p => p.ToListAsync()).Throws(new Exception("error"));

            var result = await _controller.Get();
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Error querying database.", problem.Title);
            Assert.Equal("error", problem.Detail);
            Assert.Equal((int)HttpStatusCode.InternalServerError, problem.Status);
        }

        #endregion Get

        #region GetAeTitle

        [RetryFact(DisplayName = "GetAeTitle - Shall return matching object")]
        public async void GetAeTitle_ReturnsAMatch()
        {
            var value = "AET";
            _repository.Setup(p => p.FindAsync(It.IsAny<string>())).Returns(
                Task.FromResult(
                new ClaraApplicationEntity
                {
                    AeTitle = value,
                    Name = value
                }));

            var result = await _controller.GetAeTitle(value);
            Assert.NotNull(result.Value);
            Assert.Equal(value, result.Value.AeTitle);
            _repository.Verify(p => p.FindAsync(value), Times.Once());
        }

        [RetryFact(DisplayName = "GetAeTitle - Shall return 404 if not found")]
        public async void GetAeTitle_Returns404IfNotFound()
        {
            var value = "AET";
            _repository.Setup(p => p.FindAsync(It.IsAny<string>())).Returns(Task.FromResult(default(ClaraApplicationEntity)));

            var result = await _controller.GetAeTitle(value);

            Assert.IsType<NotFoundResult>(result.Result);
            _repository.Verify(p => p.FindAsync(value), Times.Once());
        }

        [RetryFact(DisplayName = "GetAeTitle - Shall return problem on failure")]
        public async void GetAeTitle_ShallReturnProblemOnFailure()
        {
            var value = "AET";
            _repository.Setup(p => p.FindAsync(It.IsAny<string>())).Throws(new Exception("error"));

            var result = await _controller.GetAeTitle(value);

            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Error querying Clara Application Entity.", problem.Title);
            Assert.Equal("error", problem.Detail);
            Assert.Equal((int)HttpStatusCode.InternalServerError, problem.Status);
            _repository.Verify(p => p.FindAsync(value), Times.Once());
        }

        #endregion GetAeTitle

        #region Create

        [Theory(DisplayName = "Create - Shall return BadRequest when validation fails")]
        [InlineData("AeTitleIsTooooooLooooong", "'AeTitleIsTooooooLooooong' is not a valid AE Title (source: ClaraApplicationEntity).")]
        [InlineData("AET1", "Clara AE Title AET1 already exists.")]
        public async void Create_ShallReturnBadRequestOnValidationFailure(string aeTitle, string errorMessage)
        {
            var data = new List<ClaraApplicationEntity>();
            for (int i = 1; i <= 3; i++)
            {
                data.Add(new ClaraApplicationEntity()
                {
                    AeTitle = $"AET{i}",
                    Name = $"AET{i}",
                    Processor = typeof(MockJobProcessor).AssemblyQualifiedName,
                    IgnoredSopClasses = new List<string>() { $"{i}" },
                    ProcessorSettings = new Dictionary<string, string>(),
                    OverwriteSameInstance = (i % 2 == 0)
                });
            }
            _repository.Setup(p => p.AsQueryable()).Returns(data.AsQueryable());

            var claraAeTitle = new ClaraApplicationEntity
            {
                Name = aeTitle,
                Processor = typeof(MockJobProcessor).AssemblyQualifiedName,
                AeTitle = aeTitle,
            };

            var result = await _controller.Create(claraAeTitle);

            Assert.NotNull(result);
            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Validation error.", problem.Title);
            Assert.Equal(errorMessage, problem.Detail);
            Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        }

        [RetryFact(DisplayName = "Create - Shall return problem if job processor is not a subclass of JobProcessorBase")]
        public async void Create_ShallReturnBadRequestWithBadJobProcessType()
        {
            var aeTitle = "AET";
            var claraAeTitle = new ClaraApplicationEntity
            {
                Name = aeTitle,
                Processor = typeof(MockBadJobProcessorNoBase).AssemblyQualifiedName,
                AeTitle = aeTitle,
            };

            var result = await _controller.Create(claraAeTitle);

            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Validation error.", problem.Title);
            Assert.Equal($"Invalid job processor: {claraAeTitle.Processor} is not a sub-type of JobProcessorBase.", problem.Detail);
            Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        }

        [RetryFact(DisplayName = "Create - Shall return problem if job processor is not decorated with ProcessorValidationAttribute")]
        public async void Create_ShallReturnBadRequestWithNoProcessorValidationAttribute()
        {
            var aeTitle = "AET";
            var claraAeTitle = new ClaraApplicationEntity
            {
                Name = aeTitle,
                Processor = typeof(MockBadJobProcessorNoValidation).AssemblyQualifiedName,
                AeTitle = aeTitle,
            };

            var result = await _controller.Create(claraAeTitle);

            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Validation error.", problem.Title);
            Assert.Equal($"Processor type {claraAeTitle.Processor} does not have a `ProcessorValidationAttribute` defined.", problem.Detail);
            Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        }

        [RetryFact(DisplayName = "Create - Shall return problem if job processor validation failed")]
        public async void Create_ShallReturnBadRequestWithJobProcesssorValidationFailure()
        {
            var aeTitle = "AET";
            var claraAeTitle = new ClaraApplicationEntity
            {
                Name = aeTitle,
                Processor = typeof(MockBadJobProcessorValidationFailure).AssemblyQualifiedName,
                AeTitle = aeTitle,
            };

            var result = await _controller.Create(claraAeTitle);

            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Validation error.", problem.Title);
            Assert.Equal($"validation failed", problem.Detail);
            Assert.Equal((int)HttpStatusCode.BadRequest, problem.Status);
        }

        [RetryFact(DisplayName = "Create - Shall return problem if failed to add")]
        public async void Create_ShallReturnBadRequestOnAddFailure()
        {
            var aeTitle = "AET";
            var claraAeTitle = new ClaraApplicationEntity
            {
                Name = aeTitle,
                Processor = typeof(MockJobProcessor).AssemblyQualifiedName,
                AeTitle = aeTitle,
            };

            _repository.Setup(p => p.AddAsync(It.IsAny<ClaraApplicationEntity>(), It.IsAny<CancellationToken>())).Throws(new Exception("error"));

            var result = await _controller.Create(claraAeTitle);

            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Error adding new Clara Application Entity.", problem.Title);
            Assert.Equal($"error", problem.Detail);
            Assert.Equal((int)HttpStatusCode.InternalServerError, problem.Status);

            _repository.Verify(p => p.AddAsync(It.IsAny<ClaraApplicationEntity>(), It.IsAny<CancellationToken>()), Times.Once());
        }

        [RetryFact(DisplayName = "Create - Shall return CreatedAtAction")]
        public async void Create_ShallReturnCreatedAtAction()
        {
            var aeTitle = "AET";
            var claraAeTitle = new ClaraApplicationEntity
            {
                Name = aeTitle,
                Processor = typeof(MockJobProcessor).AssemblyQualifiedName,
                AeTitle = aeTitle,
            };

            _aeChangedNotificationService.Setup(p => p.Notify(It.IsAny<ClaraApplicationChangedEvent>()));
            _repository.Setup(p => p.AddAsync(It.IsAny<ClaraApplicationEntity>(), It.IsAny<CancellationToken>()));
            _repository.Setup(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()));

            var result = await _controller.Create(claraAeTitle);

            Assert.IsType<CreatedAtActionResult>(result.Result);

            _aeChangedNotificationService.Verify(p => p.Notify(It.Is<ClaraApplicationChangedEvent>(x => x.ApplicationEntity == claraAeTitle)), Times.Once());
            _repository.Verify(p => p.AddAsync(It.IsAny<ClaraApplicationEntity>(), It.IsAny<CancellationToken>()), Times.Once());
            _repository.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        #endregion Create

        #region Delete

        [RetryFact(DisplayName = "Delete - Shall return deleted object")]
        public async void Delete_ReturnsDeleted()
        {
            var value = "AET";
            var entity = new ClaraApplicationEntity
            {
                AeTitle = value,
                Name = value
            };
            _repository.Setup(p => p.FindAsync(It.IsAny<string>())).Returns(Task.FromResult(entity));

            _repository.Setup(p => p.Remove(It.IsAny<ClaraApplicationEntity>()));
            _repository.Setup(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()));

            var result = await _controller.Delete(value);
            Assert.NotNull(result.Value);
            Assert.Equal(value, result.Value.AeTitle);
            _repository.Verify(p => p.FindAsync(value), Times.Once());
            _repository.Verify(p => p.Remove(entity), Times.Once());
            _repository.Verify(p => p.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [RetryFact(DisplayName = "Delete - Shall return 404 if not found")]
        public async void Delete_Returns404IfNotFound()
        {
            var value = "AET";
            var entity = new ClaraApplicationEntity
            {
                AeTitle = value,
                Name = value
            };
            _repository.Setup(p => p.FindAsync(It.IsAny<string>())).Returns(Task.FromResult(default(ClaraApplicationEntity)));

            var result = await _controller.Delete(value);

            Assert.IsType<NotFoundResult>(result.Result);
            _repository.Verify(p => p.FindAsync(value), Times.Once());
        }

        [RetryFact(DisplayName = "Delete - Shall return problem on failure")]
        public async void Delete_ShallReturnProblemOnFailure()
        {
            var value = "AET";
            var entity = new ClaraApplicationEntity
            {
                AeTitle = value,
                Name = value
            };
            _repository.Setup(p => p.FindAsync(It.IsAny<string>())).Returns(Task.FromResult(entity));
            _repository.Setup(p => p.Remove(It.IsAny<ClaraApplicationEntity>())).Throws(new Exception("error"));

            var result = await _controller.Delete(value);

            var objectResult = result.Result as ObjectResult;
            Assert.NotNull(objectResult);
            var problem = objectResult.Value as ProblemDetails;
            Assert.NotNull(problem);
            Assert.Equal("Error deleting Clara Application Entity.", problem.Title);
            Assert.Equal("error", problem.Detail);
            Assert.Equal((int)HttpStatusCode.InternalServerError, problem.Status);
            _repository.Verify(p => p.FindAsync(value), Times.Once());
        }

        #endregion Delete
    }

    internal class MockBadJobProcessorNoBase
    { }

    internal class MockBadJobProcessorNoValidation : JobProcessorBase
    {
        public override string Name => "";

        public override string AeTitle => "";

        public MockBadJobProcessorNoValidation(
            ClaraApplicationEntity configuration,
            IInstanceStoredNotificationService instanceStoredNotificationService,
            ILoggerFactory loggerFactory,
            IJobRepository jobStore,
            IInstanceCleanupQueue cleanupQueue,
            CancellationToken cancellationToken) : base(instanceStoredNotificationService, loggerFactory, jobStore, cleanupQueue, cancellationToken)
        {
        }

        public override void HandleInstance(InstanceStorageInfo value)
        {
        }
    }

    [ProcessorValidation(ValidatorType = typeof(MockBadJobProcessorValidator))]
    internal class MockBadJobProcessorValidationFailure : JobProcessorBase
    {
        public override string Name => "";

        public override string AeTitle => "";

        public MockBadJobProcessorValidationFailure(
            ClaraApplicationEntity configuration,
            IInstanceStoredNotificationService instanceStoredNotificationService,
            ILoggerFactory loggerFactory,
            IJobRepository jobStore,
            IInstanceCleanupQueue cleanupQueue,
            CancellationToken cancellationToken) : base(instanceStoredNotificationService, loggerFactory, jobStore, cleanupQueue, cancellationToken)
        {
        }

        public override void HandleInstance(InstanceStorageInfo value)
        {
        }
    }

    internal class MockBadJobProcessorValidator : IJobProcessorValidator
    {
        public void Validate(string aeTitle, Dictionary<string, string> processorSettings)
        {
            throw new ConfigurationException("validation failed");
        }
    }
}