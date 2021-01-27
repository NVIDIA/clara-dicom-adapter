/*
 * Apache License, Version 2.0
 * Copyright 2019-2021 NVIDIA Corporation
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

using k8s;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Config
{
    public class AeTitleUpdatedEventArgs : EventArgs
    {
        public WatchEventType EventType { get; }

        public AeTitleUpdatedEventArgs(WatchEventType eventType) => EventType = eventType;
    }

    public class K8sCrdMonitorService : IHostedService, IClaraService
    {
        private static readonly object SyncRoot = new Object();
        public static EventHandler<AeTitleUpdatedEventArgs> ClaraAeTitlesChanged;
        public static EventHandler<AeTitleUpdatedEventArgs> SourceAeTitlesChanged;
        public static EventHandler<AeTitleUpdatedEventArgs> DestinationAeTitlesChanged;

        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        private readonly ILogger<K8sCrdMonitorService> _logger;
        private readonly IKubernetesWrapper _kubernetesClient;
        private readonly ConfigurationValidator _configurationValidator;
        private readonly ILoggerFactory _loggerFactory;

        private CustomResourceWatcher<ClaraApplicationEntityCustomResourceList, ClaraApplicationEntityCustomResource> _lLocalAeTitleCrdWatcher;
        private CustomResourceWatcher<SourceApplicationEntityCustomResourceList, SourceApplicationEntityCustomResource> _sourceAeTitleCrdWatcher;
        private CustomResourceWatcher<DestinationApplicationEntityCustomResourceList, DestinationApplicationEntityCustomResource> _destinationAeTitleCrdWatcher;

        public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;

        public K8sCrdMonitorService(
            ILoggerFactory loggerFactory,
            IOptions<DicomAdapterConfiguration> configuration,
            ILogger<K8sCrdMonitorService> logger,
            IKubernetesWrapper kubernetesClient,
            ConfigurationValidator configurationValidator)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _kubernetesClient = kubernetesClient ?? throw new ArgumentNullException(nameof(kubernetesClient));
            _configurationValidator = configurationValidator ?? throw new ArgumentNullException(nameof(configurationValidator));
        }

        protected async Task BackgroundProcessing(CancellationToken stoppingToken)
        {
            if (_configuration.Value.ReadAeTitlesFromCrd)
            {
                _logger.Log(LogLevel.Information, "Kubernetes CRD Monitor Hosted Service is running.");

                _configuration.Value.Dicom.Scp.AeTitles.Clear();
                _lLocalAeTitleCrdWatcher = new CustomResourceWatcher<ClaraApplicationEntityCustomResourceList, ClaraApplicationEntityCustomResource>(
                    _loggerFactory.CreateLogger<CustomResourceWatcher<ClaraApplicationEntityCustomResourceList, ClaraApplicationEntityCustomResource>>(),
                    _kubernetesClient,
                    CustomResourceDefinition.ClaraAeTitleCrd,
                    stoppingToken,
                    HandleClaraAeTitleEvents);
                await Task.Run(() => _lLocalAeTitleCrdWatcher.Start(_configuration.Value.CrdReadIntervals));

                _configuration.Value.Dicom.Scp.Sources.Clear();
                _sourceAeTitleCrdWatcher = new CustomResourceWatcher<SourceApplicationEntityCustomResourceList, SourceApplicationEntityCustomResource>(
                    _loggerFactory.CreateLogger<CustomResourceWatcher<SourceApplicationEntityCustomResourceList, SourceApplicationEntityCustomResource>>(),
                    _kubernetesClient,
                    CustomResourceDefinition.SourceAeTitleCrd,
                    stoppingToken,
                    HandleSourceAeTitleEvents);
                await Task.Run(() => _sourceAeTitleCrdWatcher.Start(_configuration.Value.CrdReadIntervals));

                _configuration.Value.Dicom.Scu.Destinations.Clear();
                _destinationAeTitleCrdWatcher = new CustomResourceWatcher<DestinationApplicationEntityCustomResourceList, DestinationApplicationEntityCustomResource>(
                    _loggerFactory.CreateLogger<CustomResourceWatcher<DestinationApplicationEntityCustomResourceList, DestinationApplicationEntityCustomResource>>(),
                    _kubernetesClient,
                    CustomResourceDefinition.DestinationAeTitleCrd,
                    stoppingToken,
                    HandleDestinationAeTitleEvents);
                await Task.Run(() => _destinationAeTitleCrdWatcher.Start(_configuration.Value.CrdReadIntervals));
            }
            else
            {
                _logger.Log(LogLevel.Information, "Reading AE Title from CRD is disabled.");
            }
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var task = Task.Run(async () =>
            {
                await BackgroundProcessing(cancellationToken);
            });

            Status = ServiceStatus.Running;
            if (task.IsCompleted)
                return task;

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Kubernetes CRD Monitor Hosted Service is stopping.");
            _lLocalAeTitleCrdWatcher.Stop();
            _sourceAeTitleCrdWatcher.Stop();
            _destinationAeTitleCrdWatcher.Stop();
            Status = ServiceStatus.Stopped;
            return Task.CompletedTask;
        }

        private void HandleDestinationAeTitleEvents(WatchEventType eventType, DestinationApplicationEntityCustomResource item)
        {
            lock (SyncRoot)
            {
                switch (eventType)
                {
                    case WatchEventType.Added:
                        if (!_configurationValidator.IsDestinationValid(item.Spec))
                        {
                            _logger.Log(LogLevel.Error, $"The configured DICOM Destination is invalid: {item.Spec.Name} with AE Title {item.Spec.AeTitle}");
                            return;
                        }

                        _configuration.Value.Dicom.Scu.Destinations.Add(item.Spec);
                        _logger.Log(LogLevel.Information, $"Destination AE Title added: {item.Spec.AeTitle}");
                        break;

                    case WatchEventType.Deleted:
                        var deleted = _configuration.Value.Dicom.Scu.Destinations.FirstOrDefault(
                            p => p.Name.Equals(item.Spec.Name, StringComparison.OrdinalIgnoreCase));
                        if (deleted != null)
                        {
                            _configuration.Value.Dicom.Scu.Destinations.Remove(deleted);
                        }

                        _logger.Log(LogLevel.Information, $"Destination AE Title deleted: {item.Spec.Name}");
                        break;

                    default:
                        _logger.Log(LogLevel.Warning, $"Unsupported watch event type {eventType} detected for {item.Metadata.Name}");
                        break;
                }

                if (DestinationAeTitlesChanged != null)
                {
                    DestinationAeTitlesChanged(item.Spec, new AeTitleUpdatedEventArgs(eventType));
                }
            }
        }

        private void HandleSourceAeTitleEvents(WatchEventType eventType, SourceApplicationEntityCustomResource item)
        {
            lock (SyncRoot)
            {
                switch (eventType)
                {
                    case WatchEventType.Added:
                        if (!_configurationValidator.IsSourceValid(item.Spec))
                        {
                            _logger.Log(LogLevel.Error, $"The configured DICOM Source is invalid: AE Title {item.Spec.AeTitle}");
                            return;
                        }

                        _configuration.Value.Dicom.Scp.Sources.Add(item.Spec);
                        _logger.Log(LogLevel.Information, $"Source AE Title added: {item.Spec.AeTitle}");
                        break;

                    case WatchEventType.Deleted:
                        var deleted = _configuration.Value.Dicom.Scp.Sources.FirstOrDefault(p => p.AeTitle.Equals(item.Spec.AeTitle, StringComparison.OrdinalIgnoreCase));
                        if (deleted != null)
                        {
                            _configuration.Value.Dicom.Scp.Sources.Remove(deleted);
                        }

                        _logger.Log(LogLevel.Information, $"Source AE Title deleted: {item.Spec.AeTitle}");
                        break;

                    default:
                        _logger.Log(LogLevel.Warning, $"Unsupported watch event type {eventType} detected for {item.Metadata.Name}");
                        break;
                }

                if (SourceAeTitlesChanged != null)
                {
                    SourceAeTitlesChanged(item.Spec, new AeTitleUpdatedEventArgs(eventType));
                }
            }
        }

        private void HandleClaraAeTitleEvents(WatchEventType eventType, ClaraApplicationEntityCustomResource item)
        {
            lock (SyncRoot)
            {
                switch (eventType)
                {
                    case WatchEventType.Added:
                        if (!_configurationValidator.IsClaraAeTitleValid(_configuration.Value.Dicom.Scp.AeTitles, "dicom>scp>aeTitle", item.Spec, true))
                        {
                            _logger.Log(LogLevel.Error, $"The configured Clara AE Title is invalid: {item.Spec.Name} with AE Title {item.Spec.AeTitle}.");
                            return;
                        }

                        _configuration.Value.Dicom.Scp.AeTitles.Add(item.Spec);
                        _logger.Log(LogLevel.Information, $"Clara AE Title added: {item.Spec.AeTitle} with processor: {string.Join(",", item.Spec.Processor)}");
                        break;

                    case WatchEventType.Deleted:
                        var deleted = _configuration.Value.Dicom.Scp.AeTitles.FirstOrDefault(p => p.Name.Equals(item.Spec.Name, StringComparison.OrdinalIgnoreCase));
                        if (deleted != null)
                        {
                            _configuration.Value.Dicom.Scp.AeTitles.Remove(deleted);
                        }

                        _logger.Log(LogLevel.Information, $"Clara AE Title deleted: {item.Spec.Name}");
                        break;

                    default:
                        _logger.Log(LogLevel.Warning, $"Unsupported watch event type {eventType} detected for {item.Metadata.Name}");
                        break;
                }

                if (ClaraAeTitlesChanged != null)
                {
                    ClaraAeTitlesChanged(item.Spec, new AeTitleUpdatedEventArgs(eventType));
                }
            }
        }
    }
}