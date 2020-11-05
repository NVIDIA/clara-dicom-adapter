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
using Moq;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Processors;
using System;
using System.Collections.Generic;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class AeTitleJobProcessorValidatorTest
    {
        private Mock<ILogger<AeTitleJobProcessorValidator>> _logger;

        public AeTitleJobProcessorValidatorTest()
        {
            _logger = new Mock<ILogger<AeTitleJobProcessorValidator>>();
        }

        [Fact(DisplayName = "Constructor - shall throw with no logger")]
        public void Constructor_ThrowsIfNoLogger()
        {
            Assert.Throws<ArgumentNullException>(() => new AeTitleJobProcessorValidator(null));
        }

        [Fact(DisplayName = "Validate - shall throw with no AE title")]
        public void Validate_ThrowsWithNullEmptyAeTitle()
        {
            var validator = new AeTitleJobProcessorValidator(_logger.Object);
            Assert.Throws<ArgumentNullException>(() => validator.Validate(null, new Dictionary<string, string>()));
            Assert.Throws<ArgumentException>(() => validator.Validate(" ", new Dictionary<string, string>()));
        }

        [Fact(DisplayName = "Validate - shall throw when processorSettings is null")]
        public void Validate_ShallThrowWithNullProcessorSettings()
        {
            var validator = new AeTitleJobProcessorValidator(_logger.Object);
            Assert.Throws<ArgumentNullException>(() => validator.Validate("aet", null));
        }

        [Fact(DisplayName = "Validate - shall throw with bad timeout value")]
        public void Validate_ShallThrowWithBadTimeoutValue()
        {
            var validator = new AeTitleJobProcessorValidator(_logger.Object);
            var settings = new Dictionary<string, string>();
            settings.Add("timeout", "a");
            Assert.Throws<ConfigurationException>(() => validator.Validate("aet", settings));

            settings["timeout"] = "-1";
            Assert.Throws<ConfigurationException>(() => validator.Validate("aet", settings));
        }

        [Fact(DisplayName = "Validate - shall throw with bad jobRetryDelay value")]
        public void Validate_ShallThrowWithBadJobRetryDelayValue()
        {
            var validator = new AeTitleJobProcessorValidator(_logger.Object);
            var settings = new Dictionary<string, string>();
            settings.Add("jobRetryDelay", "a");
            Assert.Throws<ConfigurationException>(() => validator.Validate("aet", settings));

            settings["jobRetryDelay"] = "-1";
            Assert.Throws<ConfigurationException>(() => validator.Validate("aet", settings));
        }

        [Fact(DisplayName = "Validate - shall throw with bad priority value")]
        public void Validate_ShallThrowWithBadJoPriorityValue()
        {
            var validator = new AeTitleJobProcessorValidator(_logger.Object);
            var settings = new Dictionary<string, string>();
            settings.Add("priority", "SonicSpeed");
            Assert.Throws<ConfigurationException>(() => validator.Validate("aet", settings));

            settings["priority"] = "-1";
            Assert.Throws<ConfigurationException>(() => validator.Validate("aet", settings));
        }

        [Fact(DisplayName = "Validate - shall throw with bad groupBy value")]
        public void Validate_ShallThrowWithBadGroupByyValue()
        {
            var validator = new AeTitleJobProcessorValidator(_logger.Object);
            var settings = new Dictionary<string, string>();
            settings.Add("groupBy", "0000000");
            Assert.Throws<ConfigurationException>(() => validator.Validate("aet", settings));

            settings["groupBy"] = "-1";
            Assert.Throws<ConfigurationException>(() => validator.Validate("aet", settings));
        }

        [Fact(DisplayName = "Validate - shall throw with no pipelines defined")]
        public void Validate_ShallThrowWithNoPipelinesDefined()
        {
            var validator = new AeTitleJobProcessorValidator(_logger.Object);
            var settings = new Dictionary<string, string>();
            Assert.Throws<ConfigurationException>(() => validator.Validate("aet", settings));
        }

        [Fact(DisplayName = "Validate - throw if settings are not recognized")]
        public void Validate_ShallThrowIfSettingsAreNotRecognized()
        {
            var validator = new AeTitleJobProcessorValidator(_logger.Object);
            var settings = new Dictionary<string, string>();
            settings.Add("unknown", "abc");
            settings.Add("time out", "typoe");
            Assert.Throws<ConfigurationException>(() => validator.Validate("aet", settings));
        }

        [Fact(DisplayName = "Validate - passes validation")]
        public void Validate_PassesValidation()
        {
            var validator = new AeTitleJobProcessorValidator(_logger.Object);
            var settings = new Dictionary<string, string>();
            settings.Add("timeout", "100");
            settings.Add("jobRetryDelay", "100");
            settings.Add("priority", "higher");
            settings.Add("groupBy", "00100010");
            settings.Add("pipeline-one", "ABCDEFGH");
            validator.Validate("aet", settings);

            settings["priority"] = "Higher";
            settings["groupBy"] = "0010,0010";
            validator.Validate("aet", settings);
        }
    }
}