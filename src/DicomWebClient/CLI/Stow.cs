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
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using Nvidia.Clara.Dicom.DicomWeb.Client.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Nvidia.Clara.Dicom.DicomWeb.Client.CLI
{
    [Command("stow", "Use stow to store DICOM instances to a remote DICOMweb server.")]
    public class Stow : ConsoleAppBase
    {
        private readonly IDicomWebClient _dicomWebClient;
        private readonly ILogger<Stow> _logger;

        public Stow(IDicomWebClient dicomWebClient, ILogger<Stow> logger)
        {
            _dicomWebClient = dicomWebClient ?? throw new ArgumentNullException(nameof(dicomWebClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Command("store", "Retrieves instances within a study")]
        public async Task Store(
            [Option("r", "Uniform Resource Locator (URL) of the DICOMweb service")] string rootUrl,
            [Option("u", "username for authentication with the DICOMweb service")] string username,
            [Option("p", "password for authentication with the DICOMweb service")] string password,
            [Option("i", "DICOM file or directory containing multiple DICOM files")] string input,
            [Option("o", "Output filename")] string outputFilename = "",
            [Option("s", "unique study identifier; Study Instance UID")] string studyInstanceUid = "",
            [Option("t", "Time to wait before the request times out, in minutes.  Default 5 minutes.")] int timeout = 5
            )
        {
            Uri rootUri;
            ValidateOptions(rootUrl, out rootUri);
            var files = ScanFiles(input);
            _logger.LogInformation($"Storing {files.Count} instances...");

            _dicomWebClient.ConfigureServiceUris(rootUri);
            _dicomWebClient.ConfigureAuthentication(Utils.GenerateFromUsernamePassword(username, password));

            DicomWebResponse<string> response = null;
            try
            {
                response = await _dicomWebClient.Stow.Store(studyInstanceUid, files);
            }
            catch (ResponseDecodeException ex)
            {
                _logger.LogError($"Error decoding response: {ex.Message}");
                return;
            }

            try
            {
                if (string.IsNullOrWhiteSpace(outputFilename))
                {
                    outputFilename = $"{DateTime.Now.ToString("yyyyMMdd-hhmmss-fffff")}-.json";
                }
                await Utils.SaveJson(_logger, outputFilename, response.Result);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving results: {ex.Message}");
            }
        }

        private IList<DicomFile> ScanFiles(string input)
        {
            var attr = File.GetAttributes(input);
            var dicomFiles = new List<DicomFile>();
            if (attr.HasFlag(FileAttributes.Directory))
            {
                var files = Directory.GetFiles(input, "*", SearchOption.AllDirectories);
                AddValidFiles(dicomFiles, files);
            }
            else
            {
                AddValidFiles(dicomFiles, input);
            }
            return dicomFiles;
        }

        private void AddValidFiles(List<DicomFile> dicomFiles, params string[] files)
        {
            foreach (var file in files)
            {
                if (DicomFile.HasValidHeader(file))
                {
                    try
                    {
                        var dicomFile = DicomFile.Open(file, FileReadOption.ReadAll);
                        dicomFiles.Add(dicomFile);
                        _logger.LogInformation($"\tQueued DICOM instance: {dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID.UID} from {file}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning($"Failed to open {file}: {ex.Message}");
                    }
                }
                else
                {
                    _logger.LogWarning($"{file} is not a valid DICOM part-10 file.");
                }
            }
        }

        private void ValidateOptions(string rootUrl, out Uri rootUri)
        {
            Guard.Against.NullOrWhiteSpace(rootUrl, nameof(rootUrl));

            _logger.LogInformation("Checking arguments...");
            rootUri = new Uri(rootUrl);
            rootUri = rootUri.EnsureUriEndsWithSlash();
        }
    }
}