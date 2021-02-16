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

namespace Nvidia.Clara.DicomAdapter.API.Rest
{
    /// <summary>
    /// Response message of a successful inference request.
    /// </summary>
    public class InferenceRequestResponse
    {
        /// <summary>
        /// Gets or sets the original request transaction ID.
        /// </summary>
        public string TransactionId { get; set; }

        /// <summary>
        /// Gets or sets the Clara Platform job ID associated with the request.
        /// </summary>
        public string JobId { get; set; }

        /// <summary>
        /// Gets or sets the Clara Platform payload ID associated with the request.
        /// </summary>
        public string PayloadId { get; set; }
    }
}