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
using Dicom.Network;
using k8s;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Config;
using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Scp
{
    public interface IApplicationEntityManager
    {
        IOptions<DicomAdapterConfiguration> Configuration { get; }

        /// <summary>
        /// Handles the C-Store request.
        /// </summary>
        /// <param name="request">Instance of <see cref="Dicom.Network.DicomCStoreRequest" />.</param>
        /// <param name="calledAeTitle">Calling AE Title to be associated with the call.</param>
        /// <param name="associationId">Unique association ID.</param>
        void HandleCStoreRequest(DicomCStoreRequest request, string calledAeTitle, uint associationId);

        /// <summary>
        /// Checks if a Clara AET is configured.
        /// </summary>
        /// <param name="calledAe"></param>
        /// <returns>True if the AE Title is configured; false otherwise.</returns>
        bool IsAeTitleConfigured(string calledAe);

        /// <summary>
        /// Gets next association number.
        /// This is used in logs to association log entries to an association.
        /// </summary>
        uint NextAssociationNumber();

        /// <summary>
        /// Wrapper to get injected service.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        T GetService<T>();

        /// <summary>
        /// Wrapper to get a typed logger.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        ILogger<T> GetLogger<T>(string calledAeTitle);
    }

    internal class ApplicationEntityManager : IApplicationEntityManager, IDisposable
    {
        private readonly object _syncRoot = new object();
        private readonly IHostApplicationLifetime _applicationLifetime;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IServiceScope _serviceScope;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<ApplicationEntityManager> _logger;
        private readonly ILoggerFactory _loggerFactory;
        private readonly ConcurrentDictionary<string, Lazy<ApplicationEntityHandler>> _aeTitleManagers;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private uint _associationCounter;
        private bool _disposed = false;

        public IOptions<DicomAdapterConfiguration> Configuration { get; }

        public ApplicationEntityManager(
            IHostApplicationLifetime applicationLifetime,
            IServiceScopeFactory serviceScopeFactory,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
        {
            _applicationLifetime = applicationLifetime ?? throw new ArgumentNullException(nameof(applicationLifetime));

            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _serviceScope = serviceScopeFactory.CreateScope();
            _serviceProvider = _serviceScope.ServiceProvider;

            _loggerFactory = _serviceProvider.GetService<ILoggerFactory>();
            _logger = _loggerFactory.CreateLogger<ApplicationEntityManager>();
            Configuration = dicomAdapterConfiguration;
            _associationCounter = uint.MaxValue;
            _aeTitleManagers = new ConcurrentDictionary<string, Lazy<ApplicationEntityHandler>>();
            _cancellationTokenSource = new CancellationTokenSource();
            _applicationLifetime.ApplicationStopping.Register(OnApplicationStopping);

            InitializeClaraAeTitles();
        }

        ~ApplicationEntityManager() => Dispose(false);

        private void OnApplicationStopping()
        {
            _logger.Log(LogLevel.Information, "ApplicationEntityManager stopping.");
            _cancellationTokenSource.Cancel();
        }

        public void HandleCStoreRequest(DicomCStoreRequest request, string calledAeTitle, uint associationId)
        {
            Guard.Against.Null(request, nameof(request));

            if (!_aeTitleManagers.ContainsKey(calledAeTitle))
            {
                throw new ArgumentException($"Called AE Title '{calledAeTitle}' is not configured");
            }

            _logger.Log(LogLevel.Information, "Preparing to save instance from {callingAeTitle}.", calledAeTitle);

            var instanceStorage = InstanceStorageInfo.CreateInstanceStorageInfo(request, Configuration.Value.Storage.Temporary, calledAeTitle, associationId);

            using (_logger.BeginScope("SOPInstanceUID={0}", instanceStorage.SopInstanceUid))
            {
                _logger.Log(LogLevel.Information, "Patient ID: {PatientId}", instanceStorage.PatientId);
                _logger.Log(LogLevel.Information, "Study Instance UID: {StudyInstanceUid}", instanceStorage.StudyInstanceUid);
                _logger.Log(LogLevel.Information, "Series Instance UID: {SeriesInstanceUid}", instanceStorage.SeriesInstanceUid);
                _logger.Log(LogLevel.Information, "Storage File Path: {InstanceStorageFullPath}", instanceStorage.InstanceStorageFullPath);

                _aeTitleManagers[calledAeTitle].Value.Save(request, instanceStorage);
                _logger.Log(LogLevel.Debug, "Instance saved with handler", instanceStorage.InstanceStorageFullPath);
            }
        }

        public bool IsAeTitleConfigured(string calledAe)
        {
            return !string.IsNullOrWhiteSpace(calledAe) && _aeTitleManagers.ContainsKey(calledAe);
        }

        public uint NextAssociationNumber()
        {
            lock (_syncRoot)
            {
                if (_associationCounter++ == uint.MaxValue)
                {
                    _associationCounter = 1;
                }
                return _associationCounter;
            }
        }

        public T GetService<T>()
        {
            return (T)_serviceProvider.GetService(typeof(T));
        }

        public ILogger<T> GetLogger<T>(string calledAeTitle)
        {
            if (!_aeTitleManagers.ContainsKey(calledAeTitle)) return null;

            _logger.Log(LogLevel.Warning, "Unable to create logger for AE Title {0}: not defined or not yet initialized", calledAeTitle);
            return _aeTitleManagers[calledAeTitle].Value.CreateLogger<T>();
        }

        private void InitializeClaraAeTitles()
        {
            if (Configuration.Value.ReadAeTitlesFromCrd)
            {
                _logger.Log(LogLevel.Information, "Reading Clara AE Titles from Kubernetes CRD.");
                K8sCrdMonitorService.ClaraAeTitlesChanged += HandleClaraAeTitleChanges;
            }
            else
            {
                _logger.Log(LogLevel.Information, "Reading Clara AE Titles from configuration file.");
                foreach (var claraAe in Configuration.Value.Dicom.Scp.AeTitles)
                {
                    AddNewAeTitle(claraAe);
                }
            }
        }

        private void AddNewAeTitle(ClaraApplicationEntity entity)
        {
            using (var scope = _serviceScopeFactory.CreateScope())
            {
                if (!_aeTitleManagers.TryAdd(
                        entity.AeTitle,
                        new Lazy<ApplicationEntityHandler>(NewHandler(scope.ServiceProvider, entity))))
                {
                    _logger.Log(LogLevel.Error, $"AE Title {0} could not be added to CStore Manager.  Already exits: {1}", entity.AeTitle, _aeTitleManagers.ContainsKey(entity.AeTitle));
                }
                else
                {
                    _logger.Log(LogLevel.Information, $"{entity.AeTitle} added to AE Title Manager");
                }
            }
        }

        private ApplicationEntityHandler NewHandler(IServiceProvider serviceProvider, ClaraApplicationEntity entity)
        {
            return new ApplicationEntityHandler(
                        serviceProvider,
                        entity,
                        Configuration.Value.Storage.Temporary,
                        _cancellationTokenSource.Token);
        }

        private void HandleClaraAeTitleChanges(object sender, AeTitleUpdatedEventArgs e)
        {
            var item = sender as ClaraApplicationEntity;
            switch (e.EventType)
            {
                case WatchEventType.Added:
                    AddNewAeTitle(item);
                    break;

                case WatchEventType.Deleted:
                    _ = _aeTitleManagers.TryRemove(item.AeTitle, out Lazy<ApplicationEntityHandler> handler);
                    handler?.Value?.Dispose();
                    _logger.Log(LogLevel.Information, $"{item.AeTitle} removed from AE Title Manager");
                    break;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _serviceScope.Dispose();
                }
                _disposed = true;
            }
        }
    }
}