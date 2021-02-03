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
using System;

namespace Nvidia.Clara.DicomAdapter.Logging
{
    /// <summary>
    /// Implementation of <see cref="Dicom.Log.Logger"/> for Microsoft.Extensions.Logging.
    /// </summary>
    public class MicrosoftLoggerAdapter : Logger
    {
        private readonly ILogger _logger;

        public MicrosoftLoggerAdapter(ILogger logger)
        {
            _logger = logger;
        }

        public override void Log(global::Dicom.Log.LogLevel level, string msg, params object[] args)
        {
            _logger.Log(level.ToMicrosoftExtensionsLogLevel(), msg, args);
        }

        private static string MessageFormatter(object state, Exception error)
        {
            return state.ToString();
        }
    }
}