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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Configuration.Test
{
    public class ConfigurationValidatorTest
    {
        private Mock<ILogger<ConfigurationValidator>> logger;

        public ConfigurationValidatorTest()
        {
            logger = new Mock<ILogger<ConfigurationValidator>>();
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with all valid settings")]
        public void AllValid()
        {
            var config = MockValidConfiguration();
            var valid = new ConfigurationValidator(logger.Object).Validate("", config);
            Assert.True(valid == ValidateOptionsResult.Success);
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with invalid SCP port number")]
        public void InvalidScpPort()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.Port = Int32.MaxValue;

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            var validationMessage = $"Invalid port number '{Int32.MaxValue}' specified for DicomAdapter>dicom>scp>port.";
            Assert.Equal(validationMessage, valid.FailureMessage);
            logger.VerifyLogging(validationMessage, LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with invalid maximum number of associations")]
        public void InvalidScpMaxAssociations()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.MaximumNumberOfAssociations = 0;

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            var validationMessage = $"Value of DicomAdapter>dicom>scp>max-associations must be between {1} and {1000}.";
            Assert.Equal(validationMessage, valid.FailureMessage);
            logger.VerifyLogging(validationMessage, LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with no verification transfer syntaxes")]
        public void EmptyVerificationTransferSyntax()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.Verification.TransferSyntaxes.Clear();

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            var validationMessage = $"No transfer syntax configured for verification service: DicomAdapter>dicom>scp>verification>transfer-syntaxes.";
            Assert.Equal(validationMessage, valid.FailureMessage);
            logger.VerifyLogging(validationMessage, LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with invalid transfer syntax UID for verification service")]
        public void InvalidVerificationTransferSyntax()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.Verification.TransferSyntaxes.Clear();
            config.Dicom.Scp.Verification.TransferSyntaxes.Add("1.2.3");

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            var validationMessage = $"Invalid Transfer Syntax UID found in DicomAdapter>dicom>scp>verification>transfer-syntaxes.";
            Assert.Equal(validationMessage, valid.FailureMessage);
            logger.VerifyLogging(validationMessage, LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with missing results service endpoint")]
        public void ServicesWithNullResultServiceEndpoint()
        {
            var config = MockValidConfiguration();
            config.Services.ResultsServiceEndpoint = " ";

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            var validationMessage = $"Results Service API endpoint is not configured or invalid: DicomAdapter>services>results-service-endpoint.";
            Assert.Equal(validationMessage, valid.FailureMessage);
            logger.VerifyLogging(validationMessage, LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with malformed results service endpoint")]
        public void ServicesWithMalformedResultServiceEndpoint()
        {
            var config = MockValidConfiguration();
            config.Services.ResultsServiceEndpoint = "http://www.contoso.com/path???/file name";

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            var validationMessage = $"Results Service API endpoint is not configured or invalid: DicomAdapter>services>results-service-endpoint.";
            Assert.Equal(validationMessage, valid.FailureMessage);
            logger.VerifyLogging(validationMessage, LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with missing platform endpoint")]
        public void ServicesWithMissingPlatformEndpoint()
        {
            var config = MockValidConfiguration();
            config.Services.Platform.Endpoint = null;

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            var validationMessage = $"Clara Service API endpoint is not configured: DicomAdapter>services>platform>endpoint.";
            Assert.Equal(validationMessage, valid.FailureMessage);
            logger.VerifyLogging(validationMessage, LogLevel.Error, Times.Once());
        }

        private DicomAdapterConfiguration MockValidConfiguration()
        {
            var config = new DicomAdapterConfiguration();

            config.Dicom.Scp.RejectUnknownSources = true;
            config.Services.Platform.Endpoint = "host:port";
            config.Services.ResultsServiceEndpoint = "http://1.2.3.4:8080/bla-bla";
            return config;
        }
    }
}