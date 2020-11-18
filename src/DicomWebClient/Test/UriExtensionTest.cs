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

using Nvidia.Clara.Dicom.DicomWeb.Client.Common;
using System;
using Xunit;

namespace Nvidia.Clara.Dicom.DicomWebClient.Test
{
    public class UriExtensionTest
    {
        [Fact(DisplayName = "EnsureUriEndsWithSlash shall throw when input is null")]
        public void EnsureUriEndsWithSlash_Null()
        {
            Uri input = null;
            Assert.Throws<ArgumentNullException>(() => input.EnsureUriEndsWithSlash());
        }

        [Fact(DisplayName = "EnsureUriEndsWithSlash shall append slash to end")]
        public void EnsureUriEndsWithSlash_AppendSlash()
        {
            Uri input = new Uri("http://1.2.3.4/api");

            var output = input.EnsureUriEndsWithSlash();

            Assert.EndsWith("/", output.ToString());
        }

        [Fact(DisplayName = "EnsureUriEndsWithSlash shall not append slash to end")]
        public void EnsureUriEndsWithSlash_DoesNotAppendSlash()
        {
            Uri input = new Uri("http://1.2.3.4/api/");

            var output = input.EnsureUriEndsWithSlash();

            Assert.EndsWith("/", output.ToString());
            Assert.False(output.ToString().EndsWith("//"));
        }
    }
}