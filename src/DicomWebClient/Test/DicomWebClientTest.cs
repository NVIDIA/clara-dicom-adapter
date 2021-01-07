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
using System.Net.Http;
using System.Net.Http.Headers;
using Xunit;
using DicomWebClientClass = Nvidia.Clara.Dicom.DicomWeb.Client.DicomWebClient;

namespace Nvidia.Clara.Dicom.DicomWebClient.Test
{
    public class DicomWebClientTest
    {
        private const string BaseUri = "http://dummy/api/";

        [Fact(DisplayName = "Constructor test")]
        public void ConstructorTest()
        {
            Assert.Throws<ArgumentNullException>(() => new DicomWebClientClass(null, null));
        }

        [Fact(DisplayName = "ConfigureServiceUris - throws on malformed uri root")]
        public void ConfigureServiceUris_ThrowsMalformedUriRoot()
        {
            var httpClient = new HttpClient();
            var dicomWebClient = new DicomWebClientClass(httpClient, null);
            Assert.Throws<ArgumentNullException>(() => dicomWebClient.ConfigureServiceUris(null));
        }

        [Fact(DisplayName = "ConfigureServiceUris - throws on malformed uri root")]
        public void ConfigureServiceUris_ThrowsMalformedPrefixes()
        {
            var httpClient = new HttpClient();
            var dicomWebClient = new DicomWebClientClass(httpClient, null);
            var rootUri = new Uri(BaseUri);
            Assert.Throws<ArgumentException>(() => dicomWebClient.ConfigureServiceUris(rootUri, "/bla\\?/"));
            Assert.Throws<ArgumentException>(() => dicomWebClient.ConfigureServiceUris(rootUri, "/wado", "/bla\\?/"));
            Assert.Throws<ArgumentException>(() => dicomWebClient.ConfigureServiceUris(rootUri, "/wado", "/qido", "/bla\\?/"));
            Assert.Throws<ArgumentException>(() => dicomWebClient.ConfigureServiceUris(rootUri, "/wado", "/qido", "/stow", "/bla\\?/"));
        }

        [Fact(DisplayName = "ConfigureServiceUris - sets all URIs")]
        public void ConfigureServiceUris_SetAllUris()
        {
            var httpClient = new HttpClient();
            var dicomWebClient = new DicomWebClientClass(httpClient, null);
            var rootUri = new Uri(BaseUri);
            dicomWebClient.ConfigureServiceUris(rootUri);
            dicomWebClient.ConfigureServiceUris(rootUri, "/wado", "/qido", "/stow", "/delete");
        }

        [Fact(DisplayName = "ConfigureAuthentication - throws if value is null")]
        public void ConfigureAuthentication_ThrowsIfNull()
        {
            var httpClient = new HttpClient();
            var dicomWebClient = new DicomWebClientClass(httpClient, null);
            var rootUri = new Uri(BaseUri);

            Assert.Throws<ArgumentNullException>(() => dicomWebClient.ConfigureAuthentication(null));
        }

        [Fact(DisplayName = "ConfigureAuthentication - sets auth header")]
        public void ConfigureAuthentication_SetsAuthHeader()
        {
            var httpClient = new HttpClient();
            var dicomWebClient = new DicomWebClientClass(httpClient, null);

            var auth = new AuthenticationHeaderValue("basic", "value");
            dicomWebClient.ConfigureAuthentication(auth);

            Assert.Equal(httpClient.DefaultRequestHeaders.Authorization, auth);
        }
    }
}