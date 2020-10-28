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
using System.Runtime.Serialization;

namespace Nvidia.Clara.Dicom.DicomWeb.Client.Common
{
    public class UnsupportedReturnTypeException : Exception
    {
        public UnsupportedReturnTypeException()
        {
        }

        public UnsupportedReturnTypeException(string message) : base(message)
        {
        }

        public UnsupportedReturnTypeException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected UnsupportedReturnTypeException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
