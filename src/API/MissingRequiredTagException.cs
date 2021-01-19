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

using Dicom;
using System.Linq;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// <c>MissingRequiredTagException</c> is thrown when the required DICOM tag is not found, null or blank.
    /// </summary>
    public class MissingRequiredTagException : System.Exception
    {
        public MissingRequiredTagException(DicomTag tag)
            : base($"Missing required DICOM tag for processing: {tag}")
        {
        }

        public MissingRequiredTagException(params DicomTag[] tags)
            : base($"Missing required DICOM tags for processing: {string.Join(", ", tags.Select(p => p.ToString()))}")
        {
        }
    }
}