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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.Common;
using System;
using System.Collections.Generic;

namespace Nvidia.Clara.DicomAdapter.Configuration
{
    /// <summary>
    /// Validates configuration based on application requirements and DICOM VR requirements.
    /// </summary>
    public class ConfigurationValidator : IValidateOptions<DicomAdapterConfiguration>
    {
        private ILogger<ConfigurationValidator> _logger;
        private List<string> _validationErrors;

        /// <summary>
        /// Initializes an instance of the <see cref="ConfigurationValidator"/> class.
        /// </summary>
        /// <param name="configuration">DicomAdapterConfiguration to be validated</param>
        /// <param name="logger">Logger to be used by ConfigurationValidator</param>
        public ConfigurationValidator(ILogger<ConfigurationValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _validationErrors = new List<string>();
        }

        /// <summary>
        /// Checks if DicomAdapterConfiguration instance contains valid settings required by the application and conforms to DICOM standards.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public ValidateOptionsResult Validate(string name, DicomAdapterConfiguration options)
        {
            var valid = IsDicomScpConfigValid(options.Dicom.Scp);
            valid &= IsDicomScuConfigValid(options.Dicom.Scu);
            valid &= IsServicesValid(options.Services);
            valid &= IsStorageValid(options.Storage);

            _validationErrors.ForEach(p => _logger.Log(LogLevel.Error, p));

            return valid ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(string.Join(Environment.NewLine, _validationErrors));
        }

        private bool IsStorageValid(StorageConfiguration storage)
        {
            var valid = true;
            if (storage.Watermark <= 0 || storage.Watermark > 100)
            {
                valid = false;
                _validationErrors.Add($"Invalid watermark value configured DicomAdapter>storage>watermark: {storage.Watermark}.");
            }

            if (storage.ReservedSpaceGb < 0)
            {
                valid = false;
                _validationErrors.Add($"Invalid reserved space value configured DicomAdapter>storage>reservedSpaceGb: {storage.ReservedSpaceGb}.");
            }
            return valid;
        }

        private bool IsDicomScpConfigValid(ScpConfiguration scpConfiguration)
        {
            var valid = ValidationExtensions.IsPortValid("DicomAdapter>dicom>scp>port", scpConfiguration.Port, _validationErrors);
            valid &= IsValueInRange("DicomAdapter>dicom>scp>max-associations", 1, 1000, scpConfiguration.MaximumNumberOfAssociations);
            valid &= AreVerificationTransferSyntaxesValid(scpConfiguration.Verification.TransferSyntaxes);
            return valid;
        }

        private bool IsDicomScuConfigValid(ScuConfiguration scuConfiguration)
        {
            var valid = ValidationExtensions.IsAeTitleValid("DicomAdapter>dicom>scu>aeTitle", scuConfiguration.AeTitle, _validationErrors);
            valid &= IsValueInRange("DicomAdapter>dicom>scu>max-associations", 1, 100, scuConfiguration.MaximumNumberOfAssociations);
            return valid;
        }

        private bool AreVerificationTransferSyntaxesValid(IList<string> transferSyntaxes)
        {
            try
            {
                if (transferSyntaxes.IsNullOrEmpty())
                {
                    _validationErrors.Add("No transfer syntax configured for verification service: DicomAdapter>dicom>scp>verification>transfer-syntaxes.");
                    return false;
                }

                transferSyntaxes.ToDicomTransferSyntaxArray();
                return true;
            }
            catch
            {
                _validationErrors.Add("Invalid Transfer Syntax UID found in DicomAdapter>dicom>scp>verification>transfer-syntaxes.");
                return false;
            }
        }

        private bool IsServicesValid(ServicesConfiguration services)
        {
            var valid = true;

            Uri uri;
            if (string.IsNullOrWhiteSpace(services.ResultsServiceEndpoint) ||
                !Uri.TryCreate(services.ResultsServiceEndpoint, UriKind.Absolute, out uri) ||
                !uri.IsWellFormedOriginalString())
            {
                _validationErrors.Add("Results Service API endpoint is not configured or invalid: DicomAdapter>services>results-service-endpoint.");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(services.PlatformEndpoint))
            {
                _validationErrors.Add("Clara Service API endpoint is not configured: DicomAdapter>services>platform-endpoint.");
                valid = false;
            }

            return valid;
        }

        private bool IsValueInRange(string source, int minValue, int maxValue, int actualValue)
        {
            if (actualValue >= minValue && actualValue <= maxValue) return true;

            _validationErrors.Add($"Value of {source} must be between {minValue} and {maxValue}.");
            return false;
        }
    }
}