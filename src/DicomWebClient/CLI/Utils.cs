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
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using System;
using System.IO;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;

namespace Nvidia.Clara.Dicom.DicomWeb.Client.CLI
{
    internal static class Utils
    {
        public static void CheckAndConfirmOverwriteOutputFilename<T>(ILogger<T> logger, string filename)
        {
            Guard.Against.Null(logger, nameof(logger));
            Guard.Against.NullOrWhiteSpace(filename, nameof(filename));

            if (File.Exists(filename))
            {
                ConsoleKeyInfo option;
                do
                {
                    logger.LogWarning($"Output filename {filename} already exists, any data will be overwritten. Do you wish to continue? [Y/n]");
                    option = Console.ReadKey();
                } while (option.Key != ConsoleKey.Y && option.Key != ConsoleKey.N && option.Key != ConsoleKey.Enter);

                if (option.Key == ConsoleKey.N)
                {
                    throw new OperationCanceledException();
                }
            }
        }

        public static void CheckAndConfirmOverwriteOutput<T>(ILogger<T> logger, string outputDir)
        {
            Guard.Against.Null(logger, nameof(logger));
            Guard.Against.NullOrWhiteSpace(outputDir, nameof(outputDir));

            if (Directory.Exists(outputDir))
            {
                ConsoleKeyInfo option;
                do
                {
                    logger.LogWarning($"Output path {outputDir} already exists, any data will be overwritten. Do you wish to continue? [Y/n]");
                    option = Console.ReadKey();
                } while (option.Key != ConsoleKey.Y && option.Key != ConsoleKey.N && option.Key != ConsoleKey.Enter);

                if (option.Key == ConsoleKey.N)
                {
                    throw new OperationCanceledException();
                }
            }
            else
            {
                logger.LogInformation($"Creating output directory {outputDir}...");
                Directory.CreateDirectory(outputDir);
            }
        }

        public static AuthenticationHeaderValue GenerateFromUsernamePassword(string username, string password)
        {
            Guard.Against.NullOrWhiteSpace(username, nameof(username));
            Guard.Against.NullOrWhiteSpace(password, nameof(password));

            var authToken = Encoding.ASCII.GetBytes($"{username}:{password}");
            return new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authToken));
        }

        public static async Task SaveFiles<T>(ILogger<T> logger, string outputDirectory, DicomFile dicomFile)
        {
            var path = Path.Combine(outputDirectory, dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID.UID + ".dcm");
            await SaveFiles(logger, dicomFile, path).ConfigureAwait(false);
        }

        public static async Task SaveFiles<T>(ILogger<T> logger, DicomFile dicomFile, string filename)
        {
            Guard.Against.Null(logger, nameof(logger));
            Guard.Against.Null(dicomFile, nameof(dicomFile));
            Guard.Against.NullOrWhiteSpace(filename, nameof(filename));

            logger.LogInformation($"Saving {filename}...");
            await dicomFile.SaveAsync(filename).ConfigureAwait(false);
        }

        internal static async Task SaveJson(ILogger logger, string outputDir, string item, DicomTag filenameSourceTag)
        {
            Guard.Against.Null(logger, nameof(logger));
            Guard.Against.NullOrWhiteSpace(outputDir, nameof(outputDir));
            Guard.Against.NullOrWhiteSpace(item, nameof(item));

            var token = JToken.Parse(item);
            var filename = string.Empty;
            var value = GetTagValueFromJson(token, filenameSourceTag);
            if (!string.IsNullOrWhiteSpace(value))
            {
                filename = $"{value}.txt";
            }
            else
            {
                filename = $"unknown-{DateTime.Now.ToFileTime()}";
            }
            var path = Path.Combine(outputDir, filename);
            logger.LogInformation($"Saving JSON {path}");
            await File.WriteAllTextAsync(path, token.ToString(Newtonsoft.Json.Formatting.Indented), Encoding.UTF8);
        }

        internal static async Task SaveJson(ILogger logger, string outputFilename, string text)
        {
            var token = JToken.Parse(text);
            logger.LogInformation($"Saving JSON {outputFilename}...");
            await File.WriteAllTextAsync(outputFilename, token.ToString(Newtonsoft.Json.Formatting.Indented), Encoding.UTF8).ConfigureAwait(false);
        }

        private static string GetTagValueFromJson(JToken token, DicomTag dicomTag, string defaultValue = "unknown")
        {
            Guard.Against.Null(token, nameof(token));
            Guard.Against.Null(dicomTag, nameof(dicomTag));

            var tag = $"{dicomTag.Group:X4}{dicomTag.Element:X4}";

            if (token.HasValues && token[tag].HasValues)
            {
                return token[tag]?["Value"]?.First.ToString();
            }

            return defaultValue;
        }
    }
}