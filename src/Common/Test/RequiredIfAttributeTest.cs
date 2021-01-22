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

using Nvidia.Clara.Dicom.Common;
using System;
using System.ComponentModel.DataAnnotations;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Common.Test
{
    public class RequiredIfAttributeTest
    {
        [Fact(DisplayName = "Shall throw if validation context is null")]
        public void ShallThrowIfValidationContextIsNull()
        {
            var attr = new RequiredIfAttribute("test", "value");

            Assert.Throws<ArgumentNullException>(() =>
            {
                attr.GetValidationResult("value", null);
            });
        }

        [Fact(DisplayName = "Shall throw if property is not found")]
        public void ShallThrowIfPropertyIsNotFound()
        {
            var model = new Mock { Property = null };
            var context = new ValidationContext(model);

            var attr = new RequiredIfAttribute("UnknownProperty", "value");

            Assert.Throws<ArgumentNullException>(() =>
            {
                attr.GetValidationResult("value", context);
            });
        }

        [Fact(DisplayName = "Invalid if value is null")]
        public void InvalidIfValueIsNull()
        {
            var model = new Mock { Property = "value" };
            var context = new ValidationContext(model);

            var attr = new RequiredIfAttribute("Property", "value");

            var result = attr.GetValidationResult(null, context);

            Assert.Equal("'Mock' is required because 'Property' has a value 'value'.", result.ErrorMessage);
        }

        [Fact(DisplayName = "Invalid if value is null or empty string")]
        public void InvalidIfValueIsNullOrEmptyString()
        {
            var model = new Mock { Property = "value" };
            var context = new ValidationContext(model);

            var attr = new RequiredIfAttribute("Property", "value");

            var result = attr.GetValidationResult(" ", context);

            Assert.Equal("'Mock' is required because 'Property' has a value 'value'.", result.ErrorMessage);

            result = attr.GetValidationResult(null, context);

            Assert.Equal("'Mock' is required because 'Property' has a value 'value'.", result.ErrorMessage);
        }

        [Fact(DisplayName = "Successful validation with targeted values")]
        public void SuccessfulValidationWithTargetedValue()
        {
            var model = new Mock { Property = "value" };
            var context = new ValidationContext(model);

            var attr = new RequiredIfAttribute("Property", "value");

            var result = attr.GetValidationResult("value", context);

            Assert.Equal(ValidationResult.Success, result);
        }

        [Fact(DisplayName = "Successful validation without targeted value")]
        public void SuccessfulValidationWithoutTargetedValue()
        {
            var model = new Mock { Property = "not the expected target" };
            var context = new ValidationContext(model);

            var attr = new RequiredIfAttribute("Property", "value");

            var result = attr.GetValidationResult("value", context);

            Assert.Equal(ValidationResult.Success, result);
        }

        private class Mock
        {
            public string Property { get; set; }
        }
    }
}