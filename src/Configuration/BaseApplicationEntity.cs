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
    /// DICOM Application Entity or AE.
    /// </summary>
    /// <remarks>
    /// * [Application Entity](http://www.otpedia.com/entryDetails.cfm?id=137)
    /// </remarks>
    public class BaseApplicationEntity
    {
        /// <summary>
        ///  Gets or sets the AE Title (AET) used to identify itself in a DICOM association.
        /// </summary>
        [JsonProperty(PropertyName = "aeTitle")]
        public string AeTitle { get; set; }

        /// <summary>
        /// Gets or set the host name or IP address of the AE Title.
        /// </summary>
        [JsonProperty(PropertyName = "hostIp")]
        public string HostIp { get; set; }
    }
}