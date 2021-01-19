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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.Dicom.DicomWeb.Client.API
{
    /// <summary>
    /// IStowService provides APIs to store DICOM instances to a remote
    /// DICOMweb server.
    /// This client does not transcode the input data; all input DICOM  dataset
    /// are transfered as-is using the stored Transfer Syntax.
    /// </summary>
    public interface IStowService : IServiceBase
    {
        /// <summary>
        /// Stores all DICOM files to the remote DICOMweb server.
        /// </summary>
        /// <param name="dicomFiles">DICOM files to be stored.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<DicomWebResponse<string>> Store(IEnumerable<DicomFile> dicomFiles, CancellationToken cancellationToken = default);

        /// <summary>
        /// Stores all DICOM files for the specified Study Instance UID.
        /// Note: any files found not matching the specified <c>studyInstanceUid</c> may be rejected.
        /// </summary>
        /// <param name="studyInstanceUid">Study Instance UID</param>
        /// <param name="dicomFiles">DICOM files to be stored.</param>
        /// <param name="cancellationToken">Cancellation token.</param>
        Task<DicomWebResponse<string>> Store(string studyInstanceUid, IEnumerable<DicomFile> dicomFiles, CancellationToken cancellationToken = default);
    }
}