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


using System.Runtime.Serialization;

namespace Nvidia.Clara.DicomAdapter.API.Rest
{
    /// <summary>
    /// Specifies then authentication/authorization type for a connection.
    /// </summary>
    public enum ConnectionAuthType
    {
        /// <summary>
        /// HTTP Basic access authentication.
        /// <para><c>JSON value</c>: <c>Basic</c></para>
        /// </summary>
        Basic,
    }

    /// <summary>
    /// Specifies the type of data source interface.
    /// </summary>
    public enum InputInterfaceType
    {
        /// <summary>
        /// NVIDIA Clara Deploy only - specifies a Clara Pipeline to trigger with the request
        /// <para><c>JSON value</c>: <c>Algorithm</c></para>
        /// </summary>
        [EnumMember(Value = "Algorithm")]
        Algorithm,

        /// <summary>
        /// Retrieves data using DICOMweb API
        /// <para><c>JSON value</c>: <c>DICOMweb</c></para>
        /// </summary>
        [EnumMember(Value = "DICOMweb")]
        DicomWeb,

        /// <summary>
        /// Retrieves data using TCP based DICOM DIMSE services
        /// <para><c>JSON value</c>: <c>DIMSE</c></para>
        /// </summary>
        [EnumMember(Value = "DIMSE")]
        Dimse,
    }

    /// <summary>
    /// Specifies type of inference request.
    /// </summary>
    public enum InferenceRequestType
    {
        /// <summary>
        /// Retrieves dataset specified using DICOM UIDs
        /// <para><c>JSON value</c>: <c>DICOM_UID</c></para>
        /// </summary>
        [EnumMember(Value = "DICOM_UID")]
        DicomUid,

        /// <summary>
        /// Queries the data source using Patient ID and retrieves any associated studies.
        /// <para><c>JSON value</c>: <c>DICOM_PATIENT_ID</c></para>
        /// </summary>
        [EnumMember(Value = "DICOM_PATIENT_ID")]
        DicomPatientId,

        /// <summary>
        /// Queries the data source using Accession Number and retrieves any associated studies.
        /// <para><c>JSON value</c>: <c>ACCESSION_NUMBER</c></para>
        /// </summary>
        [EnumMember(Value = "ACCESSION_NUMBER")]
        AccessionNumber,
    }

    /// <summary>
    /// Permitted operations for a data source
    /// </summary>
    public enum InputInterfaceOperations
    {
        /// <summary>
        /// Query includes C-FIND, QIDO operations
        /// <para><c>JSON value</c>: <c>QUERY</c></para>
        /// </summary>
        [EnumMember(Value = "QUERY")]
        Query,

        /// <summary>
        /// Retrieve include C-MOVE, WADO operations
        /// <para><c>JSON value</c>: <c>RETRIEVE</c></para>
        /// </summary>
        [EnumMember(Value = "RETRIEVE")]
        Retrieve,

        /// <summary>
        /// DICOMweb WADO
        /// <para><c>JSON value</c>: <c>WADO Retrieve</c></para>
        /// </summary>
        [EnumMember(Value = "WADO Retrieve")]
        WadoRetrieve,
    }
}