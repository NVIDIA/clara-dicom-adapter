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
using ConsoleAppFramework;
using Dicom;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.Dicom.DicomWeb.Client.Common;
using Nvidia.Clara.DicomAdapter.DicomWeb.Client;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Nvidia.Clara.Dicom.DicomWeb.Client.CLI
{
    [Command("wado", "Use wado to retrieve DICOM studies, series, instances, etc...")]
    public class Wado : ConsoleAppBase
    {
        private readonly ILogger<Wado> _logger;

        public Wado(ILogger<Wado> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Command("study", "Retrieves instances within a study")]
        public async Task Study(
            [Option("r", "Uniform Resource Locator (URL) of the DICOMweb service")] string rootUrl,
            [Option("u", "username for authentication with the DICOMweb service")] string username,
            [Option("p", "password for authentication with the DICOMweb service")] string password,
            [Option("s", "unique study identifier; Study Instance UID")] string studyInstanceUid,
            [Option("f", "output format: json, dicom")] OutputFormat format = OutputFormat.Dicom,
            [Option("o", "output directory, default: current directory(.)", DefaultValue = ".")] string outputDir = ".",
            [Option("t", "transfer syntaxes, separated by comma")] string transferSyntaxes = "1.2.840.10008.1.2.1"
            )
        {
            Uri rootUri;
            List<DicomTransferSyntax> dicomTransferSyntaxes;
            ValidateOptions(rootUrl, transferSyntaxes, out rootUri, out dicomTransferSyntaxes);
            ValidateOutputDirectory(ref outputDir);

            var client = new DicomWebClient(rootUri, Utils.GenerateFromUsernamePassword(username, password));
            _logger.LogInformation($"Retrieving study {studyInstanceUid}...");
            if (format == OutputFormat.Dicom)
            {
                await SaveFiles(outputDir, client.Wado.Retrieve(studyInstanceUid, transferSyntaxes: dicomTransferSyntaxes.ToArray()));
            }
            else
            {
                await SaveJson(outputDir, client.Wado.RetrieveMetadata<string>(studyInstanceUid));
            }
        }

        [Command("series", "Retrieves instances within a series")]
        public async Task Series(
            [Option("r", "Uniform Resource Locator (URL) of the DICOMweb service")] string rootUrl,
            [Option("u", "username for authentication with the DICOMweb service")] string username,
            [Option("p", "password for authentication with the DICOMweb service")] string password,
            [Option("s", "unique study identifier; Study Instance UID")] string studyInstanceUid,
            [Option("e", "unique series identifier; Series Instance UID")] string seriesInstanceUid,
            [Option("f", "output format: json, dicom")] OutputFormat format = OutputFormat.Dicom,
            [Option("o", "output directory, default: current directory(.)", DefaultValue = ".")] string outputDir = ".",
            [Option("t", "transfer syntaxes, separated by comma")] string transferSyntaxes = "1.2.840.10008.1.2.1"
            )
        {
            Uri rootUri;
            List<DicomTransferSyntax> dicomTransferSyntaxes;
            ValidateOptions(rootUrl, transferSyntaxes, out rootUri, out dicomTransferSyntaxes);
            ValidateOutputDirectory(ref outputDir);

            var client = new DicomWebClient(rootUri, Utils.GenerateFromUsernamePassword(username, password));
            _logger.LogInformation($"Retrieving series  {seriesInstanceUid} from");
            _logger.LogInformation($"\tStudy Instance UID: {studyInstanceUid}");
            if (format == OutputFormat.Dicom)
            {
                await SaveFiles(outputDir, client.Wado.Retrieve(studyInstanceUid, seriesInstanceUid, transferSyntaxes: dicomTransferSyntaxes.ToArray()));
            }
            else
            {
                await SaveJson(outputDir, client.Wado.RetrieveMetadata<string>(studyInstanceUid, seriesInstanceUid));
            }
        }

        [Command("instance", "Retrieves an instance")]
        public async Task Instance(
            [Option("r", "Uniform Resource Locator (URL) of the DICOMweb service")] string rootUrl,
            [Option("u", "username for authentication with the DICOMweb service")] string username,
            [Option("p", "password for authentication with the DICOMweb service")] string password,
            [Option("s", "unique study identifier; Study Instance UID")] string studyInstanceUid,
            [Option("e", "unique series identifier; Series Instance UID")] string seriesInstanceUid,
            [Option("i", "unique instance identifier; SOP Instance UID")] string sopInstanceUid,
            [Option("f", "output format: json, dicom")] OutputFormat format = OutputFormat.Dicom,
            [Option("o", "output directory, default: current directory(.)", DefaultValue = ".")] string outputDir = ".",
            [Option("t", "transfer syntaxes, separated by comma")] string transferSyntaxes = "1.2.840.10008.1.2.1"
            )
        {
            Uri rootUri;
            List<DicomTransferSyntax> dicomTransferSyntaxes;
            ValidateOptions(rootUrl, transferSyntaxes, out rootUri, out dicomTransferSyntaxes);
            ValidateOutputDirectory(ref outputDir);

            var client = new DicomWebClient(rootUri, Utils.GenerateFromUsernamePassword(username, password));
            _logger.LogInformation($"Retrieving instance {sopInstanceUid} from");
            _logger.LogInformation($"\tStudy Instance UID: {studyInstanceUid}");
            _logger.LogInformation($"\tSeries Instance UID: {seriesInstanceUid}");

            if (format == OutputFormat.Dicom)
            {
                var file = await client.Wado.Retrieve(studyInstanceUid, seriesInstanceUid, sopInstanceUid, transferSyntaxes: dicomTransferSyntaxes.ToArray());
                await Utils.SaveFiles(_logger, outputDir, file);
            }
            else
            {
                var json = await client.Wado.RetrieveMetadata<string>(studyInstanceUid, seriesInstanceUid, sopInstanceUid);
                await Utils.SaveJson(_logger, outputDir, json);
            }
        }

        [Command("bulk", "Retrieves bulkdata of an instance")]
        public async Task Bulk(
            [Option("r", "Uniform Resource Locator (URL) of the DICOMweb service")] string rootUrl,
            [Option("u", "username for authentication with the DICOMweb service")] string username,
            [Option("p", "password for authentication with the DICOMweb service")] string password,
            [Option("s", "unique study identifier; Study Instance UID")] string studyInstanceUid,
            [Option("e", "unique series identifier; Series Instance UID")] string seriesInstanceUid,
            [Option("i", "unique instance identifier; SOP Instance UID")] string sopInstanceUid,
            [Option("g", "DICOM tag containing the bulkdata")] string tag,
            [Option("o", "output filename", DefaultValue = ".")] string filename = "bulkdata.bin",
            [Option("t", "transfer syntaxes, separated by comma")] string transferSyntaxes = "1.2.840.10008.1.2.1"
            )
        {
            Uri rootUri;
            List<DicomTransferSyntax> dicomTransferSyntaxes;
            ValidateOptions(rootUrl, transferSyntaxes, out rootUri, out dicomTransferSyntaxes);
            ValidateOutputFilename(ref filename);
            var dicomTag = DicomTag.Parse(tag);

            var client = new DicomWebClient(rootUri, Utils.GenerateFromUsernamePassword(username, password));
            _logger.LogInformation($"Retrieving {dicomTag} from");
            _logger.LogInformation($"\tStudy Instance UID: {studyInstanceUid}");
            _logger.LogInformation($"\tSeries Instance UID: {seriesInstanceUid}");
            _logger.LogInformation($"\tSOP Instance UID: {sopInstanceUid}");
            var data = await client.Wado.Retrieve(studyInstanceUid, seriesInstanceUid, sopInstanceUid, dicomTag, transferSyntaxes: dicomTransferSyntaxes.ToArray());

            _logger.LogInformation($"Saving data to {filename}....");
            await File.WriteAllBytesAsync(filename, data);
        }

        private async Task SaveJson(string outputDir, IAsyncEnumerable<string> enumerable)
        {
            Guard.Against.NullOrWhiteSpace(outputDir, nameof(outputDir));
            Guard.Against.Null(enumerable, nameof(enumerable));

            await foreach (var item in enumerable)
            {
                await Utils.SaveJson(_logger, outputDir, item);
            }
        }

        private async Task SaveFiles(string outputDir, IAsyncEnumerable<DicomFile> enumerable)
        {
            Guard.Against.NullOrWhiteSpace(outputDir, nameof(outputDir));
            Guard.Against.Null(enumerable, nameof(enumerable));

            var count = 0;
            await foreach (var file in enumerable)
            {
                await Utils.SaveFiles(_logger, outputDir, file);
                count++;
            }
            _logger.LogInformation($"Successfully saved {count} files.");
        }

        private void ValidateOutputFilename(ref string filename)
        {
            Guard.Against.NullOrWhiteSpace(filename, nameof(filename));

            try
            {
                filename = Path.GetFullPath(filename);
            }
            catch
            {
            }
            Utils.CheckAndConfirmOverwriteOutputFilename(_logger, filename);
        }

        private void ValidateOutputDirectory(ref string outputDir)
        {
            Guard.Against.NullOrWhiteSpace(outputDir, nameof(outputDir));

            if (outputDir == ".")
            {
                outputDir = Environment.CurrentDirectory;
            }
            else
            {
                Utils.CheckAndConfirmOverwriteOutput(_logger, outputDir);
            }
        }

        private void ValidateOptions(string rootUrl, string transferSyntaxes, out Uri rootUri, out List<DicomTransferSyntax> dicomTransferSyntaxes)
        {
            Guard.Against.NullOrWhiteSpace(rootUrl, nameof(rootUrl));
            Guard.Against.NullOrWhiteSpace(transferSyntaxes, nameof(transferSyntaxes));

            _logger.LogInformation("Checking arguments...");
            rootUri = new Uri(rootUrl);
            rootUri = rootUri.EnsureUriEndsWithSlash();

            dicomTransferSyntaxes = new List<DicomTransferSyntax>();
            var transferSyntaxArray = transferSyntaxes.Split(',');
            foreach (var uid in transferSyntaxArray)
            {
                var uidData = DicomUID.Parse(uid, type: DicomUidType.TransferSyntax);
                if (uidData.Name.Equals("Unknown"))
                {
                    throw new ArgumentException($"Invalid transfer syntax: {uid}");
                }
                dicomTransferSyntaxes.Add(DicomTransferSyntax.Parse(uidData.UID));
            }
        }
    }
}