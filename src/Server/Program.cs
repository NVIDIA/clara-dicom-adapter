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
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Config;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Server.Services.Http;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using Nvidia.Clara.DicomAdapter.Server.Services.Scu;
using Serilog;
using System;
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter
{
    public class Program : IAsyncDisposable
    {
        private static readonly string ApplicationEntryDirectory = AppDomain.CurrentDomain.BaseDirectory;
        private static CancellationTokenSource cancellationSource = new CancellationTokenSource();
        private static Serilog.ILogger Logger;
        private IHost _host;

        public Program(string[] args)
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
                        .Bind(hostContext.Configuration.GetSection("DicomAdapter"))
                        .PostConfigure(options =>
                        {
                            OverwriteWithEnvironmentVariables(options);
                        });
                    services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<DicomAdapterConfiguration>, ConfigurationValidator>());

                    services.AddSingleton<ConfigurationValidator>();
                    services.AddSingleton<IInstanceCleanupQueue, InstanceCleanupQueue>();

                    services.AddTransient<IDicomToolkit, DicomToolkit>();
                    services.AddTransient<IFileSystem, FileSystem>();

                    services.AddScoped<IJobs, ClaraJobsApi>();
                    services.AddScoped<IPayloads, ClaraPayloadsApi>();
                    services.AddScoped<IResultsService, ResultsApi>();
                    services.AddScoped<IKubernetesWrapper, KubernetesClientWrapper>();

                    services.AddSingleton<IInstanceStoredNotificationService, InstanceStoredNotificationService>();
                    services.AddSingleton<IApplicationEntityManager, ApplicationEntityManager>();
                    services.AddSingleton<IJobStore, JobStore>();
                    // services.AddSingleton<IJobStore>(serviceProvider => serviceProvider.GetService<JobStore>());

                    services.AddHostedService<K8sCrdMonitorService>();
                    services.AddHostedService<SpaceReclaimerService>();
                    services.AddHostedService<JobSubmissionService>();
                    services.AddHostedService<IJobStore>(p => p.GetService<IJobStore>());
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

        public async Task Start()
        {
            try
            {
                await _host.RunAsync();
            }
            catch (System.Exception ex)
            {
                Logger.Fatal("Unknown error occurred: {0}", ex);
                Environment.Exit((int)ErrorCode.UnknownError);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _host.StopAsync();
            _host.Dispose();
        }

        private static async Task Main(string[] args)
        {
            ConfigureLogging();
            LoadPlugins();

            var program = new Program(args);
            await program.Start();
            await program.DisposeAsync();
        }

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

        private static void LoadPlugins()
        {
            try
            {
                PlugInLoader.LoadExternalProcessors(Logger);
            }
            catch (System.Exception ex)
            {
                Logger.Warning("Failed to load external job processors {0}", ex);
            }
        }

        private static void ConfigureLogging()
        {
            Console.WriteLine("Reading logging.config from {0}", ApplicationEntryDirectory);
            Environment.CurrentDirectory = ApplicationEntryDirectory;
            Environment.SetEnvironmentVariable("HOSTNAME", Environment.MachineName);

            var configuration = new ConfigurationBuilder()
                .SetBasePath(ApplicationEntryDirectory)
                .AddJsonFile(path: "appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .CreateLogger();

            LogManager.SetImplementation(new SerilogManager(Log.Logger));
            Logger = Log.Logger.ForContext<Program>();
        }
    }
}