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

namespace Nvidia.Clara.Dicom.API
{
    /// <summary>
    /// Specifies then authentication/authorization type for a connection.
    /// </summary>
    public enum ConnectionAuthType
    {
        /// <summary>
        /// HTTP Basic access authentication.
        /// </summary>
        Basic,
    }

    /// <summary>
    /// Specifies the type of data source interface.
    /// </summary>
    public enum InputInterfaceType
    {
        /// <summary>
        /// NVIDIA Clara Deploy only - specifies a Clara Pipeline to trigger with the request
        /// </summary>
        [EnumMember(Value = "Algorithm")]
        Algorithm,

        /// <summary>
        /// Retrieves data using DICOMweb API
        /// </summary>
        [EnumMember(Value = "DICOMweb")]
        DicomWeb,

        /// <summary>
        /// Retrieves data using TCP based DICOM DIMSE services
        /// </summary>
        [EnumMember(Value = "DIMSE")]
        Dimse,
    }

    /// <summary>
    /// Specifies type of inference request.
    /// </summary>
    public enum InferenceRequestType
    {
        /// <summary>
        /// Retrieves dataset specified using DICOM UIDs
        /// </summary>
        [EnumMember(Value = "DICOM_UID")]
        DicomUid,

        /// <summary>
        /// Queries the data source using Patient ID and retrieves any associated studies.
        /// </summary>
        [EnumMember(Value = "DICOM_PATIENT_ID")]
        DicomPatientId,

        /// <summary>
        /// Queries the data source using Accession Number and retrieves any associated studies.
        /// </summary>
        [EnumMember(Value = "ACCESSION_NUMBER")]
        AccessionNumber,
    }

    /// <summary>
    /// Permitted operations for a data source
    /// </summary>
    public enum InputInterfaceOperations
    {
        /// <summary>
        /// Query includes C-FIND, QIDO operations
        /// </summary>
        [EnumMember(Value = "QUERY")]
        Query,

        /// <summary>
        /// Retrieve include C-MOVE, WADO operations
        /// </summary>
        [EnumMember(Value = "RETRIEVE")]
        Retrieve,

        /// <summary>
        /// DICOMweb WADO
        /// </summary>
        [EnumMember(Value = "WADO Retrieve")]
        WadoRetrieve,
    }

    /// <summary>
    /// Represents an inference request based on ACR's Platform-Model Communication for AI.
    /// See https://www.acrdsi.org/-/media/DSI/Files/ACR-DSI-Model-API.pdf.
    /// </summary>
    public class InferenceRequest
    {
        /// <summary>
        /// Gets or set the transaction ID of a request.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "transactionID")]
        public string TransactionId { get; set; }

        /// <summary>
        /// Gets or sets the priority of a request.
        /// Default value is 128 which maps to JOB_PRIORITY_NORMAL.
        /// Any value lower than 128 is map to JOB_PRIORITY_LOWER.
        /// Any value between 129-254 (inclusive) is set to JOB_PRIORITY_HIGHER.
        /// Value of 255 maps to JOB_PRIORITY_IMMEDIATE.
        /// </summary>
        [Range(0, 255)]
        [JsonProperty(PropertyName = "priority")]
        public byte Priority { get; set; } = 128;

        /// <summary>
        /// Gets or sets the details of the data associated with the inference request.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "inputMetadata")]
        public InferenceRequestMetadata InputMetadata { get; set; }

        /// <summary>
        /// Gets or set a list of data sources to query/retrieve data from.
        /// When multiple data sources are specified, the system will query based on
        /// the order the list was received.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "inputResources")]
        public IList<RequestInputDataResource> InputResources { get; set; }
        
        /// <summary>
        /// Internal use - gets or sets the Job ID for the request once 
        /// the job is created with Clara Platform Jobs API.
        /// </summary>
        [JsonProperty(PropertyName = "jobId")]
        public string JobId { get; set; }

        /// <summary>
        /// Internal use only - get or sets the Payload ID for the request once
        /// the job is created with Clara Platform Jobs API.
        /// </summary>
        [JsonProperty(PropertyName = "payloadId")]
        public string PayloadId { get; set; }


        [JsonIgnore]
        public InputConnectionDetails Algorithm
        {
            get
            {
                return InputResources.FirstOrDefault(predicate => predicate.Interface == InputInterfaceType.Algorithm)?.ConnectionDetails;
            }
        }

        [JsonIgnore]
        public JobPriority ClaraJobPriority
        {
            get
            {
                switch(Priority)
                {
                    case byte n when (n < 128):
                        return JobPriority.Lower;
                    case byte n when (n == 128):
                        return JobPriority.Normal;
                    case byte n when (n == 255):
                        return JobPriority.Immediate;
                    default:
                        return JobPriority.Higher;
                }
            }
        }

        public bool IsValidate(out string details)
        {
            var errors = new List<string>();

            if (InputResources.IsNullOrEmpty())
            {
                errors.Add("No 'intputResources' specified.");
            }
            else if (InputResources.Count(predicate => predicate.Interface == InputInterfaceType.Algorithm) != 1)
            {
                errors.Add("No algorithm defined or more than one algorithms defined in 'intputResources'.  'intputResources' must include one algorithm/pipeline for the inference request.");
            }


            details = string.Join(' ', errors);
            return errors.Count == 0;

        }
    }

    /// <summary>
    /// Represents an input resource (data source).
    /// </summary>
    public class RequestInputDataResource
    {
        /// <summary>
        /// Gets or sets the type of interface or a data source.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "interface")]
        public InputInterfaceType Interface { get; set; }

