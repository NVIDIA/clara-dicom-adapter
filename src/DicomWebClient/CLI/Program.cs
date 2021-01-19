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

using ConsoleAppFramework;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.Dicom.DicomWeb.Client;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using System;
using System.Threading.Tasks;

namespace Nvidia.Clara.Dicom.DicomWeb.CLI
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            // target T as ConsoleAppBase.
            await Host.CreateDefaultBuilder()
                .ConfigureLogging(logging =>
                {
                    logging.AddConsole(options =>
                    {
                        options.IncludeScopes = false;
                        options.TimestampFormat = "hh:mm:ss ";
                    });

                    // Configure MinimumLogLevel(CreaterDefaultBuilder's default is Warning).
                    logging.SetMinimumLevel(LogLevel.Information);
                })
                .ConfigureServices((hostContext, services) =>
                {
                    services.AddHttpClient<IDicomWebClient, DicomWebClient>(configure => configure.Timeout = TimeSpan.FromMinutes(60))
                        .SetHandlerLifetime(TimeSpan.FromMinutes(60));
                })
                .RunConsoleAppFrameworkAsync(args);
        }
    }
}