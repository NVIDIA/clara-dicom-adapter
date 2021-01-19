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
using Microsoft.Net.Http.Headers;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;

namespace Nvidia.Clara.Dicom.DicomWeb.Client.Common
{
    internal static class HttpMessageExtension
    {
        public static void AddRange(this HttpRequestMessage request, Tuple<int, int?> byteRange = null)
        {
            Guard.Against.Null(request, nameof(request));
            if (byteRange == null)
            {
                request.Headers.Add(HeaderNames.Range, "byte=0-");
            }
            else
            {
                var end = byteRange.Item2.HasValue ? byteRange.Item2.Value.ToString() : "";
                request.Headers.Add(HeaderNames.Range, $"byte={byteRange.Item1}-{end}");
            }
        }

        public static async IAsyncEnumerable<DicomFile> ToDicomAsyncEnumerable(this HttpResponseMessage response)
        {
            Guard.Against.Null(response, nameof(response));
            Guard.Against.Null(response.Content, nameof(response.Content));
            await foreach (var buffer in DecodeMultipartMessage(response))
            {
                using (var memoryStream = new MemoryStream(buffer))
                {
                    yield return await DicomFile.OpenAsync(memoryStream, FileReadOption.ReadAll);
                }
            }
        }

        public static async Task<byte[]> ToBinaryData(this HttpResponseMessage response)
        {
            Guard.Against.Null(response, nameof(response));
            using (var memoryStream = new MemoryStream())
            {
                await foreach (var buffer in DecodeMultipartMessage(response))
                {
                    await memoryStream.WriteAsync(buffer, 0, buffer.Length);
                }
                return memoryStream.ToArray();
            }
        }

        private static async IAsyncEnumerable<byte[]> DecodeMultipartMessage(HttpResponseMessage response)
        {
            Guard.Against.Null(response, nameof(response));
            var contentType = response.Content.Headers.ContentType;
            if (contentType.MediaType != MimeMappings.MultiPartRelated)
            {
                throw new ResponseDecodeException($"Unexpected media type {contentType.MediaType}.  Expected {MimeMappings.MultiPartRelated}");
            }

            var multipartContent = await response.Content.ReadAsMultipartAsync().ConfigureAwait(false);
            foreach (var content in multipartContent.Contents)
            {
                yield return await content.ReadAsByteArrayAsync().ConfigureAwait(false); ;
            }
        }
    }
}