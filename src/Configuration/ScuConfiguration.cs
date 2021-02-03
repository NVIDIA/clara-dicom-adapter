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

namespace Nvidia.Clara.DicomAdapter.Configuration
{
    /// <summary>
    /// Represents <c>dicom>scu</c> section of the configuration file.
    /// </summary>
    public class ScuConfiguration
    {
        /// <summary>
        /// Gets or sets the AE Title for SCU service.
        /// </summary>
        [JsonProperty(PropertyName = "aeTitle")]
        public string AeTitle { get; set; } = "ClaraSCU";

        /// <summary>
        /// Gets or sets whether or not to write message to log for each P-Data-TF PDU sent or received.
        /// </summary>
        [JsonProperty(PropertyName = "logDataPDUs")]
        public bool LogDataPdus { get; set; } = false;

        /// <summary>
        /// Gets or sets whether or not to write command and data datasets to the log.
        /// </summary>
        [JsonProperty(PropertyName = "logDimseDatasets")]
        public bool LogDimseDatasets { get; set; } = false;

        /// <summary>
        /// Gets or sets the maximum number of simultaneous DICOM associations for the SCU service.
        /// </summary>
        [JsonProperty(PropertyName = "maximumNumberOfAssociations")]
        public int MaximumNumberOfAssociations { get; set; } = 2;

        /// <summary>
        /// Represents the <c>dicom>scu>export</c> section of the configuration file.
        /// </summary>
        [JsonProperty(PropertyName = "export")]
        public DataExportConfiguration ExportSettings { get; set; } = new DataExportConfiguration();

        public ScuConfiguration()
        {
        }
    }
}