        /// <summary>
        /// Gets or sets connection details of a data source.
        /// </summary>
        [JsonProperty(PropertyName = "connectionDetails")]
        public InputConnectionDetails ConnectionDetails { get; set; }
    }

    /// <summary>
    /// Connection details of a data source.
    /// </summary>
    public class InputConnectionDetails
    {
        /// <summary>
        /// Gets or sets the name of the algorithm. Used when <see cref="T:Nvidia.Clara.Dicom.API.InputInterfaceType" />
        /// is <see cref="T:Nvidia.Clara.Dicom.API.InputInterfaceType.Algorithm" />.
        /// <c>Name</c> is also used as the job name.
        /// </summary>
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }

        /// <summary>
        /// Gets or sets the Clara Deploy Pipeline ID. Used when <see cref="T:Nvidia.Clara.Dicom.API.InputInterfaceType" />
        /// is <see cref="T:Nvidia.Clara.Dicom.API.InputInterfaceType.Algorithm" />.
        /// </summary>
        [JsonProperty(PropertyName = "id")]
        public string PipelineId { get; set; }

        /// <summary>
        /// Gets or sets a list of permitted operations for the connection.
        /// </summary>
        [JsonProperty(PropertyName = "operations")]
        public IList<InputInterfaceOperations> Operations { get; set; }

        /// <summary>
        /// Gets or sets the resource URI (Uniform Resource Identifier) of the connection.
        /// </summary>
        [JsonProperty(PropertyName = "uri")]
        public string Uri { get; set; }

        /// <summary>
        /// Gets or sets the authentication/authorization token of the connection.
        /// For HTTP basic access authentication, the value must be encoded in based 64 using "{username}:{password}" format.
        /// </summary>
        [JsonProperty(PropertyName = "authID")]
        public string AuthId { get; set; }

        /// <summary>
        /// Gets or sets the type of the authentication token used for the connection.
        /// Defaults to HTTP Basic if not specified.
        /// </summary>
        [JsonProperty(PropertyName = "authType")]
        public ConnectionAuthType AuthType { get; set; } = ConnectionAuthType.Basic;
    }

    /// <summary>
    /// Represents the metadata associated with an inference request.
    /// </summary>
    public class InferenceRequestMetadata
    {
        /// <summary>
        /// Gets or sets the details of an inference request.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "details")]
        public InferenceRequestDetails Details { get; set; }
    }

    /// <summary>
    /// Details of an inference request.
    /// </summary>
    public class InferenceRequestDetails
    {
        /// <summary>
        /// Gets or sets the type of the inference request.
        /// </summary>

        [Required, JsonConverter(typeof(StringEnumConverter))]
        [JsonProperty(PropertyName = "type")]
        public InferenceRequestType Type { get; set; }

        /// <summary>
        /// Gets or sets the DICOM studies to be retrieved.
        /// Used when <c>Type</c> is <see cref="T:Nvidia.Clara.Dicom.API.InferenceRequestType.DicomUid" />.
        /// </summary>
        [RequiredIf(nameof(Type), InferenceRequestType.DicomUid)]
        [JsonProperty(PropertyName = "studies")]
        public IList<RequestedStudy> Studies { get; set; }

        /// <summary>
        /// Gets or sets Patient ID that is used to query the data source.
        /// Used when <c>Type</c> is <see cref="T:Nvidia.Clara.Dicom.API.InferenceRequestType.DicomPatientId" />.
        /// </summary>
        [RequiredIf(nameof(Type), InferenceRequestType.DicomPatientId)]
        [JsonProperty(PropertyName = "PatientID")]
        public string PatientId { get; set; }

        /// <summary>
        /// Gets or sets Access Number that is used to query the data source.
        /// Used when <c>Type</c> is <see cref="T:Nvidia.Clara.Dicom.API.InferenceRequestType.AccessionNumber" />.
        /// </summary>
        [RequiredIf(nameof(Type), InferenceRequestType.AccessionNumber)]
        [JsonProperty(PropertyName = "accessionNumber")]
        public IList<string> AccessionNumber { get; set; }
    }

    /// <summary>
    /// Details of a DICOM study to be retrieved for an inference request.
    /// </summary>
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

    /// <summary>
    /// Details of a DICOM series to be retrieved for an inference request.
    /// </summary>
    public class RequestedSeries
    {
        /// <summary>
        /// Gets or sets the Series Instance UID to be retrieved.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "SeriesInstanceUID")]
        public string SeriesInstanceUid { get; set; }

        /// <summary>
        /// Gets or sets a list of DICOM instances to be retrieved.
        /// </summary>
        [JsonProperty(PropertyName = "instances")]
        public IList<RequestedInstance> Instances { get; set; }
    }

    /// <summary>
    /// Details of a DICOM instance to be retrieved for an inference request.
    /// </summary>
    public class RequestedInstance
    {
        /// <summary>
        /// Gets or sets the SOP Instance UID to be retrieved.
        /// </summary>
        [Required]
        [JsonProperty(PropertyName = "SOPInstanceUID")]
        public IList<string> SopInstanceUid { get; set; }
    }

    /// <summary>
    /// Kubernetes CRD status for <see cref="T:Nvidia.Clara.Dicom.API.InferenceRequest" />.
    /// </summary>
    public class InferenceRequestStatus
    {
        internal static readonly InferenceRequestStatus Default = new InferenceRequestStatus();
    }
}