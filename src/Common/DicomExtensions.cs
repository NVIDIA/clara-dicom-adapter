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
using Dicom;

namespace Nvidia.Clara.DicomAdapter.Common
{
    public static class DicomExtensions
    {
        /// <summary>
        /// Converts list of SOP Class UIDs to list of DicomTransferSyntax.
        /// DicomTransferSyntax.Parse internally throws DicomDataException if UID is invalid.
        /// </summary>
        /// <param name="uids">list of SOP Class UIDs</param>
        /// <returns>Array of DicomTransferSyntax or <code>null</code> if <code>uids</code> is null or empty.</returns>
        /// <exception cref="DicomDataException">Thrown in the specified UID is not a transfer syntax type.</exception>
        public static DicomTransferSyntax[] ToDicomTransferSyntaxArray(this IEnumerable<string> uids)
        {
            if (uids.IsNullOrEmpty())
            {
                return null;
            }

            var dicomTransferSyntaxes = new List<DicomTransferSyntax>();

            foreach (var uid in uids)
            {
                dicomTransferSyntaxes.Add(DicomTransferSyntax.Lookup(DicomUID.Parse(uid)));
            }
            return dicomTransferSyntaxes.ToArray();
        }
    }
}
