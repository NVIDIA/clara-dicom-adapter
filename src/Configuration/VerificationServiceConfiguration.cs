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
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Nvidia.Clara.DicomAdapter.Configuration
{
    /// <summary>
    /// Represents <c>dicom>scp>verification</c> section of the configuration file.
    /// </summary>
    public class VerificationServiceConfiguration
    {
        /// <summary>
        /// Gets or sets whether to enable the verification service.
        /// </summary>
        [JsonProperty(PropertyName = "enabled")]
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Gets or sets a list of transfer syntaxes accepted by the verification service.
        /// </summary>
        [JsonProperty(PropertyName = "transferSyntaxes")]
        public IList<string> TransferSyntaxes { get; internal set; }

        public VerificationServiceConfiguration()
        {
            SetDefaultValues();
        }

        [JsonConstructor]
        public VerificationServiceConfiguration(IList<string> transferSyntaxes)
        {
            TransferSyntaxes = transferSyntaxes;
        }

        [OnSerializing]
        internal void OnSerializingMethod(StreamingContext context)
        {
            SetDefaultValues();
        }

        [OnDeserialized]
        internal void OnDeserializedMethod(StreamingContext context)
        {
            SetDefaultValues();
        }

        public void SetDefaultValues()
        {
            TransferSyntaxes = new List<string> {
                "1.2.840.10008.1.2.1", //Explicit VR Little Endian
                "1.2.840.10008.1.2" , //Implicit VR Little Endian
                "1.2.840.10008.1.2.2", //Explicit VR Big Endian
            };
        }
    }
}