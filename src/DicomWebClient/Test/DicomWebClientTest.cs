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
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
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

        [Fact(DisplayName = "ConfigureServiceUris - set rootUri")]
        public void ConfigureServiceUris_SetAllUris()
        {
            var httpClient = new HttpClient();
            var dicomWebClient = new DicomWebClientClass(httpClient, null);
            var rootUri = new Uri(BaseUri);
            dicomWebClient.ConfigureServiceUris(rootUri);
        }

        [Theory(DisplayName = "ConfigureServiceUris - throws on malformed prefix")]
        [InlineData(DicomWebServiceType.Qido, "/bla\\?/")]
        [InlineData(DicomWebServiceType.Wado, "/bla\\?/")]
        [InlineData(DicomWebServiceType.Stow, "/bla\\?/")]
        public void ConfigureServicePrefix_ThrowsMalformedPrefixes(DicomWebServiceType serviceType, string prefix)
        {
            var httpClient = new HttpClient();
            var dicomWebClient = new DicomWebClientClass(httpClient, null);
            var rootUri = new Uri(BaseUri);
            dicomWebClient.ConfigureServiceUris(rootUri);
            Assert.Throws<ArgumentException>(() => dicomWebClient.ConfigureServicePrefix(serviceType, prefix));
        }

        [Fact(DisplayName = "ConfigureServicePrefix - throws if base address is not configured")]
        public void ConfigureServicePrefix_ThrowsIfBaseAddressIsNotConfigured()
        {
            var httpClient = new HttpClient();
            var dicomWebClient = new DicomWebClientClass(httpClient, null);
            var rootUri = new Uri(BaseUri);
            Assert.Throws<InvalidOperationException>(() => dicomWebClient.ConfigureServicePrefix(DicomWebServiceType.Qido, "/prefix"));
        }

        [Theory(DisplayName = "ConfigureServicePrefix - sets service prefix")]
        [InlineData(DicomWebServiceType.Qido, "/qido/")]
        [InlineData(DicomWebServiceType.Wado, "/wado")]
        [InlineData(DicomWebServiceType.Stow, "/stow")]
        public void ConfigureServicePrefix_SetsServicePrefix(DicomWebServiceType serviceType, string prefix)
        {
            var httpClient = new HttpClient();
            var dicomWebClient = new DicomWebClientClass(httpClient, null);
            var rootUri = new Uri(BaseUri);
            dicomWebClient.ConfigureServiceUris(rootUri);
            dicomWebClient.ConfigureServicePrefix(serviceType, prefix);
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