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
using System.Threading.Tasks;
using FluentAssertions;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Xunit;
using xRetry;

namespace Nvidia.Clara.DicomAdapter.Test.Integration
{
    [Collection("DICOM Adapter")]
    public class CEchoTest : IAsyncDisposable
    {
        private static string AE_CECHOTEST = "CECHOTEST";
        public DicomAdapterFixture Fixture { get; }

        public CEchoTest(DicomAdapterFixture fixture)
        {
            Fixture = fixture;
        }

        [RetryFact(DisplayName = "C-ECHO from unknown source AE Title")]
        public void CEchoFromUnknownSourceAeTitle()
        {
            int exitCode = 0;
            var output = DcmtkLauncher.EchoScu($"-aet UNKNOWNSCU -aec {AE_CECHOTEST}", out exitCode);
            Assert.Equal(1, exitCode);

            output.Where(p => p == "F: Reason: Calling AE Title Not Recognized").Should().HaveCount(1);
        }

        [RetryTheory(DisplayName = "C-ECHO from known source AE Title")]
        [InlineData("PACS1")]
        [InlineData("PACS2")]
        public void CEchoFromKnownSourceAeTitle(string sourceAeTitle)
        {
            int exitCode = 0;
            var output = DcmtkLauncher.EchoScu($"-aet {sourceAeTitle} -aec {AE_CECHOTEST}", out exitCode);
            Assert.Equal(0, exitCode);

            output.Where(p => p.Contains("I: Association Accepted")).Should().HaveCount(1);
        }

        [RetryFact(DisplayName = "C-ECHO to wrong AE Title")]
        public void CEchoToWrongAeTitle()
        {
            int exitCode = 0;
            var output = DcmtkLauncher.EchoScu($"-aet PACS1 -aec blabla", out exitCode);
            Assert.Equal(1, exitCode);
            output.Where(p => p == "F: Reason: Called AE Title Not Recognized").Should().HaveCount(1);
        }

        [RetryFact(DisplayName = "C-ECHO Abort Association")]
        public void CEchoAbortAssociation()
        {
            int exitCode = 0;
            var output = DcmtkLauncher.EchoScu($"-aet PACS1 -aec {AE_CECHOTEST} --abort", out exitCode);
            Assert.Equal(0, exitCode);

            output.Where(p => p == "I: Aborting Association").Should().HaveCount(1);
        }

        public async ValueTask DisposeAsync()
        {
            await Fixture.DisposeAsync();
        }
    }
}
