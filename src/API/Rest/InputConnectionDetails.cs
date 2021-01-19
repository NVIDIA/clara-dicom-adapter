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
using Newtonsoft.Json.Converters;
using Nvidia.Clara.Dicom.Common;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;

namespace Nvidia.Clara.DicomAdapter.API.Rest
{
    /// <summary>
    /// Connection details of a data source.
    /// </summary>
    public class InputConnectionDetails
    {
        /// <summary>
        /// Gets or sets the name of the algorithm. Used when <see cref="T:Nvidia.Clara.Dicom.API.InputInterfaceType" />
        /// is <see cref="T:Nvidia.Clara.Dicom.API.InputInterfaceType.Algorithm" />.
        /// <c>Name</c> is also used as the job name.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Clara Deploy Pipeline ID. Used when <see cref="T:Nvidia.Clara.Dicom.API.InputInterfaceType" />
        /// is <see cref="T:Nvidia.Clara.Dicom.API.InputInterfaceType.Algorithm" />.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public string PipelineId { get; set; }

        /// <summary>
        /// Gets or sets a list of permitted operations for the connection.
        /// </summary>
        [JsonProperty(PropertyName = "operations")]
        public IList<InputInterfaceOperations> Operations { get; set; }

        /// <summary>
        /// Gets or sets the resource URI (Uniform Resource Identifier) of the connection.
        /// </summary>
        [JsonProperty(PropertyName = "uri")]
        public string Uri { get; set; }

        /// <summary>
        /// Gets or sets the authentication/authorization token of the connection.
        /// For HTTP basic access authentication, the value must be encoded in based 64 using "{username}:{password}" format.
        /// </summary>
        [JsonProperty(PropertyName = "authID")]
        public string AuthId { get; set; }

        /// <summary>
        /// Gets or sets the type of the authentication token used for the connection.
        /// Defaults to HTTP Basic if not specified.
        /// </summary>
        [JsonProperty(PropertyName = "authType")]
        public ConnectionAuthType AuthType { get; set; } = ConnectionAuthType.Basic;
    }



}