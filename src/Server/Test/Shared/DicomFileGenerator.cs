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
using Dicom.Imaging.Codec;
using System;
using System.Collections.Generic;
using System.IO;

namespace Nvidia.Clara.DicomAdapter.Test.Shared
{
    public class DicomFileGenerator
    {
        private DicomDataset baseDataset;

        public DicomFileGenerator()
        {
            baseDataset = new DicomDataset();
            SetPatient().SetStudyDateTime(DateTime.Now);
        }

        public DicomFileGenerator SetPatient(string patientId = "")
        {
            baseDataset.AddOrUpdate(DicomTag.PatientID, patientId);
            baseDataset.AddOrUpdate(DicomTag.PatientName, patientId);
            baseDataset.AddOrUpdate(DicomTag.AccessionNumber, patientId.Substring(0, Math.Min(patientId.Length, 16)));
            return this;
        }

        public DicomFileGenerator SetStudyDateTime(DateTime datetime)
        {
            baseDataset.AddOrUpdate(DicomTag.StudyDate, datetime);
            baseDataset.AddOrUpdate(DicomTag.StudyTime, datetime);
            return this;
        }

        public DicomFileGenerator GenerateNewStudy()
        {
            baseDataset.AddOrUpdate(DicomTag.StudyInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            return this;
        }

        public DicomFileGenerator GenerateNewSeries()
        {
            baseDataset.AddOrUpdate(DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            baseDataset.AddOrUpdate(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            return this;
        }

        public IList<TestInstanceInfo> Save(string destDir, string filenamePrefix, DicomTransferSyntax transferSyntax, int instancesToGenerate = 1, string sopClassUid = "1.2.840.10008.5.1.4.1.1.11.1")
        {
            Console.Write("Generating test files.");
            if (!Directory.Exists(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            var dataset = new DicomDataset();
            baseDataset.CopyTo(dataset);
            dataset.AddOrUpdate(DicomTag.SOPClassUID, sopClassUid);

            var dicomFile = new DicomFile(dataset);
            if (transferSyntax != dicomFile.Dataset.InternalTransferSyntax)
            {
                var transcoder = new DicomTranscoder(dicomFile.Dataset.InternalTransferSyntax, transferSyntax, outputCodecParams: new DicomJpegParams() { Quality = 100, ConvertColorspaceToRGB = true });
                dicomFile = transcoder.Transcode(dicomFile);
            }

            var instancesCreated = new List<TestInstanceInfo>();
            for (int i = 0; i < instancesToGenerate; i++)
            {
                Console.Write(".");
                var sopInstanceUid = DicomUIDGenerator.GenerateDerivedFromUUID();
                var filePath = Path.Combine(destDir, $"{filenamePrefix}-{i:00000}.dcm");
                dicomFile.Dataset.AddOrUpdate(DicomTag.SOPInstanceUID, sopInstanceUid);
                dicomFile.FileMetaInfo.AddOrUpdate(DicomTag.MediaStorageSOPInstanceUID, sopInstanceUid);
                dicomFile.FileMetaInfo.AddOrUpdate(DicomTag.MediaStorageSOPClassUID, sopClassUid);

                dicomFile.Clone().Save(filePath);

                instancesCreated.Add(new TestInstanceInfo
                {
                    PatientId = dicomFile.Dataset.GetSingleValue<string>(DicomTag.PatientID),
                    StudyInstanceUid = dicomFile.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID),
                    SeriesInstanceUid = dicomFile.Dataset.GetSingleValue<string>(DicomTag.SeriesInstanceUID),
                    SopInstanceUid = dicomFile.Dataset.GetSingleValue<string>(DicomTag.SOPInstanceUID),
                    FilePath = filePath
                });
            }
            Console.WriteLine(".");
            return instancesCreated;
        }
    }

    public class TestInstanceInfo
    {
        public string PatientId { get; set; }
        public string StudyInstanceUid { get; set; }
        public string SeriesInstanceUid { get; set; }
        public string SopInstanceUid { get; set; }
        public string FilePath { get; set; }

        public string FileDirectoryPath
        {
            get
            {
                return Path.GetDirectoryName(FilePath);
            }
        }

        public string GetPathToPatient(string root)
        {
            return Path.Combine(root, PatientId);
        }

        public string GetPathToStudy(string root)
        {
            return Path.Combine(GetPathToPatient(root), StudyInstanceUid);
        }

        public string GetPathToInstance(string root)
        {
            var path = GetPathToStudy(root);
            path = Path.Combine(path, SeriesInstanceUid);
            path = Path.Combine(path, SopInstanceUid + ".dcm");
            return path;
        }
    }
}