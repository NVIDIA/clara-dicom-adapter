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

namespace Nvidia.Clara.Dicom.DicomWeb.Client.Common
{
    public static class UriExtensions
    {
        public static Uri EnsureUriEndsWithSlash(this Uri input)
        {
            Guard.Against.MalformUri(input, nameof(input));

            var str = input.ToString();

            if (!string.IsNullOrWhiteSpace(str) && !str.EndsWith('/'))
            {
                return new Uri(str + '/');
            }

            return input;
        }
    }
}