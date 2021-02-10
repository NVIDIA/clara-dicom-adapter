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
using Microsoft.Extensions.Logging;

namespace Nvidia.Clara.DicomAdapter.Logging
{
    public static class LoggingExtensions
    {
        public static ILoggerFactory CaptureFoDicomLogs(this ILoggerFactory factory)
        {
            if (factory is null)
            {
                throw new System.ArgumentNullException(nameof(factory));
            }

            LogManager.SetImplementation(new FoDicomLogManager(factory));
            return factory;
        }

        public static Microsoft.Extensions.Logging.LogLevel ToMicrosoftExtensionsLogLevel(this global::Dicom.Log.LogLevel dicomLogLevel)
        {
            return dicomLogLevel switch
            {
                global::Dicom.Log.LogLevel.Error => Microsoft.Extensions.Logging.LogLevel.Error,
                global::Dicom.Log.LogLevel.Fatal => Microsoft.Extensions.Logging.LogLevel.Critical,
                global::Dicom.Log.LogLevel.Info => Microsoft.Extensions.Logging.LogLevel.Information,
                global::Dicom.Log.LogLevel.Warning => Microsoft.Extensions.Logging.LogLevel.Warning,
                _ => Microsoft.Extensions.Logging.LogLevel.Debug
            };
        }
    }
}