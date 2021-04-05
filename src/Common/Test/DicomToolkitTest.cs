/*
 * Apache License, Version 2.0
 * Copyright 2021 NVIDIA Corporation
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
using System;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Common.Test
{
    public class DicomToolkitTest
    {
        public DicomToolkitTest()
        {
        }

        [Fact(DisplayName = "HasValidHeder - false when reading a text file")]
        public void HasValidHeader_False()
        {
            var filename = Path.GetTempFileName();
            using (var writer = new StreamWriter(filename))
            {
                writer.WriteLine("Hello World!");
            }

            var dicomToolkit = new DicomToolkit();
            Assert.False(dicomToolkit.HasValidHeader(filename));
        }

        [Fact(DisplayName = "HasValidHeder - true with a valid DICOM file")]
        public void HasValidHeader_True()
        {
            var filename = Path.GetTempFileName();
            var dicomFile = new DicomFile();
            dicomFile.Dataset.Add(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dicomFile.FileMetaInfo.TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
            dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID = DicomUIDGenerator.GenerateDerivedFromUUID();
            dicomFile.FileMetaInfo.MediaStorageSOPClassUID = DicomUIDGenerator.GenerateDerivedFromUUID();

            dicomFile.Save(filename);

            var dicomToolkit = new DicomToolkit();
            Assert.True(dicomToolkit.HasValidHeader(filename));
        }

        [Fact(DisplayName = "Open - a valid DICOM file")]
        public void Open_ValidFile()
        {
            var filename = Path.GetTempFileName();
            var dicomFile = new DicomFile();
            dicomFile.Dataset.Add(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dicomFile.FileMetaInfo.TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
            dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID = DicomUIDGenerator.GenerateDerivedFromUUID();
            dicomFile.FileMetaInfo.MediaStorageSOPClassUID = DicomUIDGenerator.GenerateDerivedFromUUID();

            dicomFile.Save(filename);

            var dicomToolkit = new DicomToolkit();
            dicomToolkit.Open(filename);
        }

        [Fact(DisplayName = "TryGetString - invalid DICOM file")]
        public void TryGetString_InvalidDicomFile()
        {
            var filename = Path.GetTempFileName();
            using (var writer = new StreamWriter(filename))
            {
                writer.WriteLine("Hello World!");
            }

            var dicomToolkit = new DicomToolkit();
            Assert.False(dicomToolkit.TryGetString(filename, DicomTag.SOPClassUID, out var _));
        }

        [Fact(DisplayName = "TryGetString - a valid DICOM file path")]
        public void TryGetString_ValidFilePath()
        {
            var filename = Path.GetTempFileName();
            var dicomFile = new DicomFile();
            var expectedSop = DicomUIDGenerator.GenerateDerivedFromUUID();
            dicomFile.Dataset.Add(DicomTag.SOPInstanceUID, expectedSop);
            dicomFile.FileMetaInfo.TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
            dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID = DicomUIDGenerator.GenerateDerivedFromUUID();
            dicomFile.FileMetaInfo.MediaStorageSOPClassUID = DicomUIDGenerator.GenerateDerivedFromUUID();

            dicomFile.Save(filename);

            var dicomToolkit = new DicomToolkit();
            Assert.True(dicomToolkit.TryGetString(filename, DicomTag.SOPInstanceUID, out var sopInstanceUId));
            Assert.Equal(expectedSop.UID, sopInstanceUId);
        }

        [Fact(DisplayName = "TryGetString - missing DICOM tag")]
        public void TryGetString_MissingDicomTag()
        {
            var dicomFile = new DicomFile();
            var expectedSop = DicomUIDGenerator.GenerateDerivedFromUUID();
            dicomFile.Dataset.Add(DicomTag.SOPInstanceUID, expectedSop);
            dicomFile.FileMetaInfo.TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
            dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID = DicomUIDGenerator.GenerateDerivedFromUUID();
            dicomFile.FileMetaInfo.MediaStorageSOPClassUID = DicomUIDGenerator.GenerateDerivedFromUUID();

            var dicomToolkit = new DicomToolkit();
            Assert.False(dicomToolkit.TryGetString(dicomFile, DicomTag.StudyInstanceUID, out var sopInstanceUId));
            Assert.Equal(string.Empty, sopInstanceUId);
        }

        [Fact(DisplayName = "TryGetString - a valid DICOM file")]
        public void TryGetString_ValidFile()
        {
            var dicomFile = new DicomFile();
            var expectedSop = DicomUIDGenerator.GenerateDerivedFromUUID();
            dicomFile.Dataset.Add(DicomTag.SOPInstanceUID, expectedSop);
            dicomFile.FileMetaInfo.TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
            dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID = DicomUIDGenerator.GenerateDerivedFromUUID();
            dicomFile.FileMetaInfo.MediaStorageSOPClassUID = DicomUIDGenerator.GenerateDerivedFromUUID();

            var dicomToolkit = new DicomToolkit();
            Assert.True(dicomToolkit.TryGetString(dicomFile, DicomTag.SOPInstanceUID, out var sopInstanceUId));
            Assert.Equal(expectedSop.UID, sopInstanceUId);
        }

        [Fact(DisplayName = "Save - a valid DICOM file")]
        public void Save_ValidFile()
        {
            var filename = Path.GetTempFileName();
            var dicomFile = new DicomFile();
            var expectedSop = DicomUIDGenerator.GenerateDerivedFromUUID();
            dicomFile.Dataset.Add(DicomTag.SOPInstanceUID, expectedSop);
            dicomFile.FileMetaInfo.TransferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
            dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID = DicomUIDGenerator.GenerateDerivedFromUUID();
            dicomFile.FileMetaInfo.MediaStorageSOPClassUID = DicomUIDGenerator.GenerateDerivedFromUUID();

            var dicomToolkit = new DicomToolkit();
            dicomToolkit.Save(dicomFile, filename);

            var savedFile = dicomToolkit.Open(filename);

            Assert.NotNull(savedFile);

            Assert.Equal(expectedSop.UID, savedFile.Dataset.GetSingleValue<string>(DicomTag.SOPInstanceUID));
        }
    }
}