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
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Mime;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Http
{
    [ApiController]
    [Route("api/config/[controller]")]
    public class SourceAeTitleController : ControllerBase
    {
        private readonly ILogger<SourceAeTitleController> _logger;
        private readonly IDicomAdapterRepository<SourceApplicationEntity> _dicomAdapterRepository;

        public SourceAeTitleController(
            ILogger<SourceAeTitleController> logger,
            IDicomAdapterRepository<SourceApplicationEntity> dicomAdapterRepository)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dicomAdapterRepository = dicomAdapterRepository ?? throw new ArgumentNullException(nameof(dicomAdapterRepository));
        }

        [HttpGet]
        [Produces("application/json")]
        public async Task<ActionResult<IEnumerable<SourceApplicationEntity>>> Get()
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
        [ActionName(nameof(GetAeTitle))]
        public async Task<ActionResult<SourceApplicationEntity>> GetAeTitle(string aeTitle)
        {
            try
            {
                var SourceApplicationEntity = await _dicomAdapterRepository.FindAsync(aeTitle);

                if (SourceApplicationEntity is null)
                {
                    return NotFound();
                }

                return SourceApplicationEntity;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Error querying Source Application Entity.");
                return Problem(title: "Error querying Source Application Entity.", statusCode: (int)System.Net.HttpStatusCode.InternalServerError, detail: ex.Message);
            }
        }

        [HttpPost]
        [Consumes(MediaTypeNames.Application.Json)]
        [ProducesResponseType(StatusCodes.Status201Created)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Create(SourceApplicationEntity item)
        {
            try
            {
                if (!item.IsValid(_dicomAdapterRepository.AsQueryable().Select(p => p.AeTitle), out IList<string> validationErrors))
                {
                    throw new ConfigurationException(string.Join(Environment.NewLine, validationErrors));
                }

                await _dicomAdapterRepository.AddAsync(item);
                await _dicomAdapterRepository.SaveChangesAsync();
                _logger.Log(LogLevel.Information, $"DICOM source added AE Title={item.AeTitle}, Host/IP={item.HostIp}.");
                return CreatedAtAction(nameof(GetAeTitle), new { aeTitle = item.AeTitle }, item);
            }
            catch (ConfigurationException ex)
            {
                return Problem(title: "Validation error.", statusCode: (int)System.Net.HttpStatusCode.BadRequest, detail: ex.Message);
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Error adding new Source Application Entity.");
                return Problem(title: "Error adding new Source Application Entity.", statusCode: (int)System.Net.HttpStatusCode.InternalServerError, detail: ex.Message);
            }
        }

        [HttpDelete("{aeTitle}")]
        [Produces("application/json")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public async Task<ActionResult<SourceApplicationEntity>> Delete(string aeTitle)
        {
            try
            {
                var SourceApplicationEntity = await _dicomAdapterRepository.FindAsync(aeTitle);
                if (SourceApplicationEntity is null)
                {
                    return NotFound();
                }

                _dicomAdapterRepository.Remove(SourceApplicationEntity);
                await _dicomAdapterRepository.SaveChangesAsync();

                _logger.Log(LogLevel.Information, $"DICOM source deleted AE Title={aeTitle}.");
                return SourceApplicationEntity;
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Error deleting Source Application Entity.");
                return Problem(title: "Error deleting Source Application Entity.", statusCode: (int)System.Net.HttpStatusCode.InternalServerError, detail: ex.Message);
            }
        }
    }
}