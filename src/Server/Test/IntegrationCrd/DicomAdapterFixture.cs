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

using Dicom.Log;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Config;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Server.Services.Http;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using Nvidia.Clara.DicomAdapter.Server.Services.Scu;
using Nvidia.Clara.ResultsService.Api;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Test.IntegrationCrd
{
    public class DicomAdapterFixture : IAsyncDisposable
    {
        public static readonly string LogTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u4}] {Properties} {Message}{NewLine}{Exception}";
        private IHost _host;
        private ManualResetEvent _localAeTitleHandledEvent;
        private ManualResetEvent _sourceAeTitleHandledEvent;
        private ManualResetEvent _destinationAeTitleHandledEvent;
        private ManualResetEvent _connectionClosedEvent;

        public uint AssociationId { get; private set; }
        public Mock<IPayloads> Payloads { get; }
        public Mock<IJobs> Jobs { get; }
        public Mock<IJobStore> JobStore { get; }
        public Mock<IResultsService> ResultsService { get; }
        public Mock<IKubernetesWrapper> KubernetesClient { get; private set; }

        public DicomAdapterFixture()
        {
            KubernetesClient = new Mock<IKubernetesWrapper>();
            Payloads = new Mock<IPayloads>();
            Jobs = new Mock<IJobs>();
            ResultsService = new Mock<IResultsService>();
            JobStore = new Mock<IJobStore>();

            ResultsService
                .Setup(p => p.GetPendingJobs(It.IsAny<CancellationToken>(), It.IsAny<int>()))
                .ReturnsAsync(new List<TaskResponse>());

            _localAeTitleHandledEvent = new ManualResetEvent(false);
            _sourceAeTitleHandledEvent = new ManualResetEvent(false);
            _destinationAeTitleHandledEvent = new ManualResetEvent(false);
            _connectionClosedEvent = new ManualResetEvent(false);

            K8sCrdMonitorService.ClaraAeTitlesChanged += (o, e) =>
            {
                Console.WriteLine($"Clara AE Title {e.EventType}: { (o as ClaraApplicationEntity).AeTitle}");
                _localAeTitleHandledEvent.Set();
            };
            K8sCrdMonitorService.SourceAeTitlesChanged += (o, e) =>
            {
                Console.WriteLine($"DICOM Source {e.EventType}: { (o as SourceApplicationEntity).AeTitle}");
                _sourceAeTitleHandledEvent.Set();
            };
            K8sCrdMonitorService.DestinationAeTitlesChanged += (o, e) =>
            {
                Console.WriteLine($"DICOM Destination {e.EventType}: { (o as DestinationApplicationEntity).AeTitle}");
                _destinationAeTitleHandledEvent.Set();
            };

            ScpService.ConnectionClosed += (o, associationNumber) =>
            {
                AssociationId = associationNumber;
                _connectionClosedEvent.Set();
            };

            InitLogger();
            SetupHost();
            Task.Run(async () =>
            {
                await Start();
            });
            Thread.Sleep(5000);
        }

        public void WaitForAllEventHandles(int timeout)
        {
            WaitHandle.WaitAll(
                new WaitHandle[] { _localAeTitleHandledEvent, _sourceAeTitleHandledEvent, _destinationAeTitleHandledEvent },
                timeout);
        }

        public void ResetAllHandles()
        {
            KubernetesClient.Reset();
            KubernetesClient.Invocations.Clear();
            _localAeTitleHandledEvent.Reset();
            _sourceAeTitleHandledEvent.Reset();
            _destinationAeTitleHandledEvent.Reset();
        }

        public DicomAdapterConfiguration GetConfiguration()
        {
            return _host.Services.GetService<IOptions<DicomAdapterConfiguration>>()?.Value;
        }

        private void SetupHost()
        {
            _host = new HostBuilder()
                .ConfigureHostConfiguration(config =>
                {
                    config.AddEnvironmentVariables(prefix: "DOTNETCORE_");
                })
                .ConfigureAppConfiguration((builderContext, config) =>
                {
                    var env = builderContext.HostingEnvironment;
                    config
                        .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                        .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true, reloadOnChange: true);
                })
                .ConfigureLogging((hostContext, builder) =>
                {
                    builder.AddConfiguration(hostContext.Configuration.GetSection("Logging"));
                    builder.AddSerilog(dispose: true);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging();
                    services.AddOptions<DicomAdapterConfiguration>()
                        .Bind(hostContext.Configuration.GetSection("DicomAdapter"));
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DicomAdapterConfiguration>, ConfigurationValidator>());

                    services.AddSingleton<ConfigurationValidator>();
                    services.AddSingleton<IInstanceCleanupQueue, InstanceCleanupQueue>();

                    services.AddTransient<IDicomToolkit, DicomToolkit>();
                    services.AddTransient<IFileSystem, FileSystem>();

                    services.AddScoped<IJobs>(p => Jobs.Object); ;
                    services.AddScoped<IPayloads>(p => Payloads.Object);
                    services.AddScoped<IResultsService>(p => ResultsService.Object);
                    services.AddScoped<IKubernetesWrapper>(p => KubernetesClient.Object);
                    services.AddScoped<IJobStore>(p => JobStore.Object);

                    services.AddSingleton<IInstanceStoredNotificationService, InstanceStoredNotificationService>();
                    services.AddSingleton<IApplicationEntityManager, ApplicationEntityManager>();

                    services.AddHostedService<K8sCrdMonitorService>();
                    services.AddHostedService<SpaceReclaimerService>();
                    services.AddHostedService<ScpService>();
                    services.AddHostedService<ScuService>();
                })
                .ConfigureWebHostDefaults(webBuilder =>
                {
                    webBuilder.UseKestrel(options =>
                    {
                        options.ListenAnyIP(5000);
                    });
                    webBuilder.CaptureStartupErrors(true);
                    webBuilder.UseStartup<Startup>();
                })
                .Build();
        }

        private async Task Start()
        {
            await _host.StartAsync();
        }

        private void InitLogger()
        {
            var configuration = new ConfigurationBuilder()
                .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            LogManager.SetImplementation(new SerilogManager(Log.Logger));
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync();
            _host.Dispose();
        }
    }
}