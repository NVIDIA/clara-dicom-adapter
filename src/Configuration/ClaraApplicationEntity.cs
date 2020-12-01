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
using Nvidia.Clara.DicomAdapter.Common;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace Nvidia.Clara.DicomAdapter.Configuration
{
    /// <summary>
    /// Clara Application Entity
    /// Clara's SCP AE Title which is used to map to a user-defined pipeline.
    /// </summary>
    /// <example>
    /// <code>
    /// {
    ///     "name": "brain-tummor",
    ///     "aeTitle": "BrainTumorModel",
    ///     "overwriteSameInstance": true,
    ///     "ignoredSopClasses": [
    ///         "1.2.840.10008.5.1.4.1.1.7"
    ///     ],
    ///     "processor": "Nvidia.Clara.DicomAdapter.Server.Processors.AeTitleJobProcessor, Nvidia.Clara.DicomAdapter",
    ///     "processorSettings": {
    ///         "timeout": 5,
    ///         "priority": "higher",
    ///         "pipeline-brain-tumor": "7b9cda79ed834fdc87cd4169216c4011",
    ///         "otherSettings": 12345
    ///     }
    /// }
    /// </code>
    /// </example>
    public class ClaraApplicationEntity
    {
        public static readonly string DefaultClaraJobProcessor = "Nvidia.Clara.DicomAdapter.Server.Processors.AeTitleJobProcessor, Nvidia.Clara.DicomAdapter";

        /// <summary>
        /// Gets or sets the name of a Clara DICOM application entity.
        /// This value must be unique.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the AE TItle.
        /// </summary>
        [JsonProperty(PropertyName = "aeTitle")]
        public string AeTitle { get; set; } = "ClaraSCP";

        /// <summary>
        /// Gets or sets whether or not to overwrite existing instance with the same SOP Instance UID.
        /// </summary>
        [JsonProperty(PropertyName = "overwriteSameInstance")]
        public bool OverwriteSameInstance { get; set; } = false;

        /// <summary>
        /// Tells the SCP to not store DICOM instances with matching SOP Class UIDs.
        /// </summary>
        [JsonProperty(PropertyName = "ignoredSopClasses")]
        public IList<string> IgnoredSopClasses { get; internal set; }

        /// <summary>
        /// Gets or sets the processor to use for this AE Title.
        /// Default: <see cref="Nvidia.Clara.DicomAdapter.Configuration.ClaraApplicationEntity.DefaultClaraJobProcessor" />
        /// </summary>
        [JsonProperty(PropertyName = "processor")]
        public string Processor { get; internal set; } = DefaultClaraJobProcessor;

        /// <summary>
        /// Gets or set additional settings for the configured processor.
        /// All settings are passed to the processor as is for handling.
        /// </summary>
        /// <value></value>
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