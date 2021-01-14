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

using Dicom.Log;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Config;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Server.Services.Export;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using Nvidia.Clara.DicomAdapter.Server.Services.Scu;
using Nvidia.Clara.ResultsService.Api;
using Serilog;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Integration
{
    [CollectionDefinition("DICOM Adapter")]
    public class DicomAdapterHost : ICollectionFixture<DicomAdapterFixture>
    {
        // This class has no code, and is never created. Its purpose is simply
        // to be the place to apply [CollectionDefinition] and all the
        // ICollectionFixture<> interfaces.
    }

    public class DicomAdapterFixture : IAsyncDisposable
    {
        public static readonly string LogTemplate = "{Timestamp:HH:mm:ss.fff} [{Level:u4}] {Properties} {Message}{NewLine}{Exception}";
        private IHost _host;
        private int _connectionsClosed;
        private ManualResetEvent connectionClosedEvent;

        public uint AssociationId { get; private set; }
        public Mock<IPayloads> Payloads { get; }
        public Mock<IJobs> Jobs { get; }
        public Mock<IJobStore> JobStore { get; }
        public Mock<IResultsService> ResultsService { get; }
        public Mock<IKubernetesWrapper> KubernetesWrapper { get; }

        public int ConnectionsClosed { get { return _connectionsClosed; } }

        public DicomAdapterFixture()
        {
            _connectionsClosed = 0;
            Payloads = new Mock<IPayloads>();
            Jobs = new Mock<IJobs>();
            ResultsService = new Mock<IResultsService>();
            KubernetesWrapper = new Mock<IKubernetesWrapper>();
            JobStore = new Mock<IJobStore>();

            ResultsService
                .Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
                .ReturnsAsync(new List<TaskResponse>());

            KubernetesWrapper
                .Setup(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent("") }
                }));
            KubernetesWrapper
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<JobCustomResource>()))
                .Returns(() => Task.FromResult(new HttpOperationResponse<object>()
                {
                    Response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("")
                    }
                }));
            KubernetesWrapper
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Returns(() => Task.FromResult(new HttpOperationResponse<object>()
                {
                    Response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("")
                    }
                }));

            connectionClosedEvent = new ManualResetEvent(false);

            ScpService.ConnectionClosed += (o, associationNumber) =>
            {
                Interlocked.Increment(ref _connectionsClosed);
                AssociationId = associationNumber;
                connectionClosedEvent.Set();
            };

            InitLogger();
            SetupHost();
            Task.Run(async () =>
            {
                await Start();
            });
            Thread.Sleep(5000);
        }

        internal void ResetMocks()
        {
            Jobs.Reset();
            JobStore.Reset();
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

                    services.AddScoped<IJobs>(p => Jobs.Object);
                    services.AddScoped<IPayloads>(p => Payloads.Object);
                    services.AddScoped<IResultsService>(p => ResultsService.Object);
                    services.AddScoped<IKubernetesWrapper>(p => KubernetesWrapper.Object);
                    services.AddSingleton<IJobStore>(p => JobStore.Object);

                    services.AddSingleton<IInstanceStoredNotificationService, InstanceStoredNotificationService>();
                    services.AddSingleton<IApplicationEntityManager, ApplicationEntityManager>();

                    services.AddHostedService<K8sCrdMonitorService>();
                    services.AddHostedService<SpaceReclaimerService>();
                    // services.AddHostedService<JobSubmissionService>();
                    services.AddHostedService<IJobStore>(p => p.GetService<IJobStore>());
                    services.AddHostedService<ScpService>();
                    services.AddHostedService<ScuExportService>();
                    // services.AddHostedService<DicomWebExportService>();
                })
                .Build();
        }

        public IInstanceStoredNotificationService GetIInstanceStoredNotificationService()
        {
            return _host.Services.GetService<IInstanceStoredNotificationService>();
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