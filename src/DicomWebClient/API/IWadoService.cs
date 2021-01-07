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
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nvidia.Clara.Dicom.DicomWeb.Client.API
{
    /// <summary>
    /// IWadoService provides APIs to retrieve data according to
    /// DICOMweb WADO-RS specs.
    /// </summary>
    public interface IWadoService : IServiceBase
    {
        /// <summary>
        /// Retrieves all DICOM instances specified in the study.
        /// </summary>
        /// <param name="studyInstanceUid">Study Instance UID</param>
        /// <param name="transferSyntaxes">An array of supported transfer syntaxes. Default set to Explicit VR Little Endian (1.2.840.10008.1.2.1)</param>
        /// <returns>A list of <see cref="DicomFile"/> containing DICOM instances for the study.</returns>
        IAsyncEnumerable<DicomFile> Retrieve(
            string studyInstanceUid,
            params DicomTransferSyntax[] transferSyntaxes);

        /// <summary>
        /// Retrieves the metadata of all instances specified in the study.
        /// </summary>
        /// <typeparam name="T">T must be type of <see cref="DicomDataset"/> or <see cref="string"/></typeparam>
        /// <param name="studyInstanceUid">Study Instance UID</param>
        /// <returns>An enumerable of <c>T</c> containing DICOM instances for the study.</returns>
        IAsyncEnumerable<T> RetrieveMetadata<T>(
            string studyInstanceUid);

        /// <summary>
        /// Retrieves all DICOM instances secified in the series.
        /// </summary>
        /// <param name="studyInstanceUid">Study Instance UID</param>
        /// <param name="seriesInstanceUid">Series Instance UID</param>
        /// <param name="transferSyntaxes">An array of supported transfer syntaxes. Default set to Explicit VR Little Endian (1.2.840.10008.1.2.1)</param>
        /// <returns>A list of <see cref="DicomFile"/> containing DICOM instances for the series.</returns>
        IAsyncEnumerable<DicomFile> Retrieve(
            string studyInstanceUid,
            string seriesInstanceUid,
            params DicomTransferSyntax[] transferSyntaxes);

        /// <summary>
        /// Retrieves the metadata of all instances specified in the study.
        /// </summary>
        /// <typeparam name="T">T must be type of <see cref="DicomDataset"/> or <see cref="string"/></typeparam>
        /// <param name="studyInstanceUid">Study Instance UID</param>
        /// <param name="seriesInstanceUid">Series Instance UID</param>
        /// <returns>An enumerable of <c>T</c> containing DICOM instances for the series.</returns>
        IAsyncEnumerable<T> RetrieveMetadata<T>(
            string studyInstanceUid,
            string seriesInstanceUid);

        /// <summary>
        /// Retrieves a DICOM instance.
        /// </summary>
        /// <param name="studyInstanceUid">Study Instance UID</param>
        /// <param name="seriesInstanceUid">Series Instance UID</param>
        /// <param name="sopInstanceUid">SOP Instance UID</param>
        /// <param name="transferSyntaxes">An array of supported transfer syntaxes. Default set to Explicit VR Little Endian (1.2.840.10008.1.2.1)</param>
        /// <returns>A <see cref="DicomFile"/> representing the DICOM instance.</returns>
        Task<DicomFile> Retrieve(
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid,
            params DicomTransferSyntax[] transferSyntaxes);

        /// <summary>
        /// Retrieves the metadata of the specified DICOM instance.
        /// </summary>
        /// <typeparam name="T">T must be type of <see cref="DicomDataset"/> or <see cref="string"/></typeparam>
        /// <param name="studyInstanceUid">Study Instance UID</param>
        /// <param name="seriesInstanceUid">Series Instance UID</param>
        /// <param name="sopInstanceUid">SOP Instance UID</param>
        /// <returns>A <c>T</c> containing DICOM metadata for the instance.</returns>
        Task<T> RetrieveMetadata<T>(
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid);

        /// <summary>
        /// Retrieves one or more frames from a multi-frame DICOM instance.
        /// </summary>
        /// <param name="studyInstanceUid">Study Instance UID</param>
        /// <param name="seriesInstanceUid">Series Instance UID</param>
        /// <param name="sopInstanceUid">SOP Instance UID</param>
        /// <param name="frameNumbers">The frames to retrieve within a multi-frame instance. (One-based indices)</param>
        /// <param name="transferSyntaxes">An array of supported transfer syntaxes. Default set to Explicit VR Little Endian (1.2.840.10008.1.2.1)</param>
        /// <returns>A <see cref="DicomFile"/> representing the DICOM instance.</returns>
        Task<DicomFile> Retrieve(
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid,
            IReadOnlyList<uint> frameNumbers,
            params DicomTransferSyntax[] transferSyntaxes);

        /// <summary>
        /// Retrieves bulkdata in a DICOM instance.
        /// </summary>
        /// <param name="studyInstanceUid">Study Instance UID</param>
        /// <param name="seriesInstanceUid">Series Instance UID</param>
        /// <param name="sopInstanceUid">SOP Instance UID</param>
        /// <param name="dicomTag">DICOM tag containing to bulkdata</param>
        /// <param name="transferSyntaxes">An array of supported transfer syntaxes to be used to encode the bulkdata. Default set to Explicit VR Little Endian (1.2.840.10008.1.2.1)</param>
        /// <returns>A byte array containing the bulkdata.</returns>
        Task<byte[]> Retrieve(
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid,
            DicomTag dicomTag,
            params DicomTransferSyntax[] transferSyntaxes);

        /// <summary>
        /// Retrieves a specific range of bulkdata in a DICOM instance.
        /// </summary>
        /// <param name="studyInstanceUid">Study Instance UID</param>
        /// <param name="seriesInstanceUid">Series Instance UID</param>
        /// <param name="sopInstanceUid">SOP Instance UID</param>
        /// <param name="dicomTag">DICOM tag containing to bulkdata</param>
        /// <param name="byteRange">Range of data to retrieve.
        /// Entire range if <c>null</c>.
        /// If <c>byteRange.Item2</c> is not specified then value specified in <c>byteRange.Item1</c>(start) to the end is retrieved.</param>
        /// <param name="transferSyntaxes">An array of supported transfer syntaxes to be used to encode the bulkdata. Default set to Explicit VR Little Endian (1.2.840.10008.1.2.1)</param>
        /// <returns>A byte array containing the bulkdata.</returns>
        Task<byte[]> Retrieve(
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid,
            DicomTag dicomTag,
            Tuple<int, int?> byteRange = null,
            params DicomTransferSyntax[] transferSyntaxes);

        /// <summary>
        /// Retrieves bulkdata in a DICOM instance.
        /// </summary>
        /// <param name="bulkdataUri">URI to the instance.  The DICOM tag to retrieve must specified in the URI.</param>
        /// <param name="transferSyntaxes">An array of supported transfer syntaxes to be used to encode the bulkdata. Default set to Explicit VR Little Endian (1.2.840.10008.1.2.1)</param>
        /// <returns>A byte array containing the bulkdata.</returns>
        Task<byte[]> Retrieve(
            Uri bulkdataUri,
            params DicomTransferSyntax[] transferSyntaxes);

        /// <summary>
        /// Retrieves a specific range of bulkdata in a DICOM instance.
        /// </summary>
        /// <param name="bulkdataUri">URI to the instance.  The DICOM tag to retrieve must specified in the URI.</param>
        /// <param name="byteRange">Range of data to retrieve.
        /// Entire range if <c>null</c>.
        /// If <c>byteRange.Item2</c> is not specified then value specified in <c>byteRange.Item1</c>(start) to the end is retrieved.</param>
        /// <param name="transferSyntaxes">An array of supported transfer syntaxes to be used to encode the bulkdata. Default set to Explicit VR Little Endian (1.2.840.10008.1.2.1)</param>
        /// <returns>A byte array containing the bulkdata.</returns>
        Task<byte[]> Retrieve(
            Uri bulkdataUri,
            Tuple<int, int?> byteRange = null,
            params DicomTransferSyntax[] transferSyntaxes);
    }
}