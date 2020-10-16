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
using Nvidia.Clara.Dicom.DicomWeb.Client.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Nvidia.Clara.Dicom.DicomWebClient.Test
{
    public class HttpMessageExtensionsTest
    {
        const string PatientName = "DOE^JOHN";
        readonly byte[] ByteData = new byte[]
        {
           0x01, 0x02, 0x03, 0x04, 0x05
        };

        #region AddRange Test
        [Fact(DisplayName = "AddRange shall throw when input is null")]
        public void AddRange_Null()
        {
            HttpRequestMessage request = null;
            Assert.Throws<ArgumentNullException>(() => request.AddRange(null));
        }

        [Fact(DisplayName = "AddRange when byteRange is null")]
        public void AddRange_ByteRangeIsNull()
        {
            HttpRequestMessage request = new HttpRequestMessage();
            request.AddRange(null);

            var range = request.Headers.Range;

            Assert.Equal("byte", range.Unit);
            Assert.Equal(0, range.Ranges.First().From);
            Assert.Null(range.Ranges.First().To);
        }

        [Fact(DisplayName = "AddRange when byteRange contains only start")]
        public void AddRange_ByteRangeHasOnlyStart()
        {
            HttpRequestMessage request = new HttpRequestMessage();
            request.AddRange(new Tuple<int, int?>(100, null));

            var range = request.Headers.Range;

            Assert.Equal("byte", range.Unit);
            Assert.Equal(100, range.Ranges.First().From);
            Assert.Null(range.Ranges.First().To);
        }

        [Fact(DisplayName = "AddRange when byteRange contains valid range")]
        public void AddRange_ByteRangeHasValidValues()
        {
            HttpRequestMessage request = new HttpRequestMessage();
            request.AddRange(new Tuple<int, int?>(100, 200));

            var range = request.Headers.Range;

            Assert.Equal("byte", range.Unit);
            Assert.Equal(100, range.Ranges.First().From);
            Assert.Equal(200, range.Ranges.First().To);
        }
        #endregion

        #region ToDicomAsyncEnumerable Test
        [Fact(DisplayName = "ToDicomAsyncEnumerable shall throw when input is null")]
        public async Task ToDicomAsyncEnumerable_Null()
        {
            HttpResponseMessage message = null;
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await foreach (var item in message.ToDicomAsyncEnumerable())
                { }
            });
        }

        [Fact(DisplayName = "ToDicomAsyncEnumerable shall throw with non-supported MIME type")]
        public async Task ToDicomAsyncEnumerable_NotMultipartRelated()
        {
            HttpResponseMessage message = new HttpResponseMessage();
            var multipartContent = new MultipartContent("form");
            await AddDicomFileContent(multipartContent);
            message.Content = multipartContent;
            await Assert.ThrowsAsync<ResponseDecodeException>(async () =>
            {
                await foreach (var item in message.ToDicomAsyncEnumerable())
                {
                }
            });
        }

        [Fact(DisplayName = "ToDicomAsyncEnumerable shall return DicomFiles")]
        public async Task ToDicomAsyncEnumerable_Ok()
        {
            HttpResponseMessage message = new HttpResponseMessage();
            var multipartContent = new MultipartContent("related");
            await AddDicomFileContent(multipartContent);
            message.Content = multipartContent;

            var result = new List<DicomFile>();
            await foreach (var item in message.ToDicomAsyncEnumerable())
            {
                result.Add(item);
            }

            Assert.Single(result);
            Assert.Equal(PatientName, result.First().Dataset.GetString(DicomTag.PatientName));
        }

        private async Task AddDicomFileContent(MultipartContent multipartContent)
        {
            var dicomDataset = new DicomDataset(DicomTransferSyntax.ExplicitVRLittleEndian);
            dicomDataset.Add(DicomTag.PatientName, PatientName);
            dicomDataset.Add(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage);
            dicomDataset.Add(DicomTag.SOPInstanceUID, DicomUID.Generate());
            var dicomFile = new DicomFile(dicomDataset);

            using (var ms = new MemoryStream())
            {
                await dicomFile.SaveAsync(ms);
                multipartContent.Add(new ByteArrayContent(ms.ToArray()));
            }
        }
        #endregion

        #region ToBinaryData Test
        [Fact(DisplayName = "ToBinaryData shall throw when input is null")]
        public async Task ToBinaryData_Null()
        {
            HttpResponseMessage message = null;
            await Assert.ThrowsAsync<ArgumentNullException>(async () =>
            {
                await message.ToBinaryData();
            });
        }

        [Fact(DisplayName = "ToDicomAsyncEnumerable shall return byte array")]
        public async Task ToBinaryData_Ok()
        {
            HttpResponseMessage message = new HttpResponseMessage();
            var multipartContent = new MultipartContent("related");
            AddByteArrayContent(multipartContent);
            message.Content = multipartContent;

            var result = await message.ToBinaryData();

            Assert.Equal(ByteData, result);
        }

        private void AddByteArrayContent(MultipartContent multipartContent)
        {
            multipartContent.Add(new ByteArrayContent(ByteData));
        }

        #endregion
    }
}
