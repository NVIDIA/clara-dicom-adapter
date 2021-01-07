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

namespace Nvidia.Clara.Dicom.DicomWeb.Client.API
{
    /// <summary>
    /// IQidoService provides APIs to query studies, series and instances
    /// on a remote DICOMweb server.
    /// </summary>
    public interface IQidoService : IServiceBase
    {
        /// <summary>
        /// Search for all studies.
        /// </summary>
        IAsyncEnumerable<string> SearchForStudies();

        /// <summary>
        /// Search for studies based on provided query parameters.
        /// </summary>
        /// <param name="queryParameters">A dictionary object where the <c>Key</c> contains the DICOM tag
        /// or keyword of an attribute and the <c>Value</c> contains the expected value to match.</param>
        IAsyncEnumerable<string> SearchForStudies(IReadOnlyDictionary<string, string> queryParameters);

        /// <summary>
        /// Search for studies based on provided query parameters with additional DICOM fields to be included in the response message.
        /// </summary>
        /// <param name="queryParameters">A dictionary object where the <c>Key</c> contains the DICOM tag
        /// or keyword of an attribute and the <c>Value</c> contains the expected value to match.</param>
        /// <param name="fieldsToInclude">Liist of DICOM tags of name of the DICOM tag to be included in the response.</param>
        IAsyncEnumerable<string> SearchForStudies(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude);

        /// <summary>
        /// Search for studies based on provided query parameters with additional DICOM fields to be included in the response message.
        /// </summary>
        /// <param name="queryParameters">A dictionary object where the <c>Key</c> contains the DICOM tag
        /// or keyword of an attribute and the <c>Value</c> contains the expected value to match.</param>
        /// <param name="fieldsToInclude">Liist of DICOM tags of name of the DICOM tag to be included in the response.</param>
        /// <param name="fuzzyMatching">Whether fuzzy semantic matching should be performed.</param>
        IAsyncEnumerable<string> SearchForStudies(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude, bool fuzzyMatching);

        /// <summary>
        /// Search for studies based on provided query parameters with additional DICOM fields to be included in the response message.
        /// </summary>
        /// <param name="queryParameters">A dictionary object where the <c>Key</c> contains the DICOM tag
        /// or keyword of an attribute and the <c>Value</c> contains the expected value to match.</param>
        /// <param name="fieldsToInclude">Liist of DICOM tags of name of the DICOM tag to be included in the response.</param>
        /// <param name="limit">Maximum number of results to be returned.</param>
        IAsyncEnumerable<string> SearchForStudies(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude, bool fuzzyMatching, int limit);

        /// <summary>
        /// Search for studies based on provided query parameters with additional DICOM fields to be included in the response message.
        /// </summary>
        /// <param name="queryParameters">A dictionary object where the <c>Key</c> contains the DICOM tag
        /// or keyword of an attribute and the <c>Value</c> contains the expected value to match.</param>
        /// <param name="fieldsToInclude">Liist of DICOM tags of name of the DICOM tag to be included in the response.</param>
        /// <param name="limit">Maximum number of results to be returned.</param>
        /// <param name="offset">Number of results to be skipped.</param>
        IAsyncEnumerable<string> SearchForStudies(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude, bool fuzzyMatching, int limit, int offset);
    }
}