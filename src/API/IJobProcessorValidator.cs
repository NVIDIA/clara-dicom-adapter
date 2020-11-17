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

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// Interface for providing a validator for JobProcessor.  See <see cref="ProcessorValidationAttribute"/>.
    /// </summary>
    public interface IJobProcessorValidator
    {
        /// <summary>
        /// Validates process settings and throws if any invalid entires or ignored entries are found.
        /// </summary>
        /// <param name="aeTitle">AE Title of the associated settings.</param>
        /// <param name="processorSettings">Settings of a job processor.</param>
        void Validate(string aeTitle, Dictionary<string, string> processorSettings);
    }
}