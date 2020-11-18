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

using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using Nvidia.Clara.Dicom.DicomWeb.Client.Common;
using Nvidia.Clara.DicomAdapter.DicomWeb.Client.API;
using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Nvidia.Clara.DicomAdapter.DicomWeb.Client
{
    /// <inheritdoc/>
    public class DicomWebClient : IDicomWebClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        /// <inheritdoc/>
        public IWadoService Wado { get; private set; }

        /// <summary>
        /// Initializes a new instance of the DicomWebClient class that connects to the specified URI using the credentials provided.
        /// </summary>
        /// <param name="uriRoot">Base URL of the DICOMweb server.</param>
        /// <param name="credentials">Authentication information to access the remote service.</param>
        /// <param name="wadoUrlPrefix">Optional URL path prefix for WADO RESTful services.</param>
        /// <param name="qidoUrlPrefix">Optional URL path prefix for QIDO RESTful services.</param>
        /// <param name="stowUrlPrefix">Optional URL path prefix for STOW RESTful services.</param>
        /// <param name="deleteUrlPrefix">Optional URL path prefix for DELETE RESTful services.></param>
        /// <param name="logger">Optional logger for capturing client logs.</param>
        public DicomWebClient(
            Uri uriRoot,
            AuthenticationHeaderValue credentials = null,
            string wadoUrlPrefix = "",
            string qidoUrlPrefix = "",
            string stowUrlPrefix = "",
            string deleteUrlPrefix = "",
            ILogger logger = null)
        {
            Guard.Against.Null(uriRoot, nameof(uriRoot));
            Guard.Against.MalformUri(uriRoot, nameof(uriRoot));

            _httpClient = new HttpClient();
            _httpClient.BaseAddress = uriRoot;
            _logger = logger;

            if (credentials != null)
            {
                _httpClient.DefaultRequestHeaders.Authorization = credentials;
            }

            InitServices(uriRoot, wadoUrlPrefix, qidoUrlPrefix, stowUrlPrefix, deleteUrlPrefix);
        }

        /// <summary>
        /// Initializes a new instance of the DicomWebClient class that connects to the specified URI with the specified handler.
        /// </summary>
        /// <param name="uriRoot">Base URL of the DICOMweb server.</param>
        /// <param name="httpClientHandler">The HTTP handler stack to use for sending requests.</param>
        /// <param name="wadoUrlPrefix">Optional URL path prefix for WADO RESTful services.</param>
        /// <param name="qidoUrlPrefix">Optional URL path prefix for QIDO RESTful services.</param>
        /// <param name="stowUrlPrefix">Optional URL path prefix for STOW RESTful services.</param>
        /// <param name="deleteUrlPrefix">Optional URL path prefix for DELETE RESTful services.></param>
        /// <param name="logger">Optional logger for capturing client logs.</param>
        public DicomWebClient(
            Uri uriRoot,
            HttpClientHandler httpClientHandler,
            string wadoUrlPrefix = "",
            string qidoUrlPrefix = "",
            string stowUrlPrefix = "",
            string deleteUrlPrefix = "",
            ILogger logger = null)
        {
            Guard.Against.Null(uriRoot, nameof(uriRoot));
            Guard.Against.MalformUri(uriRoot, nameof(uriRoot));
            Guard.Against.Null(httpClientHandler, nameof(httpClientHandler));

            _httpClient = new HttpClient(httpClientHandler);
            _httpClient.BaseAddress = uriRoot;
            _logger = logger;

            InitServices(uriRoot, wadoUrlPrefix, qidoUrlPrefix, stowUrlPrefix, deleteUrlPrefix);
        }

        private void InitServices(
            Uri uriRoot,
            string wadoUrlPrefix,
            string qidoUrlPrefix,
            string stowUrlPrefix,
            string deleteUrlPrefix)
        {
            uriRoot = uriRoot.EnsureUriEndsWithSlash();

            this.Wado = new WadoService(
                _httpClient,
                string.IsNullOrWhiteSpace(wadoUrlPrefix) ? uriRoot : new Uri(uriRoot, wadoUrlPrefix),
                _logger);
        }
    }
}