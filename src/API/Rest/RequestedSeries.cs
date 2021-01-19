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
    /// Details of a DICOM series to be retrieved for an inference request.
    /// </summary>
    /// <remarks>
    /// <para><c>SeriesInstanceUID></c> is required.</para>
    /// <para>If <c>instances></c> is not specified, the entire series is retrieved.</para>
    /// </remarks>
    public class RequestedSeries
    {
        /// <summary>
        /// Gets or sets the Series Instance UID to be retrieved.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "SeriesInstanceUID")]
        public string SeriesInstanceUid { get; set; }

        /// <summary>
        /// Gets or sets a list of DICOM instances to be retrieved.
        /// </summary>
        [JsonProperty(PropertyName = "instances")]
        public IList<RequestedInstance> Instances { get; set; }
    }
}