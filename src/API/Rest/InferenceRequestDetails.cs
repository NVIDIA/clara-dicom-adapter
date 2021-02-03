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
using Newtonsoft.Json.Converters;
using System.Collections.Generic;

namespace Nvidia.Clara.DicomAdapter.API.Rest
{
    /// <summary>
    /// Details of an inference request.
    /// </summary>
    /// <example>
    /// <code>
    /// {
    ///     ...
    ///     "details" : {
    ///         "type": "DICOM_UID",
    ///         "studies": [ ... ]
    ///     }
    ///     ...
    /// } or
    ///
    /// {
    ///     ...
    ///     "details" : {
    ///         "type": "DICOM_PATIENT_ID",
    ///         "PatientID": "..."
    ///     }
    ///     ...
    /// } or
    ///
    /// {
    ///     ...
    ///     "details" : {
    ///         "type": "ACCESSION_NUMBER",
    ///         "accessionNumber": [ ... ]
    ///     }
    ///     ...
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><c>type></c> is required.</para>
    /// <para><c>PatientID></c> is required if <c>type</c> is <see cref="Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestType.DicomUid" />.</para>
    /// <para><c>studies></c> is required if <c>type</c> is <see cref="Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestType.DicomPatientId" />.</para>
    /// <para><c>accessionNumber></c> is required if <c>type</c> is <see cref="Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestType.AccessionNumber" />.</para>
    /// </remarks>
    public class InferenceRequestDetails
    {
        /// <summary>
        /// Gets or sets the type of the inference request.
        /// </summary>
        [JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = "type")]
        public InferenceRequestType Type { get; set; }

        /// <summary>
        /// Gets or sets the DICOM studies to be retrieved.
        /// Used when <c>Type</c> is <see cref="T:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestType.DicomUid" />.
        /// </summary>
        [JsonProperty(PropertyName = "studies")]
        public IList<RequestedStudy> Studies { get; set; }

        /// <summary>
        /// Gets or sets Patient ID that is used to query the data source.
        /// Used when <c>Type</c> is <see cref="T:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestType.DicomPatientId" />.
        /// </summary>
        [JsonProperty(PropertyName = "PatientID")]
        public string PatientId { get; set; }

        /// <summary>
        /// Gets or sets Access Number that is used to query the data source.
        /// Used when <c>Type</c> is <see cref="T:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestType.AccessionNumber" />.
        /// </summary>
        [JsonProperty(PropertyName = "accessionNumber")]
        public IList<string> AccessionNumber { get; set; }
    }
}