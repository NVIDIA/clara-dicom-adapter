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
using Dicom.Log;
using Microsoft.Extensions.Logging;

namespace Nvidia.Clara.DicomAdapter.Logging
{
    public class FoDicomLogManager : LogManager
    {
        private readonly ILoggerFactory _loggerFactory;

        public FoDicomLogManager(ILoggerFactory loggerFactory)
        {
            _loggerFactory = loggerFactory ?? throw new System.ArgumentNullException(nameof(loggerFactory));
        }

        protected override Logger GetLoggerImpl(string name)
        {
            Guard.Against.NullOrWhiteSpace(name, nameof(name));
            
            return new MicrosoftLoggerAdapter(_loggerFactory.CreateLogger(name));
        }
    }
}