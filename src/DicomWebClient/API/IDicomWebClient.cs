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

using System;
using System.Net.Http.Headers;

namespace Nvidia.Clara.Dicom.DicomWeb.Client.API
{
    public enum DicomWebServiceType
    {
        /// <summary>
        /// DICOMweb Query Service
        /// </summary>
        Qido,

        /// <summary>
        /// DICOMweb Retrieve Service
        /// </summary>
        Wado,

        /// <summary>
        /// DICOMweb Store Service
        /// </summary>
        Stow,

        /// <summary>
        /// DICOMweb Delete Service
        /// </summary>
        Delete
    }

    /// <summary>
    /// A DICOMweb client for sending HTTP requests and receiving HTTP responses from a DICOMweb server.
    /// </summary>
    public interface IDicomWebClient
    {
        /// <summary>
        /// Provides DICOMweb WADO services for retrieving studies, series, instances, frames and bulkdata.
        /// </summary>
        IWadoService Wado { get; }

        /// <summary>
        /// Provides DICOMweb QIDO services for querying a remote server for studies.
        /// </summary>
        IQidoService Qido { get; }

        /// <summary>
        /// Provides DICOMweb STOW services for storing DICOM instances.
        /// </summary>
        IStowService Stow { get; }

        /// <summary>
        /// Configures the service URI of the DICOMweb service.
        /// </summary>
        /// <param name="uriRoot">Base URL of the DICOMweb server.</param>
        void ConfigureServiceUris(Uri uriRoot);

        /// <summary>
        /// Configures prefix for the specified service
        /// </summary>
        /// <param name="serviceType"><c>ServiceType</c> to be configured</param>
        /// <param name="urlPrefix">Url prefix</param>
        void ConfigureServicePrefix(DicomWebServiceType serviceType, string urlPrefix);

        /// <summary>
        /// Configures the authentication header for the DICOMweb client.
        /// </summary>
        /// <param name="value"></param>
        void ConfigureAuthentication(AuthenticationHeaderValue value);
    }
}