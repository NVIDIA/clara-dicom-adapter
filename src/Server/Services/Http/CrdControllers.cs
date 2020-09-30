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
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.K8s;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Http
{
    [ApiController]
    [Route("api/config/[controller]")]
    public class ClaraAeTitleController : CrdCrudControllerBase<ClaraAeTitleController, ClaraApplicationEntity>
    {
        public ClaraAeTitleController(
            IHttpContextAccessor httpContextAccessor,
            ILogger<ClaraAeTitleController> logger,
            IKubernetesWrapper kubernetesClient,
            ConfigurationValidator configurationValidator,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
            : base(httpContextAccessor, logger, kubernetesClient, CustomResourceDefinition.ClaraAeTitleCrd, configurationValidator, dicomAdapterConfiguration)
        {
        }
    }

    [ApiController]
    [Route("api/config/[controller]")]
    public class SourceAeTitleController : CrdCrudControllerBase<SourceAeTitleController, SourceApplicationEntity>
    {
        public SourceAeTitleController(
            IHttpContextAccessor httpContextAccessor,
            ILogger<SourceAeTitleController> logger,
            IKubernetesWrapper kubernetesClient,
            ConfigurationValidator configurationValidator,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
            : base(httpContextAccessor, logger, kubernetesClient, CustomResourceDefinition.SourceAeTitleCrd, configurationValidator, dicomAdapterConfiguration)
        {
        }
    }

    [ApiController]
    [Route("api/config/[controller]")]
    public class DestinationAeTitleController : CrdCrudControllerBase<DestinationAeTitleController, DestinationApplicationEntity>
    {
        public DestinationAeTitleController(
            IHttpContextAccessor httpContextAccessor,
            ILogger<DestinationAeTitleController> logger,
            IKubernetesWrapper kubernetesClient,
            ConfigurationValidator configurationValidator,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
            : base(httpContextAccessor, logger, kubernetesClient, CustomResourceDefinition.DestinationAeTitleCrd, configurationValidator, dicomAdapterConfiguration)
        {
        }
    }
}
