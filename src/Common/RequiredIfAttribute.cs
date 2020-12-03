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
using System;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Reflection;

namespace Nvidia.Clara.Dicom.Common
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class RequiredIfAttribute : ValidationAttribute
    {
        /// <summary>
        /// Gets or sets the name of the property to be used during validation.
        /// </summary>
        public string Property { get; private set; }

        /// <summary>
        /// Gets or sets the property value that will be relevant for validation.
        /// </summary>
        public object PropertyValue { get; private set; }

        public override bool RequiresValidationContext
        {
            get { return true; }
        }

        /// <summary>
        /// Initializes an instance of the <see cref="RequiredIfAttribute"/> class.
        /// </summary>
        /// <param name="property">The other property.</param>
        /// <param name="propertyValue">The other property value.</param>
        public RequiredIfAttribute(string property, object propertyValue)
            : base("'{0}' is required because '{1}' has a value '{2}'.")
        {
            Property = property;
            PropertyValue = propertyValue;
        }

        /// <summary>
        /// Applies formatting to an error message, based on the data field where the error occurred.
        /// </summary>
        /// <param name="name">The name to include in the formatted message.</param>
        /// <returns>
        /// An instance of the formatted error message.
        /// </returns>
        public override string FormatErrorMessage(string name)
        {
            return string.Format(
                CultureInfo.CurrentCulture,
                base.ErrorMessageString,
                name,
                Property ?? Property,
                PropertyValue);
        }

        /// <summary>
        /// Validates the specified value with respect to the current validation attribute.
        /// </summary>
        /// <param name="value">The value to validate.</param>
        /// <param name="validationContext">The context information about the validation operation.</param>
        /// <returns>
        /// An instance of the <see cref="T:System.ComponentModel.DataAnnotations.ValidationResult" /> class.
        /// </returns>
        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            Guard.Against.Null(validationContext, nameof(validationContext));

            PropertyInfo otherProperty = validationContext.ObjectType.GetProperty(Property);
            if (otherProperty == null)
            {
                throw new ArgumentNullException(
                    string.Format(CultureInfo.CurrentCulture, "Could not find a property named '{0}'.", Property));
            }

            object otherValue = otherProperty.GetValue(validationContext.ObjectInstance);

            if (object.Equals(otherValue, PropertyValue))
            {
                if (value == null)
                {
                    return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
                }

                if (value is string val && string.IsNullOrWhiteSpace(val))
                {
                    return new ValidationResult(FormatErrorMessage(validationContext.DisplayName));
                }
            }

            return ValidationResult.Success;
        }
    }
}