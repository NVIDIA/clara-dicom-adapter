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

using System.Collections.Generic;
using System.Runtime.Serialization;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.Common;

namespace Nvidia.Clara.DicomAdapter.Configuration
{
    /// <summary>
    /// Clara Application Entity
    /// Clara's SCP AE Title which is used to map to a user-defined pipeline.
    /// </summary>

    public class ClaraApplicationEntity
    {
        public static readonly string DefaultClaraJobProcessor = "Nvidia.Clara.DicomAdapter.Server.Processors.AeTitleJobProcessor, Nvidia.Clara.DicomAdapter";

        /// <summary>
        /// Gets or sets the name of this DICOM application entity.
        /// </summary>
        /// <value><c>Name</ac> is use to identify a Clara AE Title.  This value must be unique.</value>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the AE TItle.
        /// </summary>
        /// <value><c>AeTitle</ac> represents the AE Title (or AET) used to identify itself in a DICOM connection.</value>
        [JsonProperty(PropertyName = "aeTitle")]
        public string AeTitle { get; set; } = "ClaraSCP";

        /// <summary>
        /// Gets or sets whether or not to overwrite existing instance with the same SOP Instance UID.
        /// </summary>
        /// <value><c>overwriteSameInstance</ac> determines if the system shall overwrite a received instance with same SOP Instance that is already on disk.</value>
        [JsonProperty(PropertyName = "overwriteSameInstance")]
        public bool OverwriteSameInstance { get; set; } = false;

        /// <summary>
        /// Tells the SCP to not store DICOM instances with matching SOP Class UIDs.
        /// </summary>
        /// <value>IgnoredSopClasses</value> List of SOP Class UIDs to be ignored by the SCP.
        [JsonProperty(PropertyName = "ignoredSopClasses")]
        public IList<string> IgnoredSopClasses { get; internal set; }

        [JsonProperty(PropertyName = "processor")]
        public string Processor { get; internal set; } = DefaultClaraJobProcessor;

        [JsonProperty(PropertyName = "processorSettings")]
        public Dictionary<string, string> ProcessorSettings { get; internal set; }

        public ClaraApplicationEntity()
        {
            SetDefaultValues();
        }

        [JsonConstructor]
        public ClaraApplicationEntity(IList<string> ignoredSopClasses)
        {
            IgnoredSopClasses = ignoredSopClasses;
        }

        public void SetDefaultValues()
        {
            if (ProcessorSettings is null)
                ProcessorSettings = new Dictionary<string, string>();

            if (IgnoredSopClasses.IsNull())
                IgnoredSopClasses = new List<string>();

            if (string.IsNullOrWhiteSpace(Processor))
                Processor = DefaultClaraJobProcessor;
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
    }
}
