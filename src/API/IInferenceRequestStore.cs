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
using Nvidia.Clara.DicomAdapter.API.Rest;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// Interface for submiting a new job with the Clara Platform and report job submission results.
    /// </summary>
    public interface IInferenceRequestStore : IHostedService
    {
        /// <summary>
        /// Adds new inference request to the repository.
        /// Default implementation uses Kubernetes CRD to store.
        /// </summary>
        /// <param name="inferenceRequest">The inference request to be added.</param>
        Task Add(InferenceRequest inferenceRequest);

        /// <summary>
        /// Updates an infernece request's status.
        /// The default implementation drops the request after 3 retries if status is 
        /// <see cref="Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestStatus.Fail" />.
        /// </summary>
        /// <param name="inferenceRequest">The inference request to be updated.</param>
        /// <param name="status">Current status of the inference request.</param>
        Task Update(InferenceRequest inferenceRequest, InferenceRequestStatus status);

        /// <summary>
        /// <c>Take</c> returns the next pending inference request for data retrieval.
        /// The default implementation blocks the call until a pending inference request is available for process.
        /// </summary>
        /// <param name="cancellationToken">cancellation token used to cancel the action.</param>
        /// <returns><see cref="Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequest"/></returns>
        Task<InferenceRequest> Take(CancellationToken cancellationToken);
    }
}