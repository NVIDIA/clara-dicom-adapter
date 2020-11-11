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

using Microsoft.Extensions.Hosting;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// Interface for submiting a new job with the Clara Platform and report job submission results.
    /// </summary>
    public interface IJobStore : IHostedService
    {
        /// <summary>
        /// Submits one or more jobs to the Clara Platform.
        /// </summary>
        /// <param name="InferenceJob">Metadata of an inference request.</param>
        Task New(Job job, string jobName, IList<InstanceStorageInfo> instances);

        /// <summary>
        /// Update request status.
        /// </summary>
        /// <param name="inferenceJob">Metadata of an inference request.</param>
        /// <param name="status">Status of the request.</param>
        Task Update(InferenceJob inferenceJob, InferenceJobStatus status);

        /// <summary>
        /// Take returns the next pending request for submission.
        /// The default implementation blocks the call until a pending request is available for submission.
        /// </summary>
        /// <param name="cancellationToken">cancellation token used to cancel the action.</param>
        /// <returns><cr ="JobItem"/></returns>
        Task<InferenceJob> Take(CancellationToken cancellationToken);
    }
}