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
using System;
using System.Linq;

namespace Nvidia.Clara.DicomAdapter.Configuration
{
    /// <summary>
    /// Main class used when deserializing the application configuration file.
    /// </summary>
    public class DicomAdapterConfiguration
    {
        /// <summary>
        /// Represents the <c>dicom</c> section of the configuration file.
        /// </summary>
        [JsonProperty(PropertyName = "dicom")]
        public DicomConfiguration Dicom { get; set; }

        /// <summary>
        /// Represents the <c>storage</c> section of the configuration file.
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "storage")]
        public StorageConfiguration Storage { get; set; }

        /// <summary>
        /// Represent the <c>services</c> section of the configuration file.
        /// </summary>
        /// <returns></returns>
        [JsonProperty(PropertyName = "service")]
        public ServicesConfiguration Services { get; set; }

        /// <summary>
        /// Gets or sets wether to read AE Titles from K8s CRD
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "readAeTitlesFromCrd")]
        public bool ReadAeTitlesFromCrd { get; set; } = true;

        /// <summary>
        /// Gets or sets number of seconds between reading CRD changes
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "crdReadIntervals")]
        public int CrdReadIntervals { get; set; } = 1000;

        public DicomAdapterConfiguration()
        {
            Dicom = new DicomConfiguration();
            Storage = new StorageConfiguration();
            Services = new ServicesConfiguration();
        }

        public bool ContainsClaraAeTitle(string claraAeTitle)
        {
            return Dicom.Scp.AeTitles.Any(
                p => p.AeTitle.Equals(claraAeTitle, StringComparison.InvariantCultureIgnoreCase));
        }
    }
}