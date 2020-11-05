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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.Common;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nvidia.Clara.DicomAdapter.Configuration
{
    /// <summary>
    /// Validates configuration based on application requirements and DICOM VR requirements.
    /// </summary>
    public class ConfigurationValidator : IValidateOptions<DicomAdapterConfiguration>
    {
        private ILogger<ConfigurationValidator> _logger;

        /// <summary>
        /// Initializes an instance of the <see cref="ConfigurationValidator"/> class.
        /// </summary>
        /// <param name="configuration">DicomAdapterConfiguration to be validated</param>
        /// <param name="logger">Logger to be used by ConfigurationValidator</param>
        public ConfigurationValidator(ILogger<ConfigurationValidator> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Checks if DicomAdapterConfiguration instance contains valid settings required by the application and conforms to DICOM standards.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public ValidateOptionsResult Validate(string name, DicomAdapterConfiguration options)
        {
            var valid = IsDicomScpConfigValid(options.Dicom.Scp, options.ReadAeTitlesFromCrd);
            valid &= IsDicomScuConfigValid(options.Dicom.Scu, options.ReadAeTitlesFromCrd);
            valid &= IsServicesValid(options.Services);
            return valid ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail("See console output for details.");
        }

        private bool IsDicomScpConfigValid(ScpConfiguration scpConfiguration, bool readAeTitlesFromCrd)
        {
            var valid = IsPortValid("DicomAdapter>dicom>scp>port", scpConfiguration.Port);
            valid &= AreAeTitlesValid("DicomAdapter>dicom>scp>ae-title", scpConfiguration.AeTitles, readAeTitlesFromCrd);
            valid &= IsValueInRange("DicomAdapter>dicom>scp>max-associations", 1, Int32.MaxValue, scpConfiguration.MaximumNumberOfAssociations);
            valid &= AreVerificationTransferSyntaxesValid(scpConfiguration.Verification.TransferSyntaxes);
            valid &= AreScpSourcesValid(scpConfiguration, readAeTitlesFromCrd);
            return valid;
        }

        private bool IsDicomScuConfigValid(ScuConfiguration scuConfiguration, bool readAeTitlesFromCrd)
        {
            var valid = IsAeTitleValid("DicomAdapter>dicom>scu>ae-title", scuConfiguration.AeTitle);
            valid &= IsValueInRange("DicomAdapter>dicom>scu>max-associations", 1, Int32.MaxValue, scuConfiguration.MaximumNumberOfAssociations);
            valid &= AreScuDestinationsValid(scuConfiguration.Destinations, readAeTitlesFromCrd);
            return valid;
        }

        private bool IsPortValid(string source, int port)
        {
            if (port > 0 && port <= 65535) return true;

            _logger.Log(LogLevel.Error, "Invalid port number '{0}' specified in {1}.", port, source);
            return false;
        }

        private bool AreVerificationTransferSyntaxesValid(IList<string> transferSyntaxes)
        {
            try
            {
                if (transferSyntaxes.IsNullOrEmpty())
                {
                    _logger.Log(LogLevel.Error, "No transfer syntax configured for verification service: DicomAdapter>dicom>scp>verification>transfer-syntaxes.");
                    return false;
                }

                transferSyntaxes.ToDicomTransferSyntaxArray();
                return true;
            }
            catch
            {
                _logger.Log(LogLevel.Error, "Invalid Transfer Syntax UID found in DicomAdapter>dicom>scp>verification>transfer-syntaxes.");
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
                _logger.Log(LogLevel.Error, "Results Service API endpoint is not configured or invalid: DicomAdapter>services>results-service-endpoint.");
                valid = false;
            }

            if (string.IsNullOrWhiteSpace(services.PlatformEndpoint))
            {
                _logger.Log(LogLevel.Error, "Clara Service API endpoint is not configured: DicomAdapter>services>platform-endpoint.");
                valid = false;
            }

            return valid;
        }

        private bool AreScuDestinationsValid(IList<DestinationApplicationEntity> destinations, bool readDestinationsFromCrd)
        {
            var valid = true;
            // It's fine if no destinations are configured
            if (!readDestinationsFromCrd && destinations.IsNullOrEmpty())
            {
                _logger.Log(LogLevel.Warning, "No DICOM SCU destination configured: DicomAdapter>dicom>scu>destinations.");
            }
            else if (readDestinationsFromCrd)
            {
                destinations.Clear();
                _logger.Log(LogLevel.Information, "Destination AE Titles will be read from Kubernetes Custom Resource.");
            }

            foreach (var dest in destinations)
            {
                valid &= IsDestinationValid(dest);
            }

            return valid;
        }

        public bool IsDestinationValid(DestinationApplicationEntity dest)
        {
            Guard.Against.Null(dest, nameof(dest));

            var valid = true;
            valid &= IsAeTitleValid("DicomAdapter>dicom>scu>destinations", dest.AeTitle);
            valid &= IsValidHostNameIp($"DicomAdapter>dicom>scu>destinations>{dest.AeTitle}", dest.HostIp);
            valid &= IsPortValid($"DicomAdapter>dicom>scu>destinations>{dest.AeTitle}", dest.Port);
            return valid;
        }

        private bool AreScpSourcesValid(ScpConfiguration scp, bool readSourcesFromCrd)
        {
            var valid = true;
            if (scp.Sources.IsNullOrEmpty())
            {
                if (scp.RejectUnknownSources && !readSourcesFromCrd)
                {
                    _logger.Log(LogLevel.Error, "No DICOM SCP source configured: DicomAdapter>dicom>scp>sources and reject-unknown-sources is on.");
                    valid = false;
                }
                else if (scp.RejectUnknownSources && readSourcesFromCrd)
                {
                    scp.Sources.Clear();
                    _logger.Log(LogLevel.Information, "DICOM Source AE Titles will be read from Kubernetes Custom Resource.");
                }
                else
                {
                    _logger.Log(LogLevel.Warning, "No DICOM SCP source configured: DicomAdapter>dicom>scp>sources. All associations will be accepted.");
                }
            }

            foreach (var source in scp.Sources)
            {
                valid &= IsSourceValid(source);
            }

            return valid;
        }

        public bool IsSourceValid(SourceApplicationEntity source)
        {
            Guard.Against.Null(source, nameof(source));

            var valid = true;
            valid &= IsAeTitleValid("DicomAdapter>dicom>scp>sources", source.AeTitle);
            valid &= IsValidHostNameIp($"DicomAdapter>dicom>scp>sources>{source.AeTitle}", source.HostIp);
            return valid;
        }

        private bool AreAeTitlesValid(string source, IList<ClaraApplicationEntity> aeTitles, bool readAeTitlesFromCrd)
        {
            bool valid = true;

            if (!readAeTitlesFromCrd && aeTitles.IsNullOrEmpty())
            {
                _logger.Log(LogLevel.Error, "No AE Titles defined in {0}.", source);
                valid = false;
                return valid;
            }
            else if (readAeTitlesFromCrd)
            {
                aeTitles.Clear();
                _logger.Log(LogLevel.Information, "Local AE Titles will be read from Kubernetes Custom Resource.");
            }

            foreach (var app in aeTitles)
            {
                valid &= IsClaraAeTitleValid(aeTitles, source, app);
                if (!valid)
                {
                    break;
                }
            }
            return valid;
        }

        public bool IsClaraAeTitleValid(IList<ClaraApplicationEntity> aeTitles, string source, ClaraApplicationEntity entity, bool validateCrd = false)
        {
            Guard.Against.Null(source, nameof(source));
            Guard.Against.Null(entity, nameof(entity));

            var valid = true;
            valid &= IsAeTitleValid(source, entity.AeTitle);

            var validationCount = validateCrd ? 0 : 1;

            if (aeTitles.Count(p => p.AeTitle.Equals(entity.AeTitle, StringComparison.Ordinal)) > validationCount)
            {
                _logger.Log(LogLevel.Error, $"DicomAdapter>dicom>scp>ae-titles>{entity.AeTitle} already exists.");
                valid = false;
            }

            return valid;
        }

        private bool IsAeTitleValid(string source, string aeTitle)
        {
            if (!string.IsNullOrWhiteSpace(aeTitle) && aeTitle.Length <= 15) return true;

            _logger.Log(LogLevel.Error, "Invalid AE Title '{0}' specified in {1}.", aeTitle, source);
            return false;
        }

        private bool IsValidHostNameIp(string source, string hostIp)
        {
            if (!string.IsNullOrWhiteSpace(hostIp)) return true;

            _logger.Log(LogLevel.Error, "Invalid host name/IP address '{0}' specified in {1}.", hostIp, source);
            return false;
        }

        private bool IsValueInRange(string source, int minValue, int maxValue, int actualValue)
        {
            if (actualValue >= minValue && actualValue <= maxValue) return true;

            _logger.Log(LogLevel.Error, "Value of {0} must be between {1} and {2}.", source, minValue, maxValue);
            return false;
        }
    }
}