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
using System.IO;
using System.Reflection;
using Serilog;

namespace Nvidia.Clara.DicomAdapter.Server.Common
{
    public static class PlugInLoader
    {
        public static void LoadExternalProcessors(ILogger logger, string plugInFolderName = "Processors")
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, plugInFolderName);
            if (!Directory.Exists(path))
            {
                return;
            }

            logger.Information("Loading job processors from {0}", path);
            var assemblies = Directory.GetFiles(path, "*.dll");

            foreach (var assembly in assemblies)
            {
                Assembly.LoadFile(assembly);
                logger.Information("Loaded external job processor: {0}", assembly);
            }
        }
    }
}
