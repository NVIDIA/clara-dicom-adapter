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

using Dicom;
using System.Collections.Generic;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Common.Test
{
    public class DicomExtensionsTest
    {
        [RetryFact(DisplayName = "DicomExtensions.ToDicomTransferSyntaxArray test with all valid transfer syntaxes")]
        public void ToDicomTransferSyntaxArrayWithValidUids()
        {
            var uids = new List<string>() {
                "1.2.840.10008.1.2",
                "1.2.840.10008.1.2.1",
                "1.2.840.10008.1.2.1.99",
                "1.2.840.10008.1.2.2",
                "1.2.840.10008.1.2.4.50",
                "1.2.840.10008.1.2.4.51",
                "1.2.840.10008.1.2.4.52",
                "1.2.840.10008.1.2.4.53",
                "1.2.840.10008.1.2.4.54",
                "1.2.840.10008.1.2.4.55",
                "1.2.840.10008.1.2.4.56",
                "1.2.840.10008.1.2.4.57",
                "1.2.840.10008.1.2.4.58",
                "1.2.840.10008.1.2.4.59",
                "1.2.840.10008.1.2.4.60",
                "1.2.840.10008.1.2.4.61",
                "1.2.840.10008.1.2.4.62",
                "1.2.840.10008.1.2.4.63",
                "1.2.840.10008.1.2.4.64",
                "1.2.840.10008.1.2.4.65",
                "1.2.840.10008.1.2.4.66",
                "1.2.840.10008.1.2.4.70",
                "1.2.840.10008.1.2.4.80",
                "1.2.840.10008.1.2.4.81",
                "1.2.840.10008.1.2.4.90",
                "1.2.840.10008.1.2.4.91",
                "1.2.840.10008.1.2.4.92",
                "1.2.840.10008.1.2.4.93",
                "1.2.840.10008.1.2.4.94",
                "1.2.840.10008.1.2.4.95",
                "1.2.840.10008.1.2.5",
                "1.2.840.10008.1.2.6.1",
                "1.2.840.10008.1.2.4.100",
                "1.2.840.10008.1.2.4.102",
                "1.2.840.10008.1.2.4.103"
            };

            var result = uids.ToDicomTransferSyntaxArray();

            Assert.Equal(uids.Count, result.Length);
        }

        [RetryFact(DisplayName = "DicomExtensions.ToDicomTransferSyntaxArray test with bad transfer syntaxes")]
        public void ToDicomTransferSyntaxArrayWithInvalidUids()
        {
            var uids = new List<string>() {
                "1"
            };

            Assert.Throws<DicomDataException>(() => uids.ToDicomTransferSyntaxArray());
        }

        [RetryFact(DisplayName = "DicomExtensions.ToDicomTransferSyntaxArray test with empty input")]
        public void ToDicomTransferSyntaxArrayWithEmptyList()
        {
            var uids = new List<string>() { };

            var result = uids.ToDicomTransferSyntaxArray();

            Assert.Null(result);
        }

        [RetryFact(DisplayName = "DicomExtensions.ToDicomTransferSyntaxArray test with null input")]
        public void ToDicomTransferSyntaxArrayWithNullInput()
        {
            var result = DicomExtensions.ToDicomTransferSyntaxArray(null);

            Assert.Null(result);
        }
    }
}