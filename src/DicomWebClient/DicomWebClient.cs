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
using System;
using System.Net.Http;
using System.Net.Http.Headers;

namespace Nvidia.Clara.Dicom.DicomWeb.Client
{
    /// <inheritdoc/>
    public class DicomWebClient : IDicomWebClient
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        /// <inheritdoc/>
        public IWadoService Wado { get; private set; }

        /// <inheritdoc/>
        public IQidoService Qido { get; private set; }

        /// <inheritdoc/>
        public IStowService Stow { get; private set; }

        /// <summary>
        /// Initializes a new instance of the DicomWebClient class that connects to the specified URI using the credentials provided.
        /// </summary>
        /// <param name="httpClient">HTTP client .</param>
        /// <param name="logger">Optional logger for capturing client logs.</param>
        public DicomWebClient(HttpClient httpClient, ILogger<DicomWebClient> logger)
        {
            Guard.Against.Null(httpClient, nameof(httpClient));

            _httpClient = httpClient;
            _logger = logger;

            Wado = new WadoService(
                _httpClient,
                _logger);

            Qido = new QidoService(
                _httpClient,
                _logger);

            Stow = new StowService(
                _httpClient,
                _logger);
        }

        /// <inheritdoc/>
        public void ConfigureServiceUris(Uri uriRoot, string wadoUrlPrefix = "", string qidoUrlPrefix = "", string stowUrlPrefix = "", string deleteUrlPrefix = "")
        {
            Guard.Against.MalformUri(uriRoot, nameof(uriRoot));

            _httpClient.BaseAddress = uriRoot;

            if (!string.IsNullOrWhiteSpace(wadoUrlPrefix) && !Wado.TryConfigureServiceUriPrefix(wadoUrlPrefix))
            {
                throw new ArgumentException($"Invalid wadoUrlPrefix specified: {wadoUrlPrefix}");
            }
            if (!string.IsNullOrWhiteSpace(qidoUrlPrefix) && !Wado.TryConfigureServiceUriPrefix(qidoUrlPrefix))
            {
                throw new ArgumentException($"Invalid qidoUrlPrefix specified: {qidoUrlPrefix}");
            }
            if (!string.IsNullOrWhiteSpace(stowUrlPrefix) && !Wado.TryConfigureServiceUriPrefix(stowUrlPrefix))
            {
                throw new ArgumentException($"Invalid stowUrlPrefix specified: {stowUrlPrefix}");
            }
            if (!string.IsNullOrWhiteSpace(deleteUrlPrefix) && !Wado.TryConfigureServiceUriPrefix(deleteUrlPrefix))
            {
                throw new ArgumentException($"Invalid deleteUrlPrefix specified: {deleteUrlPrefix}");
            }
        }

        /// <inheritdoc/>
        public void ConfigureAuthentication(AuthenticationHeaderValue value)
        {
            Guard.Against.Null(value, nameof(value));

            _httpClient.DefaultRequestHeaders.Authorization = value;
        }
    }
}