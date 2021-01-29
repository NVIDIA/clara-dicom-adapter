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

using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.Platform;
using System;

namespace Nvidia.Clara.Dicom.API.Rest
{
    /// <summary>
    /// Response message of a inference status query.
    /// </summary>
    public class InferenceStatusResponse
    {
        /// <summary>
        /// Gets or set the transaction ID of a request.
        /// </summary>
        public string TransactionId { get; set; }

        /// <summary>
        /// Gets or sets Clara Platform status.
        /// </summary>
        public PlatformStatus Platform { get; set; } = new PlatformStatus();

        /// <summary>
        /// Gets or set DICOM Adapter status.
        /// </summary>
        public DicomAdapterStatus Dicom { get; set; } = new DicomAdapterStatus();

        /// <summary>
        /// Gets or sets additional message.
        /// </summary>
        public string Message { get; set; }

        public class PlatformStatus
        {
            /// <summary>
            /// Gets or set Clara Platform generated Job ID.
            /// </summary>
            public string JobId { get; set; }

            /// <summary>
            /// Gets or set Clara Platform generated Payload ID.
            /// </summary>
            public string PayloadId { get; set; }

            /// <summary>
            /// Gets or set job's status.
            /// </summary>
            public JobStatus Status { get; set; }

            /// <summary>
            /// Gets or sets job's state.
            /// </summary>
            public JobState State { get; set; }

            /// <summary>
            /// Gets or set job's priority.
            /// </summary>
            public JobPriority Priority { get; set; }

            /// <summary>
            /// Gets or set when the job was created.
            /// </summary>
            public DateTimeOffset? Created { get; set; }

            /// <summary>
            /// Gets or set when the job started execution.
            /// </summary>
            public DateTimeOffset? Started { get; set; }

            /// <summary>
            /// Gets or set when the job stopped execution.
            /// </summary>
            public DateTimeOffset? Stopped { get; set; }
        }

        public class DicomAdapterStatus
        {
            /// <summary>
            /// Gets or sets the state of the inference request.
            /// </summary>
            public InferenceRequestState State { get; set; }

            /// <summary>
            /// Gets or set the state of the inference status.
            /// </summary>
            public InferenceRequestStatus Status { get; set; }
        }
    }
}