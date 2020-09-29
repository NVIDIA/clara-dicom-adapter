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
using System.Collections.Generic;
using System.IO;
using Dicom;

namespace Nvidia.Clara.DicomAdapter.Test.Shared
{
    public class ScuTestFileSetsFixture : TestFileSetsFixture
    {
        public static readonly string ScpDir = Path.GetFullPath("./storescp");

        public ScuTestFileSetsFixture()
        {
            CreateScpDir();
            GenerateTestFile_MultipleTransferSyntaxes(
                "1-scu-with-multiple-transferSyntaxes",
                1,
                new[]
                {
                    DicomTransferSyntax.ExplicitVRLittleEndian,
                    DicomTransferSyntax.ExplicitVRBigEndian,
                    DicomTransferSyntax.ImplicitVRLittleEndian,
                    DicomTransferSyntax.JPEG2000Lossless,
                    DicomTransferSyntax.JPEG2000Lossy,
                    DicomTransferSyntax.RLELossless,
                    DicomTransferSyntax.JPEGProcess1,
                    DicomTransferSyntax.JPEGProcess2_4
                });
            GenerateTestFile_MultipleTransferSyntaxes(
                "2-scu-that-would-fail-and-retry",
                1,
                DicomTransferSyntax.JPEGProcess1);
            GenerateTestFile_MultipleTransferSyntaxes(
                "3-scu-that-would-fail-and-retry",
                1,
                DicomTransferSyntax.JPEGProcess2_4);
            GenerateTestFile_MultipleTransferSyntaxes(
                "4-scu-that-would-fail-and-retry",
                1,
                DicomTransferSyntax.RLELossless);
            GenerateTestFile_MultipleTransferSyntaxes(
                "5-scu-repeat-test",
                1,
                DicomTransferSyntax.ExplicitVRLittleEndian);
        }

        private void CreateScpDir()
        {
            if (Directory.Exists(ScpDir))
            {
                Directory.Delete(ScpDir, true);
            }

            Directory.CreateDirectory(ScpDir);
        }
    }

    public class ScpTestFileSetsFixture : TestFileSetsFixture
    {
        public ScpTestFileSetsFixture()
        {
            GenerateTestFiles_SingleStudy_SingleSeries_WithTransferSyntax_WithNInstances("1-scp-explicitVrLittleEndian", DicomTransferSyntax.ExplicitVRLittleEndian, 3);

            GenerateMultiplePatientStudies("2-2-patients-2-studies", 2, 2, 2, 100);

            GenerateSingleStudyWithMultiSeriesInSeparateFolders("3-single-study-multi-series", 5, 100);
        }
    }

    public class JobProcessorTestFileSetsFixture : TestFileSetsFixture
    {
        public JobProcessorTestFileSetsFixture()
        {
            GenerateTestFiles_SingleStudy_SingleSeries_WithTransferSyntax_WithNInstances("Family^Given^Middle^Prefix^Suffix", DicomTransferSyntax.ExplicitVRLittleEndian, 1);
        }
    }

    public abstract class TestFileSetsFixture : IDisposable
    {
        public Dictionary<string, List<TestInstanceInfo>> FileSetPaths { get; }

        protected DicomFileGenerator dicomFileGenerator;
        internal static readonly string ApplicationEntryDirectory = AppDomain.CurrentDomain.BaseDirectory;

        public TestFileSetsFixture()
        {
            FileSetPaths = new Dictionary<string, List<TestInstanceInfo>>();
            dicomFileGenerator = new DicomFileGenerator();
        }

        protected void GenerateSingleStudyWithMultiSeriesInSeparateFolders(
            string testCaseName,
            int seriesCount,
            int instanceCount)
        {
            var destPath = Path.Combine(ApplicationEntryDirectory, testCaseName);

            var instancesGenerated = new List<TestInstanceInfo>();
            var fileGenerator = dicomFileGenerator
                                .SetPatient($"{testCaseName}")
                                .GenerateNewStudy()
                                .SetStudyDateTime(DateTime.Now);

            for (int series = 0; series < seriesCount; series++)
            {
                instancesGenerated.AddRange(
                    fileGenerator
                        .GenerateNewSeries()
                        .Save(
                            Path.Combine(destPath, series.ToString()), $"{testCaseName}-{series}",
                            DicomTransferSyntax.ExplicitVRLittleEndian,
                            instanceCount)
                );
            }

            FileSetPaths.Add(testCaseName, instancesGenerated);
        }

