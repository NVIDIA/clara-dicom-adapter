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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.Common;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
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
            logger.Log(LogLevel.Information, "ClaraPayloadsApi initialized with {0}", dicomAdapterConfiguration.Value.Services.Platform.Endpoint);
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
            Guard.Against.NullOrWhiteSpace(payload, nameof(payload));
            Guard.Against.NullOrWhiteSpace(name, nameof(name));

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

        public async Task Upload(string payload, string name, string filePath)
        {
            Guard.Against.NullOrWhiteSpace(payload, nameof(payload));
            Guard.Against.NullOrWhiteSpace(name, nameof(name));
            Guard.Against.NullOrWhiteSpace(filePath, nameof(filePath));

            if (!PayloadId.TryParse(payload, out var payloadId))
                throw new ArgumentException($"Invalid Payload ID received: {{{payload}}}.", nameof(payload));

            using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "PayloadId", payload }, { "Name", name }, { "File", filePath } });


            await Policy.Handle<Exception>()
                .WaitAndRetryAsync(3,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, retryCount, context) => _logger.Log(LogLevel.Error, "Exception while uploading file(s) to {{{0}}}: {exception}.", payload, exception))
                .ExecuteAsync(async () =>
                {
                    Stream stream;
                    try
                    {
                        stream = _fileSystem.File.OpenRead(filePath);
                    }
                    catch (System.Exception ex)
                    {
                        _logger.Log(LogLevel.Error, ex, "Error reading file.");
                        throw;
                    }

                    try
                    {
                        await _payloadsClient.UploadTo(payloadId, 0, name, stream);
                        _logger.Log(LogLevel.Debug, "File uploaded sucessfully.");
                    }
                    catch (PayloadUploadFailedException ex)
                    {
                        _logger.Log(LogLevel.Error, ex, "Error uploading file.");
                        throw;
                    }
                    finally
                    {
                        stream?.Dispose();
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
            return new PayloadsClient(serviceContext, dicomAdapterConfiguration.Value.Services.Platform.Endpoint);
        }
    }
}