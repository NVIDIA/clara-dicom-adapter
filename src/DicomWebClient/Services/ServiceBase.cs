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
using Dicom;
using FellowOakDicom.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Net.Http.Headers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using Nvidia.Clara.Dicom.DicomWeb.Client.Common;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Nvidia.Clara.Dicom.DicomWeb.Client
{
    internal abstract class ServiceBase
    {
        protected readonly HttpClient _httpClient;
        protected readonly ILogger _logger;
        protected string RequestServicePrefix { get; private set; } = string.Empty;

        public ServiceBase(HttpClient httpClient, ILogger logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger;
        }

        public bool TryConfigureServiceUriPrefix(string uriPrefix)
        {
            Guard.Against.NullOrWhiteSpace(uriPrefix, nameof(uriPrefix));

            if (_httpClient.BaseAddress == null)
            {
                throw new InvalidOperationException("BaseAddress is not configured; call ConfigureServiceUris(...) first");
            }

            Uri newServiceUri = null;
            try
            {
                newServiceUri = new Uri(_httpClient.BaseAddress, uriPrefix);
                Guard.Against.MalformUri(newServiceUri, nameof(uriPrefix));
                RequestServicePrefix = $"{uriPrefix.Trim('/')}/";
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning($"Invalid urlPrefix provided: {uriPrefix} ({newServiceUri})", ex);
                return false;
            }
        }

        protected async IAsyncEnumerable<T> GetMetadata<T>(Uri uri)
        {
            if (IsUnsupportedReturnType<T>())
            {
                throw new UnsupportedReturnTypeException($"Type {typeof(T).Name} is an unsupported return type.");
            }

            var message = new HttpRequestMessage(HttpMethod.Get, uri);
            message.Headers.Add(HeaderNames.Accept, MimeMappings.MimeTypeMappings[MimeType.DicomJson]);
            var response = await _httpClient.SendAsync(message).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var jsonArray = JArray.Parse(json);
            foreach (var item in jsonArray.Children())
            {
                if (typeof(T) == typeof(string))
                {
                    yield return (T)(object)item.ToString(Formatting.Indented);
                }
                else if (typeof(T) == typeof(DicomDataset))
                {
                    var dataset = JsonConvert.DeserializeObject<DicomDataset>(item.ToString(), new JsonDicomConverter());
                    yield return (T)(object)dataset;
                }
            }
        }

        protected string GetStudiesUri(string studyInstanceUid = "")
        {
            return string.IsNullOrWhiteSpace(studyInstanceUid) ?
                $"{RequestServicePrefix}studies/" :
                $"{RequestServicePrefix}studies/{studyInstanceUid}/";
        }

        protected bool IsUnsupportedReturnType<T>()
        {
            return typeof(T) != typeof(string) &&
                typeof(T) != typeof(DicomDataset);
        }
    }
}