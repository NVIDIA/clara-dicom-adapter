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

using System.IO.Abstractions;
using Newtonsoft.Json;

namespace Nvidia.Clara.DicomAdapter.Configuration
{
    public class StorageConfiguration
    {
        private readonly IFileSystem _fileSystem;

        public StorageConfiguration() : this(new FileSystem()) { }
        public StorageConfiguration(IFileSystem fileSystem) 
            => _fileSystem = fileSystem ?? throw new System.ArgumentNullException(nameof(fileSystem));

        /// <summary>
        /// Gets or sets temporary storage path.
        /// This is used to store all instances received to a temporary folder.
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "temporary")]
        public string Temporary { get; set; } = "./payloads";

        /// <summary>
        /// Gets or sets the watermark for disk usage with default value of 85%,
        /// meaning that DICOM Adapter will stop accepting (C-STORE-RQ) assocations,
        /// stop exporting and stop retreiving data via DICOMweb when used disk space
        /// is above the watermark.
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "watermarkPercent")]
        public uint Watermark { get; set; } = 85;

        /// <summary>
        /// Gets or sets the reserved disk space for DICOM Adapter with default value of 5GB.
        /// DICOM Adapter will stop accepting (C-STORE-RQ) assocations,
        /// stop exporting and stop retreiving data via DICOMweb when available disk space
        /// is less than the value.
        /// </summary>
        /// <value></value>
        [JsonProperty(PropertyName = "reservedSpaceGB")]
        public uint ReservedSpaceGB { get; set; } = 5;

        [JsonIgnore]
        public string TemporaryDataDirFullPath 
        {
            get
            {
                return _fileSystem.Path.GetFullPath(Temporary);
            }
        }
    }
}