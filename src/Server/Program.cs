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

using Ardalis.GuardClauses;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Database;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Server.Services.Export;
using Nvidia.Clara.DicomAdapter.Server.Services.Http;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using Nvidia.Clara.DicomAdapter.Server.Services.Scu;
using System;
using System.IO.Abstractions;

namespace Nvidia.Clara.DicomAdapter
{
    public class Program
    {
        private const int HTTPCLIENT_TIMEOUT_MINUTES = 60;
        private static readonly string ApplicationEntryDirectory = AppDomain.CurrentDomain.BaseDirectory;

        private static void Main(string[] args)
        {
            Console.WriteLine("Reading logging.config from {0}", ApplicationEntryDirectory);
            Environment.CurrentDirectory = ApplicationEntryDirectory;
            Environment.SetEnvironmentVariable("HOSTNAME", Environment.MachineName);
            // ConfigureLogging();

            var host = CreateHostBuilder(args).Build();
            var loggerFactory = InitializeLogger(host);
            LoadPlugins(loggerFactory.CreateLogger("PlugInLoader"));
            InitializeDatabase(host);
            host.Run();
        }

        private static void LoadPlugins(ILogger logger)
        {
            Guard.Against.Null(logger, nameof(logger));

            try
            {
                PlugInLoader.LoadExternalProcessors(logger);
            }
            catch (System.Exception ex)
            {
                logger.Log(Microsoft.Extensions.Logging.LogLevel.Error, ex, "Error loading plugins.");
            }
        }

        private static ILoggerFactory InitializeLogger(IHost host)
        {
            Guard.Against.Null(host, nameof(host));

            var loggerFactory = new LoggerFactory();
            return loggerFactory;
        }

        private static void InitializeDatabase(IHost host)
        {
            Guard.Against.Null(host, nameof(host));

            using (var serviceScope = host.Services.CreateScope())
            {
                var context = serviceScope.ServiceProvider.GetRequiredService<DicomAdapterContext>();
                context.Database.Migrate();
            }
        }

        public static IHostBuilder CreateHostBuilder(string[] args) =>
            Host.CreateDefaultBuilder(args)
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
                    builder.AddConsole(options =>
                    {
                        options.IncludeScopes = true;
                        options.TimestampFormat = "hh:mm:ss ";
                    });
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddLogging();
                    services.AddOptions<DicomAdapterConfiguration>()
                        .Bind(hostContext.Configuration.GetSection("DicomAdapter"))
                        .PostConfigure(options =>
                        {
                            OverwriteWithEnvironmentVariables(options);
                        });
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DicomAdapterConfiguration>, ConfigurationValidator>());

                    services.AddDbContext<DicomAdapterContext>(
                        options => options.UseSqlite(hostContext.Configuration.GetConnectionString(DicomConfiguration.DatabaseConnectionStringKey)));

                    services.AddSingleton<ConfigurationValidator>();
                    services.AddSingleton<IInstanceCleanupQueue, InstanceCleanupQueue>();

                    services.AddTransient<IDicomToolkit, DicomToolkit>();
                    services.AddTransient<IFileSystem, FileSystem>();

                    services.AddTransient<IJobs, ClaraJobsApi>();
                    services.AddTransient<IPayloads, ClaraPayloadsApi>();
                    services.AddTransient<IResultsService, ResultsApi>();
                    services.AddTransient<IJobRepository, ClaraJobRepository>();
                    services.AddTransient<IInferenceRequestRepository, InferenceRequestRepository>();
                    services.AddTransient(typeof(IDicomAdapterRepository<>), typeof(DicomAdapterRepository<>));

                    services.AddSingleton<IStorageInfoProvider, StorageInfoProvider>();
                    services.AddSingleton<SpaceReclaimerService>();
                    services.AddSingleton<JobSubmissionService>();
                    services.AddSingleton<DataRetrievalService>();
                    services.AddSingleton<ScpService>();
                    services.AddSingleton<ScuExportService>();
                    services.AddSingleton<DicomWebExportService>();
                    services.AddSingleton<IInstanceStoredNotificationService, InstanceStoredNotificationService>();
                    services.AddSingleton<IApplicationEntityManager, ApplicationEntityManager>();
                    services.AddSingleton<IClaraAeChangedNotificationService, ClaraAeChangedNotificationService>();

                    services
                        .AddHttpClient("dicomweb", configure => configure.Timeout = TimeSpan.FromMinutes(HTTPCLIENT_TIMEOUT_MINUTES))
                        .SetHandlerLifetime(TimeSpan.FromMinutes(HTTPCLIENT_TIMEOUT_MINUTES));

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
                });

        private static void OverwriteWithEnvironmentVariables(DicomAdapterConfiguration options)
        {
            ConfigurePlatformEndpoint(options, Environment.GetEnvironmentVariable("CLARA_SERVICE_HOST"), Environment.GetEnvironmentVariable("CLARA_SERVICE_PORT_API"));
            ConfigureResultsServiceEndpoint(options, Environment.GetEnvironmentVariable("CLARA_RESULTSSERVICE_SERVICE_HOST"), Environment.GetEnvironmentVariable("CLARA_RESULTSSERVICE_SERVICE_PORT"));
        }

        private static void ConfigureResultsServiceEndpoint(DicomAdapterConfiguration options, string ip, string port)
        {
            if (!string.IsNullOrWhiteSpace(ip) && !string.IsNullOrWhiteSpace(port))
            {
                options.Services.ResultsServiceEndpoint = $"http://{ip}:{port}";
                Console.WriteLine("Results Service API endpoint set to {0}", options.Services.ResultsServiceEndpoint);
            }
        }

        private static void ConfigurePlatformEndpoint(DicomAdapterConfiguration options, string ip, string port)
        {
            if (!string.IsNullOrWhiteSpace(ip) && !string.IsNullOrWhiteSpace(port))
            {
                options.Services.PlatformEndpoint = $"{ip}:{port}";
                Console.WriteLine("Platform API endpoint set to {0}", options.Services.PlatformEndpoint);
            }
        }
    }
}