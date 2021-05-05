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

using Newtonsoft.Json;

namespace Nvidia.Clara.DicomAdapter.API.Rest
{
    /// <summary>
    /// Represents an input resource (data source).
    /// </summary>
    /// <example>
    /// <code>
    /// {
    ///     ...
    ///     "inputResources" : [
    ///         {
    ///             "interface": "Algorithm",
    ///             "connectionDetails" : {
    ///                 "name": "ai-lung-tumor",
    ///                 "id": "123456790"
    ///             }
    ///         },
    ///         {
    ///             "interface": "DICOMweb",
    ///             "connectionDetails" : {
    ///                 "operations": [ "QUERY", "RETRIEVE" ],
    ///                 "uri": "http://host:port/dicomweb/",
    ///                 "authID": "dXNlcm5hbWU6cGFzc3dvcmQ=",
    ///                 "authType": "Basic"
    ///             }
    ///         },
    ///         {
    ///             "interface": "FHIR",
    ///             "connectionDetails" : {
    ///                 "uri": "http://host:port/fhir/",
    ///                 "authID": "ea134.12adf3adf.341",
    ///                 "authType": "bearer"
    ///             }
    ///         }
    ///     ]
    ///     ...
    /// }
    /// </code>
    /// </example>
    public class RequestInputDataResource
    {
        /// <summary>
        /// Gets or sets the type of interface or a data source.
        /// </summary>
        [JsonProperty(PropertyName = "interface")]
        public InputInterfaceType Interface { get; set; }

        /// <summary>
        /// Gets or sets connection details of a data source.
        /// </summary>
        [JsonProperty(PropertyName = "connectionDetails")]
        public InputConnectionDetails ConnectionDetails { get; set; }
    }
}