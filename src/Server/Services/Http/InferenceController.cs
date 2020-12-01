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

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Http
{
    [ApiController]
    [Route("api/[controller]")]
    public class InferenceController : ControllerBase
    {
        private readonly IKubernetesWrapper _kubernetesClient;
        private readonly ILogger<InferenceController> _logger;
        private readonly IJobs _jobsApi;

        public InferenceController(
            IKubernetesWrapper kubernetesClient,
            ILogger<InferenceController> logger,
            IJobs jobsApi)
        {
            _kubernetesClient = kubernetesClient ?? throw new System.ArgumentNullException(nameof(kubernetesClient));
            _logger = logger ?? throw new System.ArgumentNullException(nameof(logger));
            _jobsApi = jobsApi ?? throw new System.ArgumentNullException(nameof(jobsApi));
        }

        [HttpPost]
        public async Task<ActionResult> NewInferenceRequest([FromBody]InferenceRequest request)
        {
            if (!request.IsValidate(out string details))
            {
                return Problem(title: $"Invalid request", statusCode: (int)HttpStatusCode.UnprocessableEntity, detail: details);
            }

            using (_logger.BeginScope(new Dictionary<string, object> { { "TransactionId", request.TransactionId } }))
            {
                try
                {
                    await CreateJob(request);
                    _logger.Log(LogLevel.Information, $"Job created with Clara: JobId={request.JobId}, PayloadId={request.PayloadId}");
                }
                catch (Exception ex)
                {
                    return Problem(title: "Failed to create job", statusCode: (int)HttpStatusCode.InternalServerError, detail: ex.Message);
                }

                try
                {
                    await _kubernetesClient.CreateNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition.InferenceRequestsCrd, request);
                }
                catch (Exception ex)
                {
                    return Problem(title: "Failed to save request", statusCode: (int)HttpStatusCode.InternalServerError, detail: ex.Message);
                }
            }

            return Ok(new InferenceRequestResponse
            {
                JobId = request.JobId,
                PayloadId = request.PayloadId,
                TransactionId = request.TransactionId
            });
        }

        private async Task CreateJob(InferenceRequest request)
        {
            var jobname = $"{request.Algorithm.Name}-{DateTime.UtcNow.ToString("dd-HHmmss")}".FixJobName();
            var job = await _jobsApi.Create(request.Algorithm.PipelineId, jobname, request.ClaraJobPriority);
            request.JobId = job.JobId;
            request.PayloadId = job.PayloadId;
        }
    }
}