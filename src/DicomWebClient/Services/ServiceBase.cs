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

namespace Nvidia.Clara.DicomAdapter.DicomWeb.Client
{
    internal abstract class ServiceBase
    {
        protected readonly Uri _serviceUri;
        protected readonly HttpClient _httpClient;
        protected readonly ILogger _logger;

        public ServiceBase(HttpClient httpClient, Uri serviceUri, ILogger logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            Guard.Against.MalformUri(serviceUri, nameof(serviceUri));
            _serviceUri = serviceUri.EnsureUriEndsWithSlash();

            _logger = logger;
        }

        protected async IAsyncEnumerable<T> GetMetadata<T>(Uri uri)
        {
            if (IsUnsupportedReturnType<T>())
            {
                throw new UnsupportedReturnTypeException($"Type {typeof(T).Name} is an unsupported return type.");
            }

            var message = new HttpRequestMessage(HttpMethod.Get, uri);
            message.Headers.Add(HeaderNames.Accept, MimeMappings.MimeTypeMappings[MimeType.DicomJson]);
            var response = await _httpClient.SendAsync(message);
            response.EnsureSuccessStatusCode();

            var json = await response.Content.ReadAsStringAsync();
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

        protected bool IsUnsupportedReturnType<T>()
        {
            return typeof(T) != typeof(string) &&
                typeof(T) != typeof(DicomDataset);
        }
    }
}