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

using Dicom;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Configuration;
using System;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FoDicomNetwork = Dicom.Network;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Scp
{
    public class ScpService : IHostedService, IDisposable, IClaraService
    {
        internal static EventHandler<uint> ConnectionClosed;
        internal static int ActiveConnections = 0;
        private readonly IServiceScope _serviceScope;
        private readonly IServiceProvider _serviceProvider;
        private readonly IApplicationEntityManager _associationDataProvider;
        private readonly ILogger<ScpService> _logger;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly IOptions<DicomAdapterConfiguration> _dicomAdapterConfiguration;
        private FoDicomNetwork.IDicomServer _server;
        public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;

        public ScpService(IServiceScopeFactory serviceScopeFactory,
                                IApplicationEntityManager applicationEntityManager,
                                IHostApplicationLifetime appLifetime,
                                IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
        {
            _serviceScope = serviceScopeFactory.CreateScope();
            _serviceProvider = _serviceScope.ServiceProvider;
            _associationDataProvider = applicationEntityManager ?? throw new ArgumentNullException(nameof(applicationEntityManager));

            var logginFactory = _serviceProvider.GetService<ILoggerFactory>();
            _logger = logginFactory.CreateLogger<ScpService>();
            _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
            _dicomAdapterConfiguration = dicomAdapterConfiguration ?? throw new ArgumentNullException(nameof(dicomAdapterConfiguration));
            var preloadDictionary = DicomDictionary.Default;
        }


        public void Dispose()
        {
            _serviceScope.Dispose();
            _server?.Dispose();
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                _logger.LogInformation("Clara DICOM Adapter (SCP Service) {Version} loading...",
                    Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version);

                try
                {
                    _logger.Log(LogLevel.Information, "Starting SCP Service.");
                    var options = new FoDicomNetwork.DicomServiceOptions
                    {
                        IgnoreUnsupportedTransferSyntaxChange = true,
                        LogDimseDatasets = _dicomAdapterConfiguration.Value.Dicom.Scp.LogDimseDatasets,
                        MaxClientsAllowed = _dicomAdapterConfiguration.Value.Dicom.Scp.MaximumNumberOfAssociations
                    };

                    _server = FoDicomNetwork.DicomServer.Create<ScpServiceInternal>(
                        FoDicomNetwork.NetworkManager.IPv4Any,
                        _dicomAdapterConfiguration.Value.Dicom.Scp.Port,
                        options: options,
                        userState: _associationDataProvider);

                    if (_server.Exception != null)
                    {
                        throw _server.Exception;
                    }
                    
                    Status = ServiceStatus.Running;
                    _logger.Log(LogLevel.Information, "SCP listening on port: {0}", _dicomAdapterConfiguration.Value.Dicom.Scp.Port);
                }
                catch (System.Exception ex)
                {
                    Status = ServiceStatus.Cancelled;
                    _logger.Log(LogLevel.Critical, ex, "Failed to start SCP listener.");
                    _appLifetime.StopApplication();
                }
            });
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.Run(() =>
            {
                _logger.Log(LogLevel.Information, "Stopping SCP Service.");
                _server?.Stop();
                Status = ServiceStatus.Stopped;
                _logger.Log(LogLevel.Information, "SCP Service stopped.");
            });
        }
    }
}