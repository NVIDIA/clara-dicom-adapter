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

using Ardalis.GuardClauses;
using Dicom;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Nvidia.Clara.DicomAdapter.Server.Processors
{
    public class AeTitleJobProcessorValidator : IJobProcessorValidator
    {
        private readonly ILogger<AeTitleJobProcessorValidator> _logger;

        public AeTitleJobProcessorValidator(ILogger<AeTitleJobProcessorValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public void Validate(string aeTitle, Dictionary<string, string> processorSettings)
        {
            Guard.Against.NullOrWhiteSpace(aeTitle, nameof(aeTitle));
            Guard.Against.Null(processorSettings, nameof(processorSettings));

            var valid = true;
            var settingKeys = processorSettings.Keys.ToList();
            var errors = new StringBuilder();
            _logger.Log(LogLevel.Information, "Validating AE Title '{0}' processor settings.", aeTitle);

            string setting = string.Empty;
            if (processorSettings.TryGetValue("timeout", out setting))
            {
                if (!int.TryParse(setting, out int timeout) || timeout < 0)
                {
                    valid = false;
                    var errorMessage = $"Invalid processor setting 'timeout' specified for AE Title {aeTitle}.";
                    errors.AppendLine($"[ERROR] {errorMessage}");
                    _logger.Log(LogLevel.Error, errorMessage);
                }
                settingKeys.Remove("timeout");
            }

            if (processorSettings.TryGetValue("jobRetryDelay", out setting))
            {
                if (!int.TryParse(setting, out int delay))
                {
                    valid = false;
                    var errorMessage = $"Invalid processor setting 'jobRetryDelay' specified for AE Title {aeTitle}.";
                    errors.AppendLine($"[ERROR] {errorMessage}");
                    _logger.Log(LogLevel.Error, errorMessage);
                }
                settingKeys.Remove("jobRetryDelay");
            }

            if (processorSettings.TryGetValue("priority", out setting))
            {
                if (!Enum.TryParse(setting, true, out JobPriority priority))
                {
                    valid = false;
                    var errorMessage = $"Invalid processor setting 'priority' specified for AE Title {aeTitle}.";
                    errors.AppendLine($"[ERROR] {errorMessage}");
                    _logger.Log(LogLevel.Error, errorMessage);
                }
                settingKeys.Remove("priority");
            }

            if (processorSettings.TryGetValue("groupBy", out setting))
            {
                try
                {
                    DicomTag.Parse(setting);
                }
                catch (System.Exception ex)
                {
                    valid = false;
                    var errorMessage = $"Invalid processor setting 'groupBy' specified for AE Title {aeTitle}.";
                    errors.AppendLine($"[ERROR] {errorMessage}");
                    _logger.Log(LogLevel.Error, ex, errorMessage);
                }
                settingKeys.Remove("groupBy");
            }

            var pipelines = 0;
            foreach (var key in processorSettings.Keys)
            {
                if (key.StartsWith("pipeline-", StringComparison.OrdinalIgnoreCase))
                {
                    var name = key.Substring(9);
                    var value = processorSettings[key];
                    settingKeys.Remove(key);
                    pipelines++;
                }
            }

            if (pipelines == 0)
            {
                valid = false;
                var errorMessage = $"No pipeline defined for AE Title {aeTitle}.";
                errors.AppendLine($"[ERROR] {errorMessage}");
                _logger.Log(LogLevel.Error, errorMessage);
            }

            if (settingKeys.Count > 0)
            {
                valid = false; // treat any unprocessed settings as invalid
                foreach (var key in settingKeys)
                {
                    var errorMessage = $"Processor setting ignored {key}={processorSettings[key]}.";
                    errors.AppendLine($"[WARNING] {errorMessage}");
                    _logger.Log(LogLevel.Warning, errorMessage);
                }
            }

            if (!valid)
            {
                throw new ConfigurationException(errors.ToString());
            }
        }
    }
}