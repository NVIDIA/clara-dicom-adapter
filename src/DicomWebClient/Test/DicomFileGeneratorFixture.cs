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

using Dicom;
using FellowOakDicom.Serialization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Nvidia.Clara.Dicom.DicomWebClient.Test
{
    public class DicomFileGeneratorFixture
    {
        public const string MimeApplicationDicomJson = "application/dicom+json";

        internal async Task<HttpContent> GenerateInstances(
            int count,
            DicomUID studyUid,
            DicomUID seriesUid = null,
            DicomUID instanceUid = null,
            DicomTransferSyntax transferSynx = null)
        {
            var multipartContent = new MultipartContent("related");
            for (int i = 0; i < count; i++)
            {
                var bytes = await GenerateInstance(studyUid, seriesUid, instanceUid, transferSynx: transferSynx);
                multipartContent.Add(new ByteArrayContent(bytes));
            }
            return multipartContent;
        }

        internal HttpContent GenerateInstancesAsJson(
            int count,
            DicomUID studyUid,
            DicomUID seriesUid = null,
            DicomUID instanceUid = null)
        {
            var jsonArray = new JArray();
            for (int i = 0; i < count; i++)
            {
                var json = GenerateInstancesAsJson(studyUid, seriesUid, instanceUid);
                jsonArray.Add(JToken.Parse(json));
            }
            return new StringContent(jsonArray.ToString(Formatting.Indented), Encoding.UTF8, MimeApplicationDicomJson);
        }

        internal List<DicomFile> GenerateDicomFiles(int count, DicomUID studyUid)
        {
            var files = new List<DicomFile>();

            for (int i = 0; i < count; i++)
            {
                files.Add(new DicomFile(GenerateDicomDataset(studyUid, null, null, null)));
            }

            return files;
        }

        private string GenerateInstancesAsJson(DicomUID studyUid, DicomUID seriesUid = null, DicomUID instanceUid = null)
        {
            var dicomDataset = GenerateDicomDataset(studyUid, seriesUid, instanceUid, null);
            return JsonConvert.SerializeObject(dicomDataset, new JsonDicomConverter());
        }

        private async Task<byte[]> GenerateInstance(DicomUID studyUid, DicomUID seriesUid = null, DicomUID instanceUid = null, DicomTransferSyntax transferSynx = null)
        {
            var dicomDataset = GenerateDicomDataset(studyUid, seriesUid, instanceUid, transferSynx);
            var dicomFile = new DicomFile(dicomDataset);

            using (var ms = new MemoryStream())
            {
                await dicomFile.SaveAsync(ms);
                return ms.ToArray();
            }
        }

        private static DicomDataset GenerateDicomDataset(DicomUID studyUid, DicomUID seriesUid, DicomUID instanceUid, DicomTransferSyntax transferSynx)
        {
            if (seriesUid == null)
            {
                seriesUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            }

            if (instanceUid == null)
            {
                instanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();
            }

            if (transferSynx == null)
            {
                transferSynx = DicomTransferSyntax.ExplicitVRLittleEndian;
            }

            var dicomDataset = new DicomDataset(transferSynx ?? DicomTransferSyntax.ExplicitVRLittleEndian);
            dicomDataset.Add(DicomTag.PatientID, "TEST");
            dicomDataset.Add(DicomTag.SOPClassUID, DicomUID.CTImageStorage);
            dicomDataset.Add(DicomTag.StudyInstanceUID, studyUid);
            dicomDataset.Add(DicomTag.SeriesInstanceUID, seriesUid);
            dicomDataset.Add(DicomTag.SOPInstanceUID, instanceUid);
            return dicomDataset;
        }

        internal HttpContent GenerateByteData()
        {
            var multipartContent = new MultipartContent("related");

            var random = new Random();
            var data = new byte[10];
            random.NextBytes(data);
            multipartContent.Add(new ByteArrayContent(data));

            return multipartContent;
        }
    }
}