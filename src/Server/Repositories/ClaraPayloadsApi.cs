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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.Common;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.Platform;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Threading.Tasks;
using static System.StringComparer;

namespace Nvidia.Clara.DicomAdapter.Server.Repositories
{
    public class ClaraPayloadsApi : IPayloads
    {
        private readonly ILogger<ClaraPayloadsApi> _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IPayloadsClient _payloadsClient;

        public ClaraPayloadsApi(
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration,
            ILogger<ClaraPayloadsApi> logger) : this(
                InitializePayloadsClient(dicomAdapterConfiguration),
                logger,
            new FileSystem())
        {
            logger.Log(LogLevel.Information, "ClaraPayloadsApi initialized with {0}", dicomAdapterConfiguration.Value.Services.PlatformEndpoint);
        }

        public ClaraPayloadsApi(
            IPayloadsClient payloadsClient,
            ILogger<ClaraPayloadsApi> iLogger,
            IFileSystem iFileSystem)
        {
            _payloadsClient = payloadsClient ?? throw new ArgumentNullException(nameof(payloadsClient));
            _logger = iLogger ?? throw new ArgumentNullException(nameof(iLogger));
            _fileSystem = iFileSystem ?? throw new ArgumentNullException(nameof(iFileSystem));
        }

        public async Task<PayloadFile> Download(string payload, string name)
        {
            return await Policy<PayloadFile>
                .Handle<Exception>()
                .WaitAndRetryAsync(3, (r) => TimeSpan.FromSeconds(r * 1.5f), (data, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, "Failed to download file {0} from Payloads Service {1}: {2}", name, payload, data.Exception);
                    })
                .ExecuteAsync(async () =>
                    {
                        if (!PayloadId.TryParse(payload, out PayloadId payloadId))
                        {
                            throw new ApplicationException($"Invalid Payload ID received: {payload}");
                        }

                        PayloadFile file = new PayloadFile();
                        using (var ms = new MemoryStream())
                        {
                            var details = await _payloadsClient.DownloadFrom(payloadId, name, ms);
                            file.Data = ms.ToArray();
                            file.Name = details.Name;
                        }

                        _logger.Log(LogLevel.Information, "File {0} successfully downloaded from {1}.", name, payloadId);
                        return file;
                    }).ConfigureAwait(false);
        }

        public async Task Upload(string payload, string basePath, IEnumerable<string> filePaths)
        {
            if (payload is null)
                throw new ArgumentNullException(nameof(payload));
            if (filePaths is null)
                throw new ArgumentNullException(nameof(filePaths));

            if (!PayloadId.TryParse(payload, out var payloadId))
                throw new ArgumentException($"Invalid Payload ID received: {{{payload}}}.", nameof(payload));

            var queue = new Queue<string>(filePaths);
            var completedFiles = new List<PayloadFileDetails>();
            basePath = EnsureBasePathEndsWithSlash(basePath);

            await Policy.Handle<Exception>()
                        .WaitAndRetryAsync(3,
                                           retryAttempt
                                             => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                                           (exception, retryCount, context)
                                             => _logger.Log(LogLevel.Error, "Exception while uploading file(s) to {{{0}}}: {exception}.", payload, exception))
                        .ExecuteAsync(async () =>
                        {
                            var list = new List<(uint mode, string name, Stream stream)>();
                            var temp = new List<string>();

                            try
                            {
                                while (queue.Count > 0)
                                {
                                    var filePath = queue.Dequeue();
                                    temp.Add(filePath);
                                    var filename = filePath.Replace(basePath, "");
                                    if (System.Linq.Enumerable.Any(completedFiles, (PayloadFileDetails pfd) => OrdinalIgnoreCase.Equals(filename, pfd.Name)))
                                        continue;
                                    try
                                    {
                                        var stream = _fileSystem.File.OpenRead(filePath);
                                        list.Add((0, filename, stream));
                                        _logger.Log(LogLevel.Debug, "Ready to upload file \"{0}\" to PayloadId {1}.", filename, payloadId);
                                    }
                                    catch (System.Exception ex)
                                    {
                                        _logger.Log(LogLevel.Error, "Failed to open/read file {0}: {1}", filename, ex);
                                        throw;
                                    }
                                }

                                try
                                {
                                    var uploadedFiles = await _payloadsClient.UploadTo(payloadId, list);

                                    completedFiles.AddRange(uploadedFiles);
                                    _logger.Log(LogLevel.Information, "{0} files uploaded to PayloadId {1}", completedFiles.Count, payloadId);
                                }
                                catch (PayloadUploadFailedException ex)
                                {
                                    completedFiles.AddRange(ex.CompletedFiles);
                                    temp.ForEach(file => queue.Enqueue(file));

                                    if (completedFiles.Count != filePaths.Count())
                                        throw;
                                    _logger.Log(LogLevel.Information, "{0} files uploaded to PayloadId {1}", completedFiles.Count, payloadId);
                                }
                                catch
                                {
                                    temp.ForEach(file => queue.Enqueue(file));
                                    throw;
                                }
                            }
                            finally
                            {
                                for (var i = 0; i < list.Count; i += 1)
                                {
                                    try
                                    {
                                        list[i].stream?.Dispose();
                                    }
                                    catch (Exception exception)
                                    {
                                        _logger.Log(LogLevel.Error, exception, $"Failed to dispose \"{list[i].name}\" stream.");
                                    }
                                }
                            }
                        }).ConfigureAwait(false);
        }

        private string EnsureBasePathEndsWithSlash(string basePath)
        {
            if (!basePath.EndsWith(_fileSystem.Path.DirectorySeparatorChar))
                basePath += _fileSystem.Path.DirectorySeparatorChar;

            return basePath;
        }

        private static IPayloadsClient InitializePayloadsClient(IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
        {
            var serviceContext = ServiceContext.Create();
            BaseClient.InitializeServiceContext(serviceContext);
            return new PayloadsClient(serviceContext, dicomAdapterConfiguration.Value.Services.PlatformEndpoint);
        }
    }
}