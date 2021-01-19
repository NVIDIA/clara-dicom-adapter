/*
 * Apache License, Version 2.0
 * Copyright 2019-2021 NVIDIA Corporation
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
using Dicom.Network;
using Nvidia.Clara.DicomAdapter.API;
using System.IO;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;

namespace Nvidia.Clara.DicomAdapter.Test.Shared
{
    public class InstanceGenerator
    {
        public static InstanceStorageInfo GenerateInstance(
            string storageRootFullPath,
            string calledAeTitle,
            uint associationId = 1,
            IFileSystem fileSystem = null)
        {
            fileSystem = fileSystem ?? new MockFileSystem();
            var instance = InstanceStorageInfo.CreateInstanceStorageInfo(
                GenerateDicomCStoreRequest(),
                storageRootFullPath,
                calledAeTitle,
                associationId,
                fileSystem ?? new MockFileSystem());

            fileSystem.File.Create(instance.InstanceStorageFullPath).Close();
            return instance;
        }

        public static DicomCStoreRequest GenerateDicomCStoreRequest()
        {
            return new DicomCStoreRequest(GenerateDicomFile());
        }

        public static DicomFile GenerateDicomFile(
            string studyInstanceUid = null,
            string seriesInstanceUid = null,
            string sopInstanceUid = null,
            IFileSystem fileSystem = null)
        {
            var dataset = GenerateDicomDataset(studyInstanceUid, seriesInstanceUid, ref sopInstanceUid);

            fileSystem?.File.Create($"{sopInstanceUid}.dcm");
            return new DicomFile(dataset);
        }

        private static DicomDataset GenerateDicomDataset(string studyInstanceUid, string seriesInstanceUid, ref string sopInstanceUid)
        {
            if (string.IsNullOrWhiteSpace(sopInstanceUid))
            {
                sopInstanceUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
            }
            var dataset = new DicomDataset();
            dataset.Add(DicomTag.PatientID, "PID");
            dataset.Add(DicomTag.StudyInstanceUID, studyInstanceUid ?? DicomUIDGenerator.GenerateDerivedFromUUID().UID);
            dataset.Add(DicomTag.SeriesInstanceUID, seriesInstanceUid ?? DicomUIDGenerator.GenerateDerivedFromUUID().UID);
            dataset.Add(DicomTag.SOPInstanceUID, sopInstanceUid);
            dataset.Add(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage.UID);
            return dataset;
        }

        public static byte[] GenerateDicomData(
            string studyInstanceUid = null,
            string seriesInstanceUid = null,
            string sopInstanceUid = null)
        {
            var dataset = GenerateDicomDataset(studyInstanceUid, seriesInstanceUid, ref sopInstanceUid);

            var dicomfile = new DicomFile(dataset);
            using var ms = new MemoryStream();
            dicomfile.Save(ms);
            return ms.ToArray();
        }
    }
}