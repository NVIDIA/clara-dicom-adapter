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
using Dicom.Network;
using Nvidia.Clara.DicomAdapter.API;
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
            var dataset = new DicomDataset();
            dataset.Add(DicomTag.PatientID, "PID");
            dataset.Add(DicomTag.StudyInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.Add(DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.Add(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.Add(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage.UID);
            var file = new DicomFile(dataset);
            return new DicomCStoreRequest(file);
        }
    }
}