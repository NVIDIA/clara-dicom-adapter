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

using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Http
{
    [ApiController]
    [Route("api/config/[controller]")]
    public class ClaraAeTitleController : ControllerBase
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ClaraAeTitleController> _logger;
        private readonly IDicomAdapterRepository<ClaraApplicationEntity> _dicomAdapterRepository;
        private readonly IOptions<DicomAdapterConfiguration> _dicomAdapterConfiguration;
        private readonly IClaraAeChangedNotificationService _claraAeChangedNotificationService;
        private ConfigurationValidator _configurationValidator;

        public ClaraAeTitleController(
            IServiceProvider serviceProvider,
            ILogger<ClaraAeTitleController> logger,
            ConfigurationValidator configurationValidator,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration,
            IClaraAeChangedNotificationService claraAeChangedNotificationService,
            IDicomAdapterRepository<ClaraApplicationEntity> dicomAdapterRepository)
        {
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dicomAdapterRepository = dicomAdapterRepository ?? throw new ArgumentNullException(nameof(dicomAdapterRepository));
            _configurationValidator = configurationValidator ?? throw new ArgumentNullException(nameof(configurationValidator));
            _dicomAdapterConfiguration = dicomAdapterConfiguration ?? throw new ArgumentNullException(nameof(dicomAdapterConfiguration));
            _claraAeChangedNotificationService = claraAeChangedNotificationService ?? throw new ArgumentNullException(nameof(claraAeChangedNotificationService));
        }

        [HttpGet]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<IEnumerable<ClaraApplicationEntity>>> Get()
        {
            try
            {
                return await _dicomAdapterRepository.ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Error querying database.");
                return Problem(title: "Error querying database.", statusCode: (int)System.Net.HttpStatusCode.InternalServerError, detail: ex.Message);
            }
        }

        [HttpGet("{aeTitle}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [ActionName(nameof(GetAeTitle))]
        public async Task<ActionResult<ClaraApplicationEntity>> GetAeTitle(string aeTitle)
        {
            try
            {
                var claraApplicationEntity = await _dicomAdapterRepository.FindAsync(aeTitle);

                if (claraApplicationEntity is null)
                {
                    return NotFound();
                }

                return claraApplicationEntity;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Error querying Clara Application Entity.");
                return Problem(title: "Error querying Clara Application Entity.", statusCode: (int)System.Net.HttpStatusCode.InternalServerError, detail: ex.Message);
            }
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<ClaraApplicationEntity>> Create(ClaraApplicationEntity item)
        {
            try
            {
                ValidateProcessor(item);
                item.SetDefaultValues();

                await _dicomAdapterRepository.AddAsync(item);
                await _dicomAdapterRepository.SaveChangesAsync();
                _claraAeChangedNotificationService.Notify(new ClaraApplicationChangedEvent(item, ChangedEventType.Added));
                _logger.Log(LogLevel.Information, $"Clara SCP AE Title added AE Title={item.AeTitle}.");
                return CreatedAtAction(nameof(GetAeTitle), new { aeTitle = item.AeTitle }, item);
            }
            catch (ConfigurationException ex)
            {
                return Problem(title: "Validation error.", statusCode: (int)System.Net.HttpStatusCode.BadRequest, detail: ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Error adding new Clara Application Entity.");
                return Problem(title: "Error adding new Clara Application Entity.", statusCode: (int)System.Net.HttpStatusCode.InternalServerError, detail: ex.Message);
            }
        }

        [HttpDelete("{aeTitle}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<ClaraApplicationEntity>> Delete(string aeTitle)
        {
            try
            {
                var claraApplicationEntity = await _dicomAdapterRepository.FindAsync(aeTitle);
                if (claraApplicationEntity is null)
                {
                    return NotFound();
                }

                _dicomAdapterRepository.Remove(claraApplicationEntity);
                await _dicomAdapterRepository.SaveChangesAsync();

                _claraAeChangedNotificationService.Notify(new ClaraApplicationChangedEvent(claraApplicationEntity, ChangedEventType.Deleted));
                _logger.Log(LogLevel.Information, $"Clara SCP AE Title deleted AE Title={aeTitle}.");
                return claraApplicationEntity;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Error deleting Clara Application Entity.");
                return Problem(title: "Error deleting Clara Application Entity.", statusCode: (int)System.Net.HttpStatusCode.InternalServerError, detail: ex.Message);
            }
        }

        private void ValidateProcessor(ClaraApplicationEntity claraAe)
        {
            Guard.Against.Null(claraAe, nameof(claraAe));

            if (!claraAe.IsValid(_dicomAdapterRepository.AsQueryable().Select(p => p.AeTitle), out IList<string> validationErrors))
            {
                throw new ConfigurationException(string.Join(Environment.NewLine, validationErrors));
            }

            ProcessorValidationAttribute attribute;
            try
            {
                var type = typeof(JobProcessorBase).GetType<JobProcessorBase>(claraAe.Processor);
                attribute = (ProcessorValidationAttribute)Attribute.GetCustomAttributes(type, typeof(ProcessorValidationAttribute)).FirstOrDefault();
            }
            catch (ConfigurationException ex)
            {
                throw new ConfigurationException($"Invalid job processor: {ex.Message}.", ex);
            }

            if (attribute is null)
            {
                throw new ConfigurationException($"Processor type {claraAe.Processor} does not have a `ProcessorValidationAttribute` defined.");
            }

            var validator = attribute.ValidatorType.CreateInstance<IJobProcessorValidator>(_serviceProvider);
            validator.Validate(claraAe.AeTitle, claraAe.ProcessorSettings);
        }
    }
}