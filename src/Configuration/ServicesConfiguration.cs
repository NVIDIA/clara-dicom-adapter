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

using Newtonsoft.Json;

namespace Nvidia.Clara.DicomAdapter.Configuration
{
    /// <summary>
    /// Source (SCP) Application Entity
    /// </summary>
    public class ServicesConfiguration
    {
        /// <summary>
        /// Gets or sets the URI of the Platform API.
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "platform")]
        public PlatformConfiguration Platform { get; set; } = new PlatformConfiguration();

        /// <summary>
        /// Gets or sets the URI of the Results Service API.
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "resultsServiceEndpoint")]
        public string ResultsServiceEndpoint { get; set; }
    }

    public class PlatformConfiguration
    {
        /// <summary>
        /// Gets or sets the URI of the Platform API.
        /// </summary>
        [JsonProperty(PropertyName = "endpoint")]
        public string Endpoint { get; set; }

        /// <summary>
        /// Gets or sets maximum number of concurrent uploads to the Paylodas Service.
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "paralellUploads")]
        public int ParalellUploads { get; set; } = 4;
    }
}