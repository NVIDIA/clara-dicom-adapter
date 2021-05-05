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

using System.Collections.Generic;
using Newtonsoft.Json;

namespace Nvidia.Clara.DicomAdapter.API.Rest
{
    /// <summary>
    /// Represents the metadata associated with an inference request.
    /// </summary>
    /// <example>
    /// <code>
    /// {
    ///     ...
    ///     "inputMetadata" : {
    ///         "details" : { ... }
    ///     }
    ///     ...
    /// }
    /// </code>
    /// </example>
    /// <remarks>
    /// <para><c>details></c> is required.</para>
    /// </remarks>
    public class InferenceRequestMetadata
    {
        /// <summary>
        /// Gets or sets the details of an inference request.
        /// Note: the preprocessor moves details defined here into <c>Inputs</c>
        /// </summary>
        [JsonProperty(PropertyName = "details")]
        internal InferenceRequestDetails Details { get; set; }

        /// <summary>
        /// Gets or sets an array of inference request details.
        /// Note: this is an extension to the ACR specs to enable multiple input data types.
        /// </summary>
        [JsonProperty(PropertyName = "inputs")]
        public IList<InferenceRequestDetails> Inputs { get; set; }

    }
}