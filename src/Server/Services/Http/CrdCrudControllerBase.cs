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

using System;
using System.Diagnostics;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.K8s;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Http
{
    public class CrdCrudControllerBase<S, T> : ControllerBase
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<S> _logger;
        private readonly IKubernetesWrapper _kubernetesClient;
        private readonly CustomResourceDefinition _customResourceDefinition;
        private readonly IOptions<DicomAdapterConfiguration> _dicomAdapterConfiguration;
        private ConfigurationValidator _configurationValidator;

        public CrdCrudControllerBase(
            IHttpContextAccessor httpContextAccessor,
            ILogger<S> logger,
            IKubernetesWrapper kubernetesClient,
            CustomResourceDefinition customResourceDefinition,
            ConfigurationValidator configurationValidator,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
        {
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
            _logger = logger;
            _kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
            _customResourceDefinition = customResourceDefinition ?? throw new ArgumentNullException(nameof(customResourceDefinition));
            _configurationValidator = configurationValidator ?? throw new ArgumentNullException(nameof(configurationValidator));
            _dicomAdapterConfiguration = dicomAdapterConfiguration ?? throw new ArgumentNullException(nameof(dicomAdapterConfiguration));
        }

        [HttpGet]
        public async Task<ActionResult<string>> Get()
        {
            try
            {
                EnsureCrdIsEnabled();

                var result = await _kubernetesClient.ListNamespacedCustomObjectWithHttpMessagesAsync(_customResourceDefinition);
                result.Response.EnsureSuccessStatusCode();

                var json = await result.Response.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }
            catch (CrdNotEnabledException ex)
            {
                _logger.LogWarning($"Trying to list AE Titles while CRD is disabled: {ex}");
                return CreateProblemDetails(title: ex.Message, statusCode: (int)System.Net.HttpStatusCode.ServiceUnavailable, detail: ex.ToString());
            }
            catch (HttpOperationException ex)
            {
                _logger.LogWarning($"{_customResourceDefinition.ApiVersion}/{_customResourceDefinition.PluralName} not found: {ex}");
                return CreateProblemDetails(title: ex.Response.Content, statusCode: (int)ex.Response.StatusCode, detail: ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create CRD for type {typeof(T)} {ex}");
                return CreateProblemDetails(title: ex.Message, statusCode: (int)System.Net.HttpStatusCode.BadRequest, detail: ex.ToString());
            }
        }

        [HttpPost]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Create(T item)
        {
            try
            {
                EnsureCrdIsEnabled();

                var crd = CreateCrdFromType(item);

                var result = await _kubernetesClient.CreateNamespacedCustomObjectWithHttpMessagesAsync(_customResourceDefinition, crd);
                result.Response.EnsureSuccessStatusCode();

                var json = await result.Response.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }
            catch (HttpOperationException ex)
            {
                _logger.LogWarning($"{_customResourceDefinition.ApiVersion}/{_customResourceDefinition.PluralName} not found: {ex}");
                return CreateProblemDetails(title: ex.Response.Content, statusCode: (int)ex.Response.StatusCode, detail: ex.Message);
            }
            catch (CrdNotEnabledException ex)
            {
                _logger.LogWarning($"Trying to create AE Title while CRD is disabled: {ex}");
                return CreateProblemDetails(title: "Reading AE Titles from Kubernetes CRD is not enabled.", statusCode: (int)System.Net.HttpStatusCode.ServiceUnavailable, detail: ex.Message);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to create CRD for type {typeof(T)} {ex}");
                return CreateProblemDetails(title: ex.Message, statusCode: (int)System.Net.HttpStatusCode.BadRequest, detail: ex.ToString());
            }
        }

        [HttpDelete("{name}")]
        [Produces("application/json")]
        public async Task<ActionResult<string>> Delete(string name)
        {
            try
            {
                EnsureCrdIsEnabled();

                var result = await _kubernetesClient.DeleteNamespacedCustomObjectWithHttpMessagesAsync(_customResourceDefinition, name);
                result.Response.EnsureSuccessStatusCode();

                var json = await result.Response.Content.ReadAsStringAsync();
                return Content(json, "application/json");
            }
            catch (HttpOperationException ex)
            {
                _logger.LogWarning($"{_customResourceDefinition.ApiVersion}/{_customResourceDefinition.PluralName} not found: {ex}", ex);
                return CreateProblemDetails(title: ex.Response.Content, statusCode: (int)ex.Response.StatusCode, detail: ex.Message);
            }
            catch (CrdNotEnabledException ex)
            {
                _logger.LogWarning($"Trying to delete AE Title while CRD is disabled: {ex}");
                return CreateProblemDetails(title: "Reading AE Titles from Kubernetes CRD is not enabled.", statusCode: (int)System.Net.HttpStatusCode.ServiceUnavailable, detail: ex.Message);
            }
            catch (Exception ex)
            {
                return CreateProblemDetails(title: ex.Message, statusCode: (int)System.Net.HttpStatusCode.BadRequest, detail: ex.ToString());
            }
        }

        private void EnsureCrdIsEnabled()
        {
            if (!_dicomAdapterConfiguration.Value.ReadAeTitlesFromCrd)
            {
                throw new CrdNotEnabledException("Reading AE Titles from Kubernetes CRD is not enabled.  Please enabled it in appsettings.json `DicomAdapter>readAeTitlesFromCrd`");
            }
        }

        private object CreateCrdFromType(T item)
        {
            if (item is ClaraApplicationEntity claraAe)
            {

                if (!_configurationValidator.IsClaraAeTitleValid(_dicomAdapterConfiguration.Value.Dicom.Scp.AeTitles, "dicom>scp>ae-title", claraAe, true))
                    throw new Exception("Invalid Clara (local) AE Title specs provided or AE Title already exits");

                claraAe.SetDefaultValues();

                return new ClaraApplicationEntityCustomResource
                {
                    Kind = _customResourceDefinition.Kind,
                    ApiVersion = _customResourceDefinition.ApiVersion,
                    Metadata = new k8s.Models.V1ObjectMeta
                    {
                        Name = claraAe.Name.ToLowerInvariant()
                    },
                    Spec = claraAe,
                    Status = AeTitleStatus.Default
                };
            }
            else if (item is SourceApplicationEntity sourceAe)
            {
                if (!_dicomAdapterConfiguration.Value.ReadAeTitlesFromCrd)
                    throw new CrdNotEnabledException("dicom>scp>read-sources-from-crd is disabled");

                if (!_configurationValidator.IsSourceValid(sourceAe))
                    throw new Exception("Invalid source AE Title specs provided");

                return new SourceApplicationEntityCustomResource
                {
                    Kind = _customResourceDefinition.Kind,
                    ApiVersion = _customResourceDefinition.ApiVersion,
                    Metadata = new k8s.Models.V1ObjectMeta
                    {
                        Name = sourceAe.AeTitle.ToLowerInvariant()
                    },
                    Spec = sourceAe,
                    Status = AeTitleStatus.Default
                };
            }
            else if (item is DestinationApplicationEntity destAe)
            {
                if (!_dicomAdapterConfiguration.Value.ReadAeTitlesFromCrd)
                    throw new CrdNotEnabledException("dicom>scu>read-destinations-from-crd is disabled");

                if (!_configurationValidator.IsDestinationValid(destAe))
                    throw new Exception("Invalid destination specs provided");

                return new DestinationApplicationEntityCustomResource
                {
                    Kind = _customResourceDefinition.Kind,
                    ApiVersion = _customResourceDefinition.ApiVersion,
                    Metadata = new k8s.Models.V1ObjectMeta
                    {
                        Name = destAe.Name.ToLowerInvariant()
                    },
                    Spec = destAe,
                    Status = AeTitleStatus.Default
                };
            }
            throw new ApplicationException($"Unsupported data type: {item.GetType()}");
        }

        private ObjectResult CreateProblemDetails(
            int? statusCode = null,
            string title = null,
            string type = null,
            string detail = null,
            string instance = null)
        {
            statusCode ??= (int)HttpStatusCode.InternalServerError;
            var problemDetails = new ProblemDetails
            {
                Status = statusCode.Value,
                Title = title,
                Type = type,
                Detail = detail,
                Instance = instance,
            };

            var traceId = Activity.Current?.Id ?? _httpContextAccessor.HttpContext?.TraceIdentifier;
            if (traceId != null)
            {
                problemDetails.Extensions["traceId"] = traceId;
            }

            return new ObjectResult(problemDetails)
            {
                ContentTypes = { "application/problem+json" },
                StatusCode = statusCode,
            };
        }
    }
}
