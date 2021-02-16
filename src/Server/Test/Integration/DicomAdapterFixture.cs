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

using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
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
using Nvidia.Clara.DicomAdapter.Database;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Server.Services.Export;
using Nvidia.Clara.DicomAdapter.Server.Services.Http;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using Nvidia.Clara.DicomAdapter.Server.Services.Scu;
using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
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
        internal static string AET_CECHO = "CECHOTEST";
        internal static string AET_CLARA1 = "Clara1";
        internal static string AET_CLARA2 = "Clara2";
        private IHost _host;
        private int _connectionsClosed;
        private ManualResetEvent connectionClosedEvent;

        public uint AssociationId { get; private set; }
        public Mock<IPayloads> Payloads { get; }
        public Mock<IJobs> Jobs { get; }
        public Mock<IResultsService> ResultsService { get; }

        public int ConnectionsClosed { get { return _connectionsClosed; } }

        public DicomAdapterFixture()
        {
            _connectionsClosed = 0;
            Payloads = new Mock<IPayloads>();
            Jobs = new Mock<IJobs>();
            ResultsService = new Mock<IResultsService>();

            ResultsService
                .Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
                .ReturnsAsync(new List<TaskResponse>());

            connectionClosedEvent = new ManualResetEvent(false);

            ScpService.ConnectionClosed += (o, associationNumber) =>
            {
                Interlocked.Increment(ref _connectionsClosed);
                AssociationId = associationNumber;
                connectionClosedEvent.Set();
            };

            SetupHost();
            PopulateDatabase();
            Task.Run(async () =>
            {
                await _host.StartAsync();
            });

            EnsurAllServiceStarted();
        }

        private void EnsurAllServiceStarted()
        {
            using var serviceScope = _host.Services.CreateScope();
            var scpService = serviceScope.ServiceProvider.GetService<ScpService>();
            var claraServices = new List<IClaraService>();

            claraServices.Add(scpService as IClaraService);

            while (true)
            {
                if (claraServices.All(p => p.Status == API.Rest.ServiceStatus.Running))
                {
                    break;
                }
                Task.Delay(100);
            }
        }

        private void PopulateDatabase()
        {
            using var serviceScope = _host.Services.CreateScope();

            var context = serviceScope.ServiceProvider.GetRequiredService<DicomAdapterContext>();

            context.Database.EnsureDeleted();
            context.Database.Migrate();

            context.ClaraApplicationEntities.Add(new ClaraApplicationEntity
            {
                Name = AET_CECHO,
                AeTitle = AET_CECHO,
                OverwriteSameInstance = false,
                ProcessorSettings = new Dictionary<string, string>() { { "pipeline-lung", "test" } }
            });
            context.ClaraApplicationEntities.Add(new ClaraApplicationEntity
            {
                Name = AET_CLARA1,
                AeTitle = AET_CLARA1,
                OverwriteSameInstance = false,
                ProcessorSettings = new Dictionary<string, string>() 
                { 
                    { "priority", "Higher" } ,
                    { "pipeline-chest", "test" }
                }
            });
            context.ClaraApplicationEntities.Add(new ClaraApplicationEntity
            {
                Name = AET_CLARA2,
                AeTitle = AET_CLARA2,
                OverwriteSameInstance = false,
                ProcessorSettings = new Dictionary<string, string>() 
                { 
                    { "timeout", "10" } ,
                    { "groupBy", "0010,0020" } ,
                    { "pipeline-lung", "test" } ,
                    { "pipeline-brain", "test" } 
                }
            });

            context.SourceApplicationEntities.Add(new SourceApplicationEntity
            {
                HostIp = "127.0.0.1",
                AeTitle = "PACS1"
            });
            context.SourceApplicationEntities.Add(new SourceApplicationEntity
            {
                HostIp = "127.0.0.1",
                AeTitle = "PACS2"
            });

            context.DestinationApplicationEntities.Add(new DestinationApplicationEntity
            {
                HostIp = "127.0.0.1",
                AeTitle = "STORESCP",
                Name = "PACS1",
                Port = 11112
            });
            context.DestinationApplicationEntities.Add(new DestinationApplicationEntity
            {
                HostIp = "127.0.0.1",
                AeTitle = "STORESCP2",
                Name = "LOCALSCP",
                Port = 12345
            });

            context.SaveChanges();

            if (context.SourceApplicationEntities.Count() != 2 ||
                context.DestinationApplicationEntities.Count() != 2 ||
                context.ClaraApplicationEntities.Count() != 3)
            {
                throw new ApplicationException("databse initialization failed");
            }
        }

        internal void ResetMocks()
        {
            Jobs.Reset();
        }

        private void SetupHost()
        {
            _host = Host.CreateDefaultBuilder()
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
                 })
                 .ConfigureServices((hostContext, services) =>
                 {
                     services.AddLogging();
                     services.AddOptions<DicomAdapterConfiguration>()
                         .Bind(hostContext.Configuration.GetSection("DicomAdapter"));

                     services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DicomAdapterConfiguration>, ConfigurationValidator>());

                     services.AddDbContext<DicomAdapterContext>(
                         options => options.UseSqlite(hostContext.Configuration.GetConnectionString("DicomAdapterDatabase")));

                     services.AddSingleton<ConfigurationValidator>();
                     services.AddSingleton<IInstanceCleanupQueue, InstanceCleanupQueue>();

                     services.AddTransient<IDicomToolkit, DicomToolkit>();
                     services.AddTransient<IFileSystem, FileSystem>();

                     services.AddTransient<IJobs>(p => Jobs.Object);
                     services.AddTransient<IPayloads>(p => Payloads.Object);
                     services.AddTransient<IResultsService>(p => ResultsService.Object);

                     services.AddTransient<IJobRepository, ClaraJobRepository>();
                     services.AddTransient<IInferenceRequestRepository, InferenceRequestRepository>();
                     services.AddTransient(typeof(IDicomAdapterRepository<>), typeof(DicomAdapterRepository<>));

                     services.AddSingleton<SpaceReclaimerService>();
                     services.AddSingleton<JobSubmissionService>();
                     services.AddSingleton<DataRetrievalService>();
                     services.AddSingleton<ScpService>();
                     services.AddSingleton<ScuExportService>();
                     services.AddSingleton<DicomWebExportService>();
                     services.AddSingleton<IInstanceStoredNotificationService, InstanceStoredNotificationService>();
                     services.AddSingleton<IApplicationEntityManager, ApplicationEntityManager>();
                     services.AddSingleton<IClaraAeChangedNotificationService, ClaraAeChangedNotificationService>();

                     services.AddHttpClient("dicomweb", configure => configure.Timeout = TimeSpan.FromMinutes(60))
                         .SetHandlerLifetime(TimeSpan.FromMinutes(60));

                     services.AddHostedService<SpaceReclaimerService>(p => p.GetService<SpaceReclaimerService>());
                     services.AddHostedService<JobSubmissionService>(p => p.GetService<JobSubmissionService>());
                     services.AddHostedService<DataRetrievalService>(p => p.GetService<DataRetrievalService>());
                     services.AddHostedService<ScpService>(p => p.GetService<ScpService>());
                     services.AddHostedService<ScuExportService>(p => p.GetService<ScuExportService>());
                     services.AddHostedService<DicomWebExportService>(p => p.GetService<DicomWebExportService>());
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

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync();
            _host.Dispose();
        }
        
        public IInstanceStoredNotificationService GetIInstanceStoredNotificationService()
        {
            return _host.Services.GetService<IInstanceStoredNotificationService>();
        }
    }
}