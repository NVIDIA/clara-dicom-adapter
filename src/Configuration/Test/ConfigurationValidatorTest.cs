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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nvidia.Clara.DicomAdapter.Test.Shared;
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

        [RetryFact(DisplayName = "ConfigurationValidator test with duplicate Clara AE titles")]
        public void DuplicateClaraAeTitles()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.AeTitles.Add(new ClaraApplicationEntity
            {
                AeTitle = "AET",
                Name = "AET",
                ProcessorSettings = new System.Collections.Generic.Dictionary<string, string>() { { "key1", "value1" } }
            });

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);
            Assert.False(valid == ValidateOptionsResult.Success);
            logger.VerifyLogging($"DicomAdapter>dicom>scp>ae-titles>AET already exists.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with invalid SCP port number")]
        public void InvalidScpPort()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.Port = Int32.MaxValue;

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"Invalid port number '{Int32.MaxValue}' specified in DicomAdapter>dicom>scp>port.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with empty SCP AE Titles")]
        public void InvalidScpAeTitle_EmptyString()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.AeTitles.Clear();

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"No AE Titles defined in DicomAdapter>dicom>scp>ae-title.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with AE Title exceeding 16 chars limit")]
        public void InvalidScpAeTitle_ExceedsLength()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.AeTitles.Clear();
            config.Dicom.Scp.AeTitles.Add(new ClaraApplicationEntity { AeTitle = "12345678901234567890" });

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"Invalid AE Title '{config.Dicom.Scp.AeTitles[0].AeTitle}' specified in DicomAdapter>dicom>scp>ae-title.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with invalid maximum number of associations")]
        public void InvalidScpMaxAssociations()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.MaximumNumberOfAssociations = 0;

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"Value of DicomAdapter>dicom>scp>max-associations must be between {1} and {Int32.MaxValue}.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with no verification transfer syntaxes")]
        public void EmptyVerificationTransferSyntax()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.Verification.TransferSyntaxes.Clear();

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"No transfer syntax configured for verification service: DicomAdapter>dicom>scp>verification>transfer-syntaxes.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with invalid transfer syntax UID for verification service")]
        public void InvalidVerificationTransferSyntax()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.Verification.TransferSyntaxes.Clear();
            config.Dicom.Scp.Verification.TransferSyntaxes.Add("1.2.3");

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"Invalid Transfer Syntax UID found in DicomAdapter>dicom>scp>verification>transfer-syntaxes.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test when no sources is specified and rejects unknown sources")]
        public void NoSourcesWithRejectUnknown()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scp.RejectUnknownSources = true;
            config.Dicom.Scp.Sources.Clear();

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"No DICOM SCP source configured: DicomAdapter>dicom>scp>sources and reject-unknown-sources is on.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test when no sources defined but allows unknown sources")]
        public void NoSourcesWithAcceptUnknown_IsStillValid()
        {
            var config = MockValidConfiguration();

            config.Dicom.Scp.RejectUnknownSources = false;
            config.Dicom.Scp.Sources.Clear();

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.True(valid == ValidateOptionsResult.Success);
            logger.VerifyLogging($"No DICOM SCP source configured: DicomAdapter>dicom>scp>sources. All associations will be accepted.", LogLevel.Warning, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test when reading sources from CRD which is OK")]
        public void ReadSourcesFromCrd_IsStillValid()
        {
            var config = MockValidConfiguration();
            config.ReadAeTitlesFromCrd = true;
            config.Dicom.Scp.Sources.Clear();

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.True(valid == ValidateOptionsResult.Success);
            logger.VerifyLogging($"DICOM Source AE Titles will be read from Kubernetes Custom Resource.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with invalid source settings")]
        public void ScpSourceWithInvalidValues()
        {
            var config = MockValidConfiguration();

            config.Dicom.Scp.Sources[0].AeTitle = "   ";
            config.Dicom.Scp.Sources[0].HostIp = "   ";

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"Invalid AE Title '{config.Dicom.Scp.Sources[0].AeTitle}' specified in DicomAdapter>dicom>scp>sources.", LogLevel.Error, Times.Once());
            logger.VerifyLogging($"Invalid host name/IP address '{config.Dicom.Scp.Sources[0].HostIp}' specified in DicomAdapter>dicom>scp>sources>{config.Dicom.Scp.Sources[0].AeTitle}.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with reading destinations from CRDwhich is still OK.")]
        public void ReadDestinationsFromCrd_IsStillValid()
        {
            var config = MockValidConfiguration();
            config.ReadAeTitlesFromCrd = true;

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.True(valid == ValidateOptionsResult.Success);
            logger.VerifyLogging($"Destination AE Titles will be read from Kubernetes Custom Resource.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with no destinations defined which is still OK.")]
        public void NoDestinations_IsStillValid()
        {
            var config = MockValidConfiguration();
            config.Dicom.Scu.Destinations.Clear();

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.True(valid == ValidateOptionsResult.Success);
            logger.VerifyLogging($"No DICOM SCU destination configured: DicomAdapter>dicom>scu>destinations.", LogLevel.Warning, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with missing results service endpoint")]
        public void ServicesWithNullResultServiceEndpoint()
        {
            var config = MockValidConfiguration();
            config.Services.ResultsServiceEndpoint = " ";

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"Results Service API endpoint is not configured or invalid: DicomAdapter>services>results-service-endpoint.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with malformed results service endpoint")]
        public void ServicesWithMalformedResultServiceEndpoint()
        {
            var config = MockValidConfiguration();
            config.Services.ResultsServiceEndpoint = "htp://zzz.com:";

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"Results Service API endpoint is not configured or invalid: DicomAdapter>services>results-service-endpoint.", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ConfigurationValidator test with missing platform endpoint")]
        public void ServicesWithMissingPlatformEndpoint()
        {
            var config = MockValidConfiguration();
            config.Services.PlatformEndpoint = null;

            var valid = new ConfigurationValidator(logger.Object).Validate("", config);

            Assert.Equal("See console output for details.", valid.FailureMessage);
            logger.VerifyLogging($"Clara Service API endpoint is not configured: DicomAdapter>services>platform-endpoint.", LogLevel.Error, Times.Once());
        }

        private DicomAdapterConfiguration MockValidConfiguration()
        {
            var config = new DicomAdapterConfiguration();

            config.ReadAeTitlesFromCrd = false;
            config.Dicom.Scp.RejectUnknownSources = true;
            config.Dicom.Scp.AeTitles.Add(new ClaraApplicationEntity
            {
                AeTitle = "AET",
                Name = "AET",
                ProcessorSettings = new System.Collections.Generic.Dictionary<string, string>() { { "key1", "value1" } }
            });

            config.Dicom.Scu.Destinations.Add(new DestinationApplicationEntity
            {
                Name = "Dest1",
                AeTitle = "Dest1",
                HostIp = "Dest1",
                Port = 123
            });

            config.Dicom.Scp.Sources.Add(new SourceApplicationEntity
            {
                AeTitle = "Src1",
                HostIp = "Src1"
            });

            config.Dicom.Scp.AeTitles.Add(new ClaraApplicationEntity { });

            config.Services.PlatformEndpoint = "host:port";
            config.Services.ResultsServiceEndpoint = "http://1.2.3.4:8080/bla-bla";
            return config;
        }
    }
}
