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
using System.IO.Abstractions;
using System.Threading;
using Ardalis.GuardClauses;
using Dicom.Network;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Processors;
using Polly;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Scp
{
    /// <summary>
    /// ApplicationEntityHandler handles instances received for the AE Title.
    /// </summary>
    internal class ApplicationEntityHandler : IDisposable
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ApplicationEntityHandler> _logger;
        private readonly IDicomToolkit _dicomToolkit;
        private readonly IFileSystem _fileSystem;
        private readonly IInstanceStoredNotificationService _instanceStoredNotificationService;
        private readonly JobProcessorBase _jobProcessor;
        private readonly CancellationToken _cancellationToken;
        private ILoggerFactory _loggerFactory;

        public ClaraApplicationEntity Configuration { get; }

        public string AeStorageRootFullPath { get; }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceProvider">Instance of IServiceProvide for dependency injection.</param>
        /// <param name="applicationEntity">ClaraApplicationEntity configuration to be used.</param>
        /// <param name="storageRootFullPath">Temporary storage path location</param>
        public ApplicationEntityHandler(IServiceProvider serviceProvider, ClaraApplicationEntity applicationEntity, string storageRootFullPath, CancellationToken cancellationToken)
            : this(serviceProvider, applicationEntity, storageRootFullPath, cancellationToken, new FileSystem())
        {
        }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="serviceProvider">Instance of IServiceProvide for dependency injection.</param>
        /// <param name="applicationEntity">ClaraApplicationEntity configuration to be used.</param>
        /// <param name="storageRootFullPath">Temporary storage path location</param>
        /// <param name="iFileSystem">instance of IFileSystem</param>
        public ApplicationEntityHandler(IServiceProvider serviceProvider, ClaraApplicationEntity applicationEntity, string storageRootFullPath, CancellationToken cancellationToken, IFileSystem iFileSystem)
        {
            Guard.Against.NullOrWhiteSpace(storageRootFullPath, nameof(storageRootFullPath));

            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            Configuration = applicationEntity ?? throw new ArgumentNullException(nameof(applicationEntity));
            _loggerFactory = _serviceProvider.GetService<ILoggerFactory>();

            _logger = _loggerFactory.CreateLogger<ApplicationEntityHandler>();
            _dicomToolkit = _serviceProvider.GetService<IDicomToolkit>();
            _cancellationToken = cancellationToken;
            _fileSystem = iFileSystem ?? throw new ArgumentNullException(nameof(iFileSystem));
            AeStorageRootFullPath = _fileSystem.Path.Combine(storageRootFullPath, applicationEntity.AeTitle.RemoveInvalidPathChars());

            _instanceStoredNotificationService = (IInstanceStoredNotificationService)serviceProvider.GetService(typeof(IInstanceStoredNotificationService)) ?? throw new ArgumentNullException("IInstanceStoredNotificationService service not configured");

            _jobProcessor = typeof(JobProcessorBase).CreateInstance<JobProcessorBase>(serviceProvider, Configuration.Processor, Configuration, _cancellationToken);
            _logger.Log(LogLevel.Information, "Clara AE Title {0} configured with temporary storage location {1}", applicationEntity.AeTitle, AeStorageRootFullPath);

            CleanRootPath();
        }

        /// <summary>
        /// Saves specified <code>InstanceStorage</code> to disk.
        /// </summary>
        /// <param name="request">Instance of <code>DicomCStoreRequest</code> to be stored to disk.</param>
        /// <param name="instanceStorage">Instance of <code>InstanceStorage</code></param>
        public void Save(DicomCStoreRequest request, InstanceStorageInfo instanceStorage)
        {
            Guard.Against.Null(instanceStorage, nameof(instanceStorage));

            if (ShouldBeIgnored(instanceStorage.SopClassUid))
            {
                _logger.Log(LogLevel.Warning, "Instance with SOP Class {0} ignored based on configured AET {1}", instanceStorage.SopClassUid, Configuration.AeTitle);
                return;
            }

            Policy.Handle<Exception>()
                .WaitAndRetry(3,
                (retryAttempt) =>
                {
                    return retryAttempt == 1 ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromMilliseconds(500);
                },
                    (exception, retryCount, context) =>
                {
                    _logger.Log(LogLevel.Error, "Failed to save instance, retry count={retryCount}: {exception}", retryCount, exception);
                })
                .Execute(() =>
                {
                    if (ShouldSaveInstance(instanceStorage))
                    {
                        _logger.Log(LogLevel.Information, "Saving {path}.", instanceStorage.InstanceStorageFullPath);
                        _dicomToolkit.Save(request.File, instanceStorage.InstanceStorageFullPath);
                        _logger.Log(LogLevel.Debug, "Instance saved successfully.");
                        _instanceStoredNotificationService.NewInstanceStored(instanceStorage);
                        _logger.Log(LogLevel.Information, "Instance stored and notified successfully.");
                    }
                });
        }

        /// <summary>
        /// Removes files in the designated storage path.
        /// </summary>
        private void CleanRootPath()
        {
            if (_fileSystem.Directory.Exists(AeStorageRootFullPath))
            {
                _logger.Log(LogLevel.Information, "Existing AE Title storage directory {0} found, deleting...", AeStorageRootFullPath);
                _fileSystem.Directory.Delete(AeStorageRootFullPath, true);
                _logger.Log(LogLevel.Information, "Existing AE Title storage directory {0} deleted.", AeStorageRootFullPath);
                _fileSystem.Directory.CreateDirectoryIfNotExists(AeStorageRootFullPath);
            }
        }

        /// <summary>
        /// Determines if the instance exists and if shall be overwritten.
        /// </summary>
        /// <returns>true if instance should be saved; false otherwise.</returns>
        private bool ShouldSaveInstance(InstanceStorageInfo instanceStorage)
        {
            var shouldSaveInstance = false;
            if (!_fileSystem.File.Exists(instanceStorage.InstanceStorageFullPath))
            {
                shouldSaveInstance = true;
            }
            else if ((_fileSystem.File.Exists(instanceStorage.InstanceStorageFullPath) && Configuration.OverwriteSameInstance))
            {
                _logger.Log(LogLevel.Information, "Overwriting existing instance.");
                shouldSaveInstance = true;
            }
            else
            {
                _logger.Log(LogLevel.Information, "Instance already exists, skipping.");
            }
            return shouldSaveInstance;
        }

        /// <summary>
        /// Creates an instance of ILogger<T>
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <returns></returns>
        public ILogger<T> CreateLogger<T>()
        {
            return _loggerFactory.CreateLogger<T>();
        }

        /// <summary>
        /// Determines if the SOP Class should be ignored based on configured values for the calling AE Title.
        /// </summary>
        /// <param name="sopClassUid">SOP Class UID to be checked.</param>
        /// <returns>true if the SOP class shall be ignored; false otherwise.</returns>
        private bool ShouldBeIgnored(string sopClassUid)
        {
            return Configuration.IgnoredSopClasses.Contains(sopClassUid);
        }
        
        public void Dispose()
        {
            _jobProcessor.Dispose();
        }

    }
}
