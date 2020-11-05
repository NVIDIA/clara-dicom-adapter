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
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.DicomWeb.Client
{
    internal class WadoService : IWadoService
    {
        private readonly Uri _serviceUri;
        private readonly HttpClient _httpClient;
        private readonly ILogger _logger;

        public WadoService(HttpClient httpClient, Uri serviceUri, ILogger logger = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));

            Guard.Against.MalformUri(serviceUri, nameof(serviceUri));
            _serviceUri = serviceUri.EnsureUriEndsWithSlash();

            _logger = logger;
        }

        public async IAsyncEnumerable<DicomFile> Retrieve(
            string studyInstanceUid,
            params DicomTransferSyntax[] transferSyntaxes)
        {
            Guard.Against.NullOrWhiteSpace(studyInstanceUid, nameof(studyInstanceUid));
            DicomValidation.ValidateUI(studyInstanceUid);
            var studyUri = GetStudiesUri(studyInstanceUid);

            transferSyntaxes = transferSyntaxes.Trim();
            if (transferSyntaxes == null || transferSyntaxes.Length == 0)
            {
                transferSyntaxes = new[] { DicomTransferSyntax.ExplicitVRLittleEndian };
            }

            var message = new HttpRequestMessage(HttpMethod.Get, studyUri);
            message.Headers.Add(HeaderNames.Accept, BuildAcceptMediaHeader(MimeType.Dicom, transferSyntaxes));

            var response = await _httpClient.SendAsync(message);
            response.EnsureSuccessStatusCode();

            await foreach (var item in response.ToDicomAsyncEnumerable())
            {
                yield return item;
            }
        }

        public async IAsyncEnumerable<T> RetrieveMetadata<T>(
            string studyInstanceUid)
        {
            Guard.Against.NullOrWhiteSpace(studyInstanceUid, nameof(studyInstanceUid));
            DicomValidation.ValidateUI(studyInstanceUid);
            var studyUri = GetStudiesUri(studyInstanceUid);
            var studyMetadataUri = new Uri(studyUri, "metadata");

            await foreach (var metadata in GetMetadata<T>(studyMetadataUri))
            {
                yield return metadata;
            }
        }

        public async IAsyncEnumerable<DicomFile> Retrieve(
            string studyInstanceUid,
            string seriesInstanceUid,
            params DicomTransferSyntax[] transferSyntaxes)
        {
            Guard.Against.NullOrWhiteSpace(studyInstanceUid, nameof(studyInstanceUid));
            DicomValidation.ValidateUI(studyInstanceUid);
            Guard.Against.NullOrWhiteSpace(seriesInstanceUid, nameof(seriesInstanceUid));
            DicomValidation.ValidateUI(seriesInstanceUid);

            var seriesUri = GetSeriesUri(studyInstanceUid, seriesInstanceUid);

            transferSyntaxes = transferSyntaxes.Trim();
            if (transferSyntaxes == null || transferSyntaxes.Length == 0)
            {
                transferSyntaxes = new[] { DicomTransferSyntax.ExplicitVRLittleEndian };
            }

            var message = new HttpRequestMessage(HttpMethod.Get, seriesUri);
            message.Headers.Add(HeaderNames.Accept, BuildAcceptMediaHeader(MimeType.Dicom, transferSyntaxes));
            var response = await _httpClient.SendAsync(message);
            response.EnsureSuccessStatusCode();

            await foreach (var item in response.ToDicomAsyncEnumerable())
            {
                yield return item;
            }
        }

        public async IAsyncEnumerable<T> RetrieveMetadata<T>(
            string studyInstanceUid,
            string seriesInstanceUid)
        {
            Guard.Against.NullOrWhiteSpace(studyInstanceUid, nameof(studyInstanceUid));
            DicomValidation.ValidateUI(studyInstanceUid);
            Guard.Against.NullOrWhiteSpace(seriesInstanceUid, nameof(seriesInstanceUid));
            DicomValidation.ValidateUI(seriesInstanceUid);

            var seriesUri = GetSeriesUri(studyInstanceUid, seriesInstanceUid);
            var seriesMetadataUri = new Uri(seriesUri, "metadata");
            await foreach (var metadata in GetMetadata<T>(seriesMetadataUri))
            {
                yield return metadata;
            }
        }

        public async Task<DicomFile> Retrieve(
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid,
            params DicomTransferSyntax[] transferSyntaxes)
        {
            Guard.Against.NullOrWhiteSpace(studyInstanceUid, nameof(studyInstanceUid));
            DicomValidation.ValidateUI(studyInstanceUid);
            Guard.Against.NullOrWhiteSpace(seriesInstanceUid, nameof(seriesInstanceUid));
            DicomValidation.ValidateUI(seriesInstanceUid);
            Guard.Against.NullOrWhiteSpace(sopInstanceUid, nameof(sopInstanceUid));
            DicomValidation.ValidateUI(sopInstanceUid);

            var instanceUri = GetInstanceUri(studyInstanceUid, seriesInstanceUid, sopInstanceUid);

            transferSyntaxes = transferSyntaxes.Trim();
            if (transferSyntaxes == null || transferSyntaxes.Length == 0)
            {
                transferSyntaxes = new[] { DicomTransferSyntax.ExplicitVRLittleEndian };
            }

            var message = new HttpRequestMessage(HttpMethod.Get, instanceUri);
            message.Headers.Add(HeaderNames.Accept, BuildAcceptMediaHeader(MimeType.Dicom, transferSyntaxes));
            var response = await _httpClient.SendAsync(message);

            response.EnsureSuccessStatusCode();

            try
            {
                await foreach (var item in response.ToDicomAsyncEnumerable())
                {
                    return item;
                }
            }
            catch (Exception ex)
            {
                _logger?.Log(LogLevel.Error, ex, "Failed to retrieve instances");
            }

            return null;
        }

        public async Task<T> RetrieveMetadata<T>(
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid)
        {
            Guard.Against.NullOrWhiteSpace(studyInstanceUid, nameof(studyInstanceUid));
            DicomValidation.ValidateUI(studyInstanceUid);
            Guard.Against.NullOrWhiteSpace(seriesInstanceUid, nameof(seriesInstanceUid));
            DicomValidation.ValidateUI(seriesInstanceUid);
            Guard.Against.NullOrWhiteSpace(sopInstanceUid, nameof(sopInstanceUid));
            DicomValidation.ValidateUI(sopInstanceUid);

            var instanceUri = GetInstanceUri(studyInstanceUid, seriesInstanceUid, sopInstanceUid);
            var instancMetadataUri = new Uri(instanceUri, "metadata");

            try
            {
                await foreach (var metadata in GetMetadata<T>(instancMetadataUri))
                {
                    return metadata;
                }
            }
            catch (Exception ex) when (!(ex is UnsupportedReturnTypeException))
            {
                _logger?.Log(LogLevel.Error, ex, "Failed to retrieve metadata");
            }

            return default(T);
        }

        public async Task<DicomFile> Retrieve(
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid,
            IReadOnlyList<uint> frameNumbers,
            params DicomTransferSyntax[] transferSyntaxes)
        {
            throw new NotImplementedException("Retrieving instance frames API is not yet supported.");
        }

        public Task<byte[]> Retrieve(
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid,
            DicomTag dicomTag,
            params DicomTransferSyntax[] transferSyntaxes) =>
                Retrieve(studyInstanceUid, seriesInstanceUid, sopInstanceUid, dicomTag, null, transferSyntaxes);

        public async Task<byte[]> Retrieve(
            string studyInstanceUid,
            string seriesInstanceUid,
            string sopInstanceUid,
            DicomTag dicomTag,
            Tuple<int, int?> byteRange = null,
            params DicomTransferSyntax[] transferSyntaxes)
        {
            Guard.Against.NullOrWhiteSpace(studyInstanceUid, nameof(studyInstanceUid));
            DicomValidation.ValidateUI(studyInstanceUid);
            Guard.Against.NullOrWhiteSpace(seriesInstanceUid, nameof(seriesInstanceUid));
            DicomValidation.ValidateUI(seriesInstanceUid);
            Guard.Against.NullOrWhiteSpace(sopInstanceUid, nameof(sopInstanceUid));
            DicomValidation.ValidateUI(sopInstanceUid);

            return await Retrieve(new Uri(_serviceUri, $"studies/{studyInstanceUid}/series/{seriesInstanceUid}/instances/{sopInstanceUid}/bulk/{dicomTag.Group:X4}{dicomTag.Element:X4}"), byteRange, transferSyntaxes);
        }

        public Task<byte[]> Retrieve(
            Uri bulkdataUri,
            params DicomTransferSyntax[] transferSyntaxes) =>
                Retrieve(bulkdataUri, null, transferSyntaxes);

        public async Task<byte[]> Retrieve(
            Uri bulkdataUri,
            Tuple<int, int?> byteRange,
            params DicomTransferSyntax[] transferSyntaxes)
        {
            Guard.Against.Null(bulkdataUri, nameof(bulkdataUri));
            Guard.Against.MalformUri(bulkdataUri, nameof(bulkdataUri));

            transferSyntaxes = transferSyntaxes.Trim();
            if (transferSyntaxes == null || transferSyntaxes.Length == 0)
            {
                transferSyntaxes = new[] { DicomTransferSyntax.ExplicitVRLittleEndian };
            }

            var message = new HttpRequestMessage(HttpMethod.Get, bulkdataUri);
            message.Headers.Add(HeaderNames.Accept, BuildAcceptMediaHeader(MimeType.OctetStreme, transferSyntaxes));
            if (byteRange != null)
            {
                message.AddRange(byteRange);
            }
            var response = await _httpClient.SendAsync(message);
            response.EnsureSuccessStatusCode();
            return await response.ToBinaryData();
        }

        private string BuildAcceptMediaHeader(MimeType mimeType, DicomTransferSyntax[] transferSyntaxes)
        {
            if (transferSyntaxes == null || transferSyntaxes.Length == 0)
            {
                transferSyntaxes = new[] { DicomTransferSyntax.ExplicitVRLittleEndian };
            }

            var acceptHeaders = new List<string>();
            foreach (var mediaType in transferSyntaxes)
            {
                if (!MimeMappings.IsValidMediaType(mediaType))
                {
                    throw new ArgumentException($"invalid media type: {mediaType}");
                }

                acceptHeaders.Add($@"{MimeMappings.MultiPartRelated}; type=""{MimeMappings.MimeTypeMappings[mimeType]}""; transfer-syntax={mediaType.UID.UID}");
            }
            return string.Join(", ", acceptHeaders);
        }

        private Uri GetStudiesUri(string studyInstanceUid = "")
        {
            return string.IsNullOrWhiteSpace(studyInstanceUid) ?
                new Uri(_serviceUri, "studies/") :
                new Uri(_serviceUri, $"studies/{studyInstanceUid}/");
        }

        private Uri GetSeriesUri(string studyInstanceUid = "", string seriesInstanceUid = "")
        {
            if (string.IsNullOrWhiteSpace(studyInstanceUid))
            {
                if (!string.IsNullOrWhiteSpace(seriesInstanceUid))
                {
                    _logger?.Log(LogLevel.Warning, "Series Instance UID not provided, will retrieve all instances for study.");
                }
                return new Uri(_serviceUri, "series/");
            }
            else
            {
                var studiesUri = GetStudiesUri(studyInstanceUid);
                return string.IsNullOrWhiteSpace(seriesInstanceUid) ?
                    new Uri(studiesUri, "series/") :
                    new Uri(studiesUri, $"series/{seriesInstanceUid}/");
            }
        }

        private Uri GetInstanceUri(string studyInstanceUid, string seriesInstanceUid, string sopInstanceUid)
        {
            if (!string.IsNullOrWhiteSpace(studyInstanceUid) &&
                !string.IsNullOrWhiteSpace(seriesInstanceUid))
            {
                var seriesUri = GetSeriesUri(studyInstanceUid, seriesInstanceUid);
                return string.IsNullOrWhiteSpace(sopInstanceUid) ?
                    new Uri(seriesUri, "instances/") :
                    new Uri(seriesUri, $"instances/{sopInstanceUid}/");
            }
            else
            {
                if (!string.IsNullOrWhiteSpace(sopInstanceUid))
                {
                    _logger?.Log(LogLevel.Warning, "SOP Instance UID not provided, will retrieve all instances for study.");
                }
                return new Uri(_serviceUri, "instances/");
            }
        }

        private async IAsyncEnumerable<T> GetMetadata<T>(Uri uri)
        {
            if (IsUnsupportedReturnType<T>())
            {
                throw new UnsupportedReturnTypeException("invalid return type specified");
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

        private bool IsUnsupportedReturnType<T>()
        {
            return typeof(T) != typeof(string) &&
                typeof(T) != typeof(DicomDataset);
        }
    }
}