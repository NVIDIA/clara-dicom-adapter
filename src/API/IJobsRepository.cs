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

using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Nvidia.Clara.Platform;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// Interface for queuing jobs to be submitted to Clara Platform.
    /// The actual implementation stores all queued jobs in a database and
    /// deletes the job once the associated job is successfully submitted.
    /// </summary>
    public interface IJobRepository
    {
        /// <summary>
        /// Queues a new job for submission.
        /// <c>New</c> makes a copy of the instances to a temporary location that is to be
        /// uploaded by the `Nvidia.Clara.DicomAdapter.Server.Services.Jobs.JobSubmissionService`.
        /// </summary>
        /// <param name="job"><see cref="Nvidia.Clara.DicomAdapter.API.InferenceJob" /></param>
        Task Add(InferenceJob job);

        /// <summary>
        /// Queues a new job for submission and disable EF change tracking.
        /// <c>New</c> makes a copy of the instances to a temporary location that is to be
        /// uploaded by the `Nvidia.Clara.DicomAdapter.Server.Services.Jobs.JobSubmissionService`.
        /// </summary>
        /// <param name="job"><see cref="Nvidia.Clara.DicomAdapter.API.InferenceJob" /></param>
        Task Add(InferenceJob job, bool enableTracking);

        /// <summary>
        /// <c>Take</c> returns the next pending request for submission.
        /// The default implementation blocks the call until a pending request is available for submission.
        /// </summary>
        /// <param name="cancellationToken">cancellation token used to cancel the action.</param>
        /// <returns><see cref="Nvidia.Clara.DicomAdapter.API.InferenceJob"/></returns>
        Task<InferenceJob> Take(CancellationToken cancellationToken);

        /// <summary>
        /// Transition a job to the next processing state.
        /// </summary>
        /// <param name="job"><see cref="Nvidia.Clara.DicomAdapter.API.InferenceJob" /></param>
        /// <param name="status">Status of the request.</param>
        /// <param name="cancellationToken">cancellation token used to cancel the action.</param>
        Task<InferenceJob> TransitionState(InferenceJob job, InferenceJobStatus status, CancellationToken cancellationToken);

        /// <summary>
        /// Revert all jobs that are in processing states back into waiting states.
        /// </summary>
        Task ResetJobState();
    }

    /// <summary>
    /// Job storage exception
    /// </summary>
    public class JobStoreException : Exception
    {
        public JobStoreException()
        {
        }

        public JobStoreException(string message) : base(message)
        {
        }

        public JobStoreException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected JobStoreException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}