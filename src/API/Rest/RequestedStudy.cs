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
    /// Details of a DICOM study to be retrieved for an inference request.
    /// </summary>
    /// <example>
    /// <code>
    /// {
    ///     ...
    ///     "studies" : [
    ///         "StudyInstanceUID": "1.2.3.4.555.6666.7777",
    ///         "series": [
    ///             "SeriesInstanceUID": "1.2.3.4.55.66.77.88",
    ///             "instances": [
    ///                 "SOPInstanceUID": [
    ///                     "1.2.3.4.5.6.7.8.99.1",    
    ///                     "1.2.3.4.5.6.7.8.99.2",    
    ///                     "1.2.3.4.5.6.7.8.99.3",    
    ///                     ...
    ///                 ]    
    ///             ]
    ///         ]
    ///     ]
    ///     ...
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><c>StudyInstanceUid></c> is required.</para>
    /// <para>If <c>Series></c> is not specified, the entire study is retrieved.</para>
    /// </remarks>
    public class RequestedStudy
    {
        /// <summary>
        /// Gets or sets the Study Instance UID to be retrieved.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "StudyInstanceUID")]
        public string StudyInstanceUid { get; set; }

        /// <summary>
        /// Gets or sets a list of DICOM series to be retrieved.
        /// </summary>
        [JsonProperty(PropertyName = "series")]
        public IList<RequestedSeries> Series { get; set; }
    }
}