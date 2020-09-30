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
    /// Destination Application Entity
    /// </summary>
    public class DestinationApplicationEntity : BaseApplicationEntity
    {
        /// <summary>
        /// A unique name used to identify a DICOM destination.
        /// </summary>
        /// <value>Name</value> Identifies a DICOM destination.
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <value><c>Port</ac> the port number associated with the AET.</value>
        [JsonProperty(PropertyName = "port")]
        public int Port { get; set; }
    }
}
