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

using Ardalis.GuardClauses;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace Nvidia.Clara.DicomAdapter.API.Rest
{
    /// <summary>
    /// Status of an inference request.
    /// </summary>
    public enum InferenceRequestStatus
    {
        Unknown,
        Success,
        Fail
    }

    /// <summary>
    /// State of a inference request.
    /// </summary>
    public enum InferenceRequestState
    {
        Queued,
        InProcess,
        Completed,
    }

    /// <summary>
    /// Structure that represents an inference request based on ACR's Platform-Model Communication for AI.
    /// </summary>
    /// <example>
    /// <code>
    /// {
    ///     "transactionID": "ABCDEF123456",
    ///     "priority": "255",
    ///     "inputMetadata": { ... },
    ///     "inputResources": [ ... ]
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// Refer to [ACR DSI Model API](https://www.acrdsi.org/-/media/DSI/Files/ACR-DSI-Model-API.pdf)
    /// for more information.
    /// <para><c>transactionID></c> is required.</para>
    /// <para><c>inputMetadata></c> is required.</para>
    /// <para><c>inputResources></c> is required.</para>
    /// </remarks>
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
        /// </summary>
        /// <remarks>
        /// <para>Default value is <c>128</c> which maps to <c>JOB_PRIORITY_NORMAL</c>.</para>
        /// <para>Any value lower than <c>128</c> is map to <c>JOB_PRIORITY_LOWER</c>.</para>
        /// <para>Any value between <c>129-254</c> (inclusive) is set to <c>JOB_PRIORITY_HIGHER</c>.</para>
        /// <para>Value of <c>255</c> maps to <c>JOB_PRIORITY_IMMEDIATE</c>.</para>
        /// </remarks>
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
        /// <remarks>
        /// Internal use only.
        /// </remarks>
        [JsonProperty(PropertyName = "jobId")]
        public string JobId { get; set; }

        /// <summary>
        /// Internal use only - get or sets the Payload ID for the request once
        /// the job is created with Clara Platform Jobs API.
        /// </summary>
        /// <remarks>
        /// Internal use only.
        /// </remarks>
        [JsonProperty(PropertyName = "payloadId")]
        public string PayloadId { get; set; }

        /// <summary>
        /// Internal use only - get or sets the state of a inference request.
        /// </summary>
        /// <remarks>
        /// Internal use only.
        /// </remarks>
        [JsonProperty(PropertyName = "state")]
        public InferenceRequestState State { get; set; } = InferenceRequestState.Queued;

        /// <summary>
        /// Internal use only - get or sets the status of a inference request.
        /// </summary>
        /// <remarks>
        /// Internal use only.
        /// </remarks>
        [JsonProperty(PropertyName = "status")]
        public InferenceRequestStatus Status { get; set; } = InferenceRequestStatus.Unknown;

        /// <summary>
        /// Internal use only - get or sets the status of a inference request.
        /// </summary>
        /// <remarks>
        /// Internal use only.
        /// </remarks>
        [JsonProperty(PropertyName = "storagePath")]
        public string StoragePath { get; set; }

        /// <summary>
        /// Internal use only - get or sets number of retries performed.
        /// </summary>
        /// <remarks>
        /// Internal use only.
        /// </remarks>
        [JsonProperty(PropertyName = "tryCount")]
        public int TryCount { get; set; } = 0;

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
                switch (Priority)
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

        [JsonIgnore]
        public string JobName
        {
            get
            {
                return $"{Algorithm.Name}-{DateTime.UtcNow.ToString("dd-HHmmss")}".FixJobName();
            }
        }

        public InferenceRequest()
        {
            InputResources = new List<RequestInputDataResource>();
        }

        /// <summary>
        /// Configures temporary storage location used to store retrieved data.
        /// </summary>
        /// <param name="temporaryStorageRoot">Root path to the temporary storage location.</param>
        public void ConfigureTemporaryStorageLocation(string storagePath)
        {
            Guard.Against.NullOrWhiteSpace(storagePath, nameof(storagePath));
            if (!string.IsNullOrWhiteSpace(StoragePath))
            {
                throw new InferenceRequestException("StoragePath already configured.");
            }

            StoragePath = storagePath;
        }

        public bool IsValid(out string details)
        {
            var errors = new List<string>();

            if (InputResources.IsNullOrEmpty() ||
                InputResources.Count(predicate => predicate.Interface != InputInterfaceType.Algorithm) == 0)
            {
                errors.Add("No 'intputResources' specified.");
            }

            if (Algorithm is null)
            {
                errors.Add("No algorithm defined or more than one algorithms defined in 'intputResources'.  'intputResources' must include one algorithm/pipeline for the inference request.");
            }

            if (InputMetadata?.Details?.Type == InferenceRequestType.DicomUid)
            {
                if(InputMetadata.Details.Studies.IsNullOrEmpty())
                {
                    errors.Add("Request type is set to `DICOM_UID` but no studies defined.");
                }
            }
            else
            {
                errors.Add($"'inputMetadata' does not yet support type '{InputMetadata?.Details?.Type}'.");
            }

            details = string.Join(' ', errors);
            return errors.Count == 0;
        }
    }
}