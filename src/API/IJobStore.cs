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

using Microsoft.Extensions.Hosting;
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// Interface for queuing jobs to be submitted to Clara Platform.
    /// The actual implementation stores all queued jobs in Kubernetes CRD objects and
    /// deletes the CRD item once the associated job is successfully submitted.
    /// </summary>
    public interface IJobStore : IHostedService
    {
        /// <summary>
        /// Queues a new job for submission.
        /// <c>New</c> makes a copy of the instances to a temporary location that is to be
        /// uploaded by the `Nvidia.Clara.DicomAdapter.Server.Services.Jobs.JobSubmissionService`.
        /// </summary>
        /// <param name="job"><see cref="Nvidia.Clara.DicomAdapter.API.Job" /> includes the Job ID and Payload ID returned from the Clara Job.Create API call.</param>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="instances">DICOM instances to be uploaded to the payload.</param>
        Task Add(Job job, string jobName, IList<InstanceStorageInfo> instances);

        /// <summary>
        /// Updates job status.
        /// </summary>
        /// <param name="inferenceJob">Metadata of an inference request.</param>
        /// <param name="status">Status of the request.</param>
        Task Update(InferenceJob inferenceJob, InferenceJobStatus status);

        /// <summary>
        /// <c>Take</c> returns the next pending request for submission.
        /// The default implementation blocks the call until a pending request is available for submission.
        /// </summary>
        /// <param name="cancellationToken">cancellation token used to cancel the action.</param>
        /// <returns><see cref="Nvidia.Clara.DicomAdapter.API.InferenceJob"/></returns>
        Task<InferenceJob> Take(CancellationToken cancellationToken);
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