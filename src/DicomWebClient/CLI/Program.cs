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

using ConsoleAppFramework;
using Dicom;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Nvidia.Clara.Dicom.DicomWeb.CLI
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // target T as ConsoleAppBase.
            await Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    // Replacing default console logger to SimpleConsoleLogger.
                    logging.ReplaceToSimpleConsole();

                    // Configure MinimumLogLevel(CreaterDefaultBuilder's default is Warning).
                    logging.SetMinimumLevel(LogLevel.Trace);
                })
                .RunConsoleAppFrameworkAsync(args);
        }

        private static async Task SaveBinaryData(byte[] data, string filename)
        {
            Console.Write($"Saving data to {filename}....");
            var path = Path.Combine(Environment.CurrentDirectory, filename);
            await File.WriteAllBytesAsync(path, data);
            Console.WriteLine("\tDone.");
        }

        private static async Task PrintMetadata(IAsyncEnumerable<DicomDataset> asyncEnumerable)
        {
            var count = 0;
            await foreach (var dataset in asyncEnumerable)
            {
                PrintMetadata(dataset);
            }
            Console.WriteLine("=================================================================");
            Console.WriteLine($"Found {count} instances in query");
        }

        private static void PrintMetadata(DicomDataset dataset)
        {
            Console.WriteLine("=================================================================");
            Console.WriteLine(dataset.GetSingleValueOrDefault(DicomTag.PatientID, "<No PatientID>"));
            Console.WriteLine(dataset.GetSingleValueOrDefault(DicomTag.PatientName, "<No PatientName>"));
            Console.WriteLine(dataset.GetSingleValueOrDefault(DicomTag.StudyInstanceUID, "<No StudyInstanceUID>"));
            Console.WriteLine(dataset.GetSingleValueOrDefault(DicomTag.StudyDescription, "<No StudyDescription>"));
            Console.WriteLine(dataset.GetSingleValueOrDefault(DicomTag.SeriesInstanceUID, "<No SeriesInstanceUID>"));
            Console.WriteLine(dataset.GetSingleValueOrDefault(DicomTag.SeriesDescription, "<No SeriesDescription>"));
            Console.WriteLine(dataset.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, "<No SOPInstanceUID>"));
        }

        private static void PrintSaveFiles(IList<DicomFile> files)
        {
            foreach (var file in files)
            {
                PrintSaveFiles(file);
            }
            Console.WriteLine("=================================================================");
            Console.WriteLine($"Found {files.Count} instances in query.");
        }

        private static void PrintSaveFiles(DicomFile file)
        {
            var path = Path.Combine(Environment.CurrentDirectory, file.FileMetaInfo.MediaStorageSOPInstanceUID.UID + ".dcm");
            file.Save(path);
            Console.WriteLine($"File saved in {path}");
        }
    }
}