        protected void GenerateMultiplePatientStudies(
            string testCaseName,
            int patientCount,
            int studyCount,
            int seriesCount,
            int instanceCount)
        {
            var destPath = Path.Combine(ApplicationEntryDirectory, testCaseName);

            var instancesGenerated = new List<TestInstanceInfo>();
            for (int patient = 0; patient < patientCount; patient++)
            {
                var fileGenerator = dicomFileGenerator.SetPatient($"{testCaseName}_P{patient}");
                for (int study = 0; study < studyCount; study++)
                {
                    fileGenerator = fileGenerator
                                        .GenerateNewStudy()
                                        .SetStudyDateTime(DateTime.Now);
                    for (int series = 0; series < seriesCount; series++)
                    {
                        instancesGenerated.AddRange(
                            fileGenerator
                                .GenerateNewSeries()
                                .Save(
                                    Path.Combine(destPath, $"P{patient}", $"S{study}"), $"{testCaseName}-{patient}-{study}-{series}",
                                    DicomTransferSyntax.ExplicitVRLittleEndian,
                                    instanceCount)
                        );
                    }
                }
            }

            FileSetPaths.Add(testCaseName, instancesGenerated);
        }

        protected void GenerateTestFiles_MultiPatientStudySeriesInstance(
            string testCaseName,
            int patientCount,
            int studyCount,
            int seriesCount,
            int instanceCount)
        {
            var destPath = Path.Combine(ApplicationEntryDirectory, testCaseName);

            var instancesGenerated = new List<TestInstanceInfo>();
            for (int patient = 0; patient < patientCount; patient++)
            {
                var fileGenerator = dicomFileGenerator.SetPatient($"testCaseName_P{patient}");
                for (int study = 0; study < studyCount; study++)
                {
                    fileGenerator = fileGenerator
                                        .GenerateNewStudy()
                                        .SetStudyDateTime(DateTime.Now);
                    for (int series = 0; series < seriesCount; series++)
                    {
                        instancesGenerated.AddRange(
                            fileGenerator
                                .GenerateNewSeries()
                                .Save(
                                    destPath, $"{testCaseName}-{patient}-{study}-{series}",
                                    DicomTransferSyntax.ExplicitVRLittleEndian,
                                    instanceCount)
                        );
                    }
                }
            }

            FileSetPaths.Add(testCaseName, instancesGenerated);
        }

        protected void GenerateTestFile_MultipleTransferSyntaxes(string testCaseName, int instanceCount, params DicomTransferSyntax[] tranxferSyntaxes)
        {
            var instancesGenerated = new List<TestInstanceInfo>();
            foreach (var transferSyntax in tranxferSyntaxes)
            {
                var destPath = Path.Combine(ApplicationEntryDirectory, testCaseName);
                destPath = Path.Combine(destPath, transferSyntax.ToString());
                instancesGenerated.AddRange(dicomFileGenerator.SetPatient(testCaseName)
                    .GenerateNewStudy()
                    .SetStudyDateTime(DateTime.Now)
                    .GenerateNewSeries()
                    .Save(destPath, $"{testCaseName}-{transferSyntax}", transferSyntax, instanceCount));
            }
            FileSetPaths.Add(testCaseName, new List<TestInstanceInfo>(instancesGenerated));
        }

        protected void GenerateTestFiles_SingleStudy_WithMultipleSopClasses(string testCaseName)
        {
            var transferSyntax = DicomTransferSyntax.ExplicitVRLittleEndian;
            var destPath = Path.Combine(ApplicationEntryDirectory, testCaseName);
            var instancesGenerated = dicomFileGenerator.SetPatient(testCaseName)
                .GenerateNewStudy()
                .SetStudyDateTime(DateTime.Now)
                .GenerateNewSeries()
                .Save(destPath, testCaseName, transferSyntax, 1);

            dicomFileGenerator.Save(destPath, testCaseName + "1", transferSyntax, 1, "1.2.840.10008.5.1.4.1.1.481.1");
            dicomFileGenerator.Save(destPath, testCaseName + "2", transferSyntax, 1, "1.2.840.10008.5.1.4.1.1.481.2");
            dicomFileGenerator.Save(destPath, testCaseName + "3", transferSyntax, 1, "1.2.840.10008.5.1.4.1.1.481.3");
            dicomFileGenerator.Save(destPath, testCaseName + "4", transferSyntax, 1, "1.2.840.10008.5.1.4.1.1.481.4");
            dicomFileGenerator.Save(destPath, testCaseName + "5", transferSyntax, 1, "1.2.840.10008.5.1.4.1.1.481.5");
            FileSetPaths.Add(testCaseName, new List<TestInstanceInfo>(instancesGenerated));
        }

        protected void GenerateTestFiles_SingleStudy_SingleSeries_WithTransferSyntax_WithNInstances(
            string testCaseName,
            DicomTransferSyntax transferSyntax,
            int instanceCount)
        {
            var destPath = Path.Combine(ApplicationEntryDirectory, testCaseName);
            var instancesGenerated = dicomFileGenerator.SetPatient(testCaseName)
                .GenerateNewStudy()
                .SetStudyDateTime(DateTime.Now)
                .GenerateNewSeries()
                .Save(destPath, testCaseName, transferSyntax, instanceCount);

            FileSetPaths.Add(testCaseName, new List<TestInstanceInfo>(instancesGenerated));
        }

        public void Dispose()
        {
        }
    }
}
