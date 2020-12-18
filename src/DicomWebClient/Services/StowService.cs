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
using Dicom.IO.Writer;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.Dicom.DicomWeb.Client
{
    internal class StowService : ServiceBase, IStowService
    {
        private const string BOUNDARY = "---DICOM-INSTASNCE---";

        public StowService(HttpClient httpClient, ILogger logger = null)
            : base(httpClient, logger)
        {
        }

        /// <inheritdoc />
        public async Task<DicomWebResponse<string>> Store(IEnumerable<DicomFile> dicomFiles, CancellationToken cancellationToken = default) => await Store(string.Empty, dicomFiles, cancellationToken);

        /// <inheritdoc />
        public async Task<DicomWebResponse<string>> Store(string studyInstanceUid, IEnumerable<DicomFile> dicomFiles, CancellationToken cancellationToken = default)
        {
            Guard.Against.NullOrEmpty(dicomFiles, nameof(dicomFiles));

            var postUri = GetStudiesUri(studyInstanceUid);

            var toBeUploadedInstances = new List<DicomFile>();
            var streams = new List<Stream>();

            try
            {
                foreach (var dicomFile in dicomFiles)
                {
                    if (AreStudyInstanceUidsMatch(studyInstanceUid, dicomFile.Dataset.GetString(DicomTag.StudyInstanceUID)))
                    {
                        var stream = new MemoryStream();
                        await dicomFile.SaveAsync(stream, DicomWriteOptions.Default);
                        stream.Seek(0, SeekOrigin.Begin);

                        toBeUploadedInstances.Add(dicomFile);
                        streams.Add(stream);
                    }
                    else
                    {
                        _logger?.LogWarning($"Specified StudyInstanceUID {studyInstanceUid} does not match one in the file: {dicomFile}");
                    }
                }

                if (streams.Count == 0)
                {
                    _logger?.LogWarning("No DICOM files to upload.");
                    throw new ArgumentException("No matching DICOM files found or Study Instance UIDs do not match.");
                }

                return await UploadDataset(postUri, ConvertStreamsToContent(streams), cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                foreach (Stream stream in streams)
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }
            }
        }

        private HttpContent ConvertStreamsToContent(List<Stream> streams)
        {
            var content = new MultipartContent("related", BOUNDARY);
            content.Headers.ContentType.Parameters.Add(new NameValueHeaderValue("type", $"\"{MimeMappings.MediaTypeApplicationDicom.MediaType}\""));
            foreach (var stream in streams)
            {
                var byteContent = new StreamContent(stream);
                byteContent.Headers.ContentType = new MediaTypeHeaderValue(MimeMappings.MimeTypeMappings[MimeType.Dicom]);
                content.Add(byteContent);
            }
            return content;
        }

        private async Task<DicomWebResponse<string>> UploadDataset(string uri, HttpContent content, CancellationToken cancellationToken)
        {
            var message = new HttpRequestMessage(HttpMethod.Post, uri);
            message.Headers.Accept.Add(MimeMappings.MediaTypeApplicationDicomJson);
            message.Content = content;

            HttpResponseMessage response = null;
            try
            {
                response = await _httpClient.SendAsync(message, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to store DICOM instances.", ex);
                throw new DicomWebClientException(response?.StatusCode, "Failed to store DICOM instances", ex);
            }

            try
            {
                response.EnsureSuccessStatusCode();
            }
            catch (Exception ex)
            {
                _logger?.LogError($"Failed to store DICOM instances with response code: {response.StatusCode}", ex);
            }

            return await ParseContent(response).ConfigureAwait(false);
        }

        private async Task<DicomWebResponse<string>> ParseContent(HttpResponseMessage response)
        {
            try
            {
                var result = await response.Content.ReadAsStringAsync().ConfigureAwait(false); ;
                return new DicomWebResponse<string>(response.StatusCode, result);
            }
            catch (Exception ex)
            {
                _logger?.LogError("Failed to parse response.", ex);
                throw new DicomWebClientException(response.StatusCode, ex.Message);
            }
        }

        private bool AreStudyInstanceUidsMatch(string targetStudyInstanceUid, string studyInstanceUid)
        {
            if (string.IsNullOrEmpty(targetStudyInstanceUid))
            {
                return true;
            }

            return targetStudyInstanceUid.Equals(studyInstanceUid);
        }
    }
}