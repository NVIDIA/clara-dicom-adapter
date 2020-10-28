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
using System.Linq;
using Dicom;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// An attribute that is attached to a derived class of <see cref="JobProcessorBase"/>.
    /// This attribute is used when the Create Clara AE Title (RESTful) API is called.
    /// The API looks up the passed in value of Job Processor and reads the associated attribute 
    /// to determine and instantiate <see cref="IJobProcessValidator"/> for validating process settings.
    /// </summary>
    [System.AttributeUsage(System.AttributeTargets.Class)]
    public class ProcessorValidationAttribute : System.Attribute
    {
        public Type ValidatorType { get; set; }
    }
}
