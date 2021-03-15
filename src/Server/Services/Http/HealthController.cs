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
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Http
{
    [ApiController]
    [Route("[controller]")]
    public class HealthController : ControllerBase
    {
        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        private readonly ILogger<HealthController> _logger;
        private readonly IServiceProvider _serviceProvider;

        public HealthController(
            IOptions<DicomAdapterConfiguration> configuration,
            ILogger<HealthController> logger,
            IServiceProvider serviceProvider)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        [HttpGet("status")]
        public ActionResult<HealthStatusResponse> Status()
        {
            try
            {
                var services = GetServiceStatus();
                var response = new HealthStatusResponse
                {
                    ActiveDimseConnections = ScpService.ActiveConnections,
                    Services = services.Select((p) => new { p.Key.Name, p.Value }).ToDictionary(x => x.Name, x => x.Value)
                };

                return response;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, $"Error collecting system status.");
                return Problem(title: "Error collecting system status.", statusCode: (int)HttpStatusCode.InternalServerError, detail: ex.Message);
            }
        }

        [HttpGet("ready")]
        [HttpGet("live")]
        public ActionResult Ready()
        {
            try
            {
                var services = GetServiceStatus();

                if (services.Values.Any((p) => p != ServiceStatus.Running))
                {
                    return StatusCode((int)HttpStatusCode.ServiceUnavailable, "Unhealthy");
                }

                return Ok("Healthy");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, $"Error collecting system status.");
                return Problem(title: "Error collecting system status.", statusCode: (int)HttpStatusCode.InternalServerError, detail: ex.Message);
            }
        }

        private Dictionary<Type, ServiceStatus> GetServiceStatus()
        {
            var services = new Dictionary<Type, ServiceStatus>();
            var serviceTypes = GetServices();
            foreach (var type in serviceTypes)
            {
                services[type] = GetServiceStatus(type);
            }
            return services;
        }

        private static List<Type> GetServices()
        {
            var services = new List<Type>
            {
                typeof(Disk.SpaceReclaimerService),
                typeof(Jobs.JobSubmissionService),
                typeof(Jobs.DataRetrievalService),
                typeof(Scp.ScpService),
                typeof(Scu.ScuExportService),
                typeof(Export.DicomWebExportService)
            };
            return services;
        }

        private ServiceStatus GetServiceStatus(Type type)
        {
            Guard.Against.Null(type, nameof(type));

            var service = _serviceProvider.GetService(type);

            if (service is null)
            {
                return ServiceStatus.Unknown;
            }

            return (service as IClaraService)?.Status ?? ServiceStatus.Unknown;
        }
    }
}