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
    /// <see href="http://www.otpedia.com/entryDetails.cfm?id=137">Application Entity</see>
    /// </summary>
    public class BaseApplicationEntity
    {
        /// <value><c>AeTitle</ac> represents the AE Title (or AET) used to identify itself in a DICOM connection.</value>
        [JsonProperty(PropertyName = "aeTitle")]
        public string AeTitle { get; set; }

        /// <value><c>HostIp</ac> represents the host name or IP address associated with the AET.</value>
        [JsonProperty(PropertyName = "hostIp")]
        public string HostIp { get; set; }
    }
}