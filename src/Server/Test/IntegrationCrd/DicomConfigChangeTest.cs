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

using FluentAssertions;
using k8s.Models;
using Microsoft.Rest;
using Moq;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.Platform;
using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.IntegrationCrd
{
    public class DicomConfigChangeTest : IClassFixture<DicomAdapterFixture>, IClassFixture<ScpTestFileSetsFixture>
    {
        private readonly DicomAdapterFixture _dicomAdapterFixture;
        private readonly TestFileSetsFixture _testFileSetsFixture;

        public DicomConfigChangeTest(DicomAdapterFixture dicomAdapterFixture, ScpTestFileSetsFixture testFileSetsFixture)
        {
            _dicomAdapterFixture = dicomAdapterFixture;
            _testFileSetsFixture = testFileSetsFixture;

            dicomAdapterFixture.Jobs.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>()))
                .ReturnsAsync(new API.Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });

            dicomAdapterFixture.Jobs.Setup(p => p.Start(It.IsAny<API.Job>()))
                .Returns(Task.CompletedTask);

            dicomAdapterFixture.Payloads.Setup(p => p.Download(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new API.PayloadFile { Name = Guid.NewGuid().ToString() }));

            dicomAdapterFixture.Payloads.Setup(p => p.Upload(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IEnumerable<string>>()))
                .Returns(Task.CompletedTask);

            dicomAdapterFixture.ResultsService.Setup(p => p.GetPendingJobs(It.IsAny<CancellationToken>(), It.IsAny<int>()))
                .Returns(Task.FromResult((IList<TaskResponse>)new List<TaskResponse>()));

            dicomAdapterFixture.ResultsService.Setup(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));

            dicomAdapterFixture.ResultsService.Setup(p => p.ReportFailure(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
                .Returns(Task.FromResult(true));
        }

        [RetryFact(DisplayName = "SCP Service shall handle config changes")]
        public async Task ScpShallHandleConfigurationChanges()
        {
            // initial setup with out any local AE TItle and source
            await InitialState();
            // add a valid source
            _dicomAdapterFixture.ResetAllHandles();
            await AddSourceWithoutValidLocalAeTittle();
            // add a valid local AE Title
            _dicomAdapterFixture.ResetAllHandles();
            await AddLocalAeTitle();
            // add another source and makes sure both sources work
            _dicomAdapterFixture.ResetAllHandles();
            await AddAnotherSource();
            // remove one source
            _dicomAdapterFixture.ResetAllHandles();
            await RemoveOneSource();
            // remove existing local AET and add a new one
            _dicomAdapterFixture.ResetAllHandles();
            await ReplaceLocalAeTitle();
            // add a few destinations
            await AddDestinations();
            // remove a couple destinations
            await RemoveDestinations();
        }

        private async Task VerifyWithRestAPIs(int claraAeTitleCount, int sourceCount, int destinationCount)
        {
            var httpClient = new HttpClient();
            httpClient.BaseAddress = new Uri("http://localhost:5000");
            var claraAeTitlesJson = await httpClient.GetStringAsync("api/config/ClaraAeTitle/");
            var claraAeTitles = JsonConvert.DeserializeObject<ClaraApplicationEntityCustomResourceList>(claraAeTitlesJson);
            Assert.Equal(claraAeTitleCount, claraAeTitles.Items.Count);

            var sourceAeTitlesJson = await httpClient.GetStringAsync("api/config/SourceAeTitle/");
            var sourceAeTitles = JsonConvert.DeserializeObject<SourceApplicationEntityCustomResourceList>(sourceAeTitlesJson);
            Assert.Equal(sourceCount, sourceAeTitles.Items.Count);

            var destAeTitlesJson = await httpClient.GetStringAsync("api/config/DestinationAeTitle/");
            var destAeTitles = JsonConvert.DeserializeObject<DestinationApplicationEntityCustomResourceList>(destAeTitlesJson);
            Assert.Equal(destinationCount, destAeTitles.Items.Count);
        }

        private async Task RemoveDestinations()
        {
            var localAeTitle = "ClaraSCP2";
            var sourceAeTitle = "PACS";

            SetupLocalAe(localAeTitle);
            SetupSource(sourceAeTitle);
            SetupDestination("dest3");
            _dicomAdapterFixture.WaitForAllEventHandles(10000);
            VerifyConfigChanges(1, 1, 1);
            await VerifyWithRestAPIs(1, 1, 1);
        }

        private async Task AddDestinations()
        {
            var localAeTitle = "ClaraSCP2";
            var sourceAeTitle = "PACS";

            SetupLocalAe(localAeTitle);
            SetupSource(sourceAeTitle);
            SetupDestination("dest1", "dest2", "dest3");
            _dicomAdapterFixture.WaitForAllEventHandles(10000);
            VerifyConfigChanges(1, 1, 3);
            await VerifyWithRestAPIs(1, 1, 3);
        }

        private async Task ReplaceLocalAeTitle()
        {
            var testCase = "1-scp-explicitVrLittleEndian";

            var localAeTitleOld = "ClaraSCP1";
            var localAeTitleNew = "ClaraSCP2";
            var sourceAeTitle = "PACS";

            SetupLocalAe(localAeTitleNew);
            SetupSource(sourceAeTitle);
            SetupDestination();

            _dicomAdapterFixture.WaitForAllEventHandles(10000);
            // Verify client side
            var output = SendData(testCase, sourceAeTitle, localAeTitleOld, 1);
            VerifyAllCrdCallsWereMade();
            VerifyClientLogs(output, "F: Reason: Called AE Title Not Recognized", 1);

            output = SendData(testCase, sourceAeTitle, localAeTitleNew, 0);
            VerifyClientLogs(output, "I: Received Store Response (Success)", 3);
            VerifyConfigChanges(1, 1, 0);
            await VerifyWithRestAPIs(1, 1, 0);
        }

        private async Task RemoveOneSource()
        {
            var testCase = "1-scp-explicitVrLittleEndian";

            var localAeTitle = "ClaraSCP1";
            var sourceAeTitle1 = "GoodSource";
            var sourceAeTitle2 = "PACS";

            SetupLocalAe(localAeTitle);
            SetupSource(sourceAeTitle2);
            SetupDestination();

            _dicomAdapterFixture.WaitForAllEventHandles(10000);
            // Verify client side
            var output = SendData(testCase, sourceAeTitle1, localAeTitle, 1);

            VerifyAllCrdCallsWereMade();
            VerifyClientLogs(output, "F: Reason: Calling AE Title Not Recognized", 1);

            output = SendData(testCase, sourceAeTitle2, localAeTitle, 0);

            VerifyClientLogs(output, "I: Received Store Response (Success)", 3);
            VerifyConfigChanges(1, 1, 0);
            await VerifyWithRestAPIs(1, 1, 0);
        }

        private async Task AddAnotherSource()
        {
            var testCase = "1-scp-explicitVrLittleEndian";

            var localAeTitle = "ClaraSCP1";
            var sourceAeTitle1 = "GoodSource";
            var sourceAeTitle2 = "PACS";

            SetupLocalAe(localAeTitle);
            SetupSource(sourceAeTitle1, sourceAeTitle2);
            SetupDestination();

            _dicomAdapterFixture.WaitForAllEventHandles(10000);
            // Verify client side
            var output = SendData(testCase, sourceAeTitle1, localAeTitle, 0);

            VerifyAllCrdCallsWereMade();
            VerifyClientLogs(output, "I: Received Store Response (Success)", 3);

            output = SendData(testCase, sourceAeTitle2, localAeTitle, 0);

            VerifyClientLogs(output, "I: Received Store Response (Success)", 3);
            VerifyConfigChanges(1, 2, 0);
            await VerifyWithRestAPIs(1, 2, 0);
        }

        private async Task AddLocalAeTitle()
        {
            var testCase = "1-scp-explicitVrLittleEndian";

            var localAeTitle = "ClaraSCP1";
            var sourceAeTitle = "GoodSource";

            SetupLocalAe(localAeTitle);
            SetupSource(sourceAeTitle);
            SetupDestination();

            _dicomAdapterFixture.WaitForAllEventHandles(10000);
            var output = SendData(testCase, sourceAeTitle, localAeTitle, 0);

            VerifyAllCrdCallsWereMade();
            VerifyClientLogs(output, "I: Received Store Response (Success)", 3);
            VerifyConfigChanges(1, 1, 0);
            await VerifyWithRestAPIs(1, 1, 0);
        }

        private async Task AddSourceWithoutValidLocalAeTittle()
        {
            var testCase = "1-scp-explicitVrLittleEndian";

            var sourceAeTitle = "GoodSource";

            SetupLocalAe();
            SetupSource(sourceAeTitle);
            SetupDestination();

            _dicomAdapterFixture.WaitForAllEventHandles(10000);

            var output = SendData(testCase, sourceAeTitle, "NOBODY", 1);

            VerifyAllCrdCallsWereMade();
            VerifyClientLogs(output, "F: Reason: Called AE Title Not Recognized", 1);
            VerifyConfigChanges(0, 1, 0);
            await VerifyWithRestAPIs(0, 1, 0);
        }

        private async Task InitialState()
        {
            var testCase = "1-scp-explicitVrLittleEndian";

            SetupLocalAe();
            SetupSource();
            SetupDestination();

            _dicomAdapterFixture.WaitForAllEventHandles(2000);

            var output = SendData(testCase, "UNKNOWN", "NOBODY", 1);

            VerifyAllCrdCallsWereMade();
            VerifyClientLogs(output, "F: Reason: Calling AE Title Not Recognized", 1);
            VerifyConfigChanges(0, 0, 0);
            await VerifyWithRestAPIs(0, 0, 0);
        }

        private void VerifyClientLogs(string[] logs, string message, int count)
        {
            logs.Where(p => p == message).Should().HaveCount(count);
        }

        private void VerifyConfigChanges(int claraAeTitleCount, int sourceCount, int destinationCount)
        {
            Assert.Equal(claraAeTitleCount, _dicomAdapterFixture.GetConfiguration().Dicom.Scp.AeTitles.Count());
            Assert.Equal(sourceCount, _dicomAdapterFixture.GetConfiguration().Dicom.Scp.Sources.Count());
            Assert.Equal(destinationCount, _dicomAdapterFixture.GetConfiguration().Dicom.Scu.Destinations.Count());
        }

        private string[] SendData(string testCase, string callingAet, string calledAet, int expectedExitCode)
        {
            int exitCode = 0;
            var output = DcmtkLauncher.StoreScu(
                _testFileSetsFixture.FileSetPaths[testCase].First().FileDirectoryPath,
                 "-xb",
                 $"-v -aet {callingAet} -aec {calledAet}",
                 out exitCode,
                 "1114");
            Assert.Equal(expectedExitCode, exitCode);
            return output;
        }

        private void VerifyAllCrdCallsWereMade()
        {
            _dicomAdapterFixture.KubernetesClient
                .Verify(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.Is<CustomResourceDefinition>(p => p.Kind == "ClaraAeTitle")), Times.AtLeastOnce());
            _dicomAdapterFixture.KubernetesClient
                .Verify(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.Is<CustomResourceDefinition>(p => p.Kind == "Source")), Times.AtLeastOnce());
            _dicomAdapterFixture.KubernetesClient
                .Verify(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.Is<CustomResourceDefinition>(p => p.Kind == "Destination")), Times.AtLeastOnce());
        }

        private void SetupDestination(params string[] names)
        {
            var destCrdList = new DestinationApplicationEntityCustomResourceList();
            destCrdList.Items = new List<DestinationApplicationEntityCustomResource>();
            foreach (var name in names)
            {
                destCrdList.Items.Add(new DestinationApplicationEntityCustomResource
                {
                    Spec = new DestinationApplicationEntity
                    {
                        Name = name,
                        AeTitle = name,
                        HostIp = "hostname",
                        Port = 123
                    },
                    Metadata = new V1ObjectMeta { Name = name }
                });
            }
            _dicomAdapterFixture.KubernetesClient
                .Setup(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.Is<CustomResourceDefinition>(p => p.Kind == "Destination")))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(destCrdList)) }
                }));
        }

        private void SetupSource(params string[] names)
        {
            var sourceCrdList = new SourceApplicationEntityCustomResourceList();
            sourceCrdList.Items = new List<SourceApplicationEntityCustomResource>();
            foreach (var name in names)
            {
                sourceCrdList.Items.Add(new SourceApplicationEntityCustomResource
                {
                    Spec = new SourceApplicationEntity
                    {
                        AeTitle = name,
                        HostIp = "hostname"
                    },
                    Metadata = new V1ObjectMeta { Name = name }
                });
            }

            _dicomAdapterFixture.KubernetesClient
                .Setup(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.Is<CustomResourceDefinition>(p => p.Kind == "Source")))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(sourceCrdList)) }
                }));
        }

        private void SetupLocalAe(params string[] names)
        {
            var claraCrdList = new ClaraApplicationEntityCustomResourceList();
            claraCrdList.Items = new List<ClaraApplicationEntityCustomResource>();
            foreach (var name in names)
            {
                var aet = new ClaraApplicationEntityCustomResource
                {
                    Spec = new ClaraApplicationEntity
                    {
                        AeTitle = name,
                        Name = name,
                        ProcessorSettings = new Dictionary<string, string>() { { "pipeline-test", "123" } }
                    },
                    Metadata = new V1ObjectMeta { Name = name }
                };
                claraCrdList.Items.Add(aet);
            }

            _dicomAdapterFixture.KubernetesClient
                .Setup(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.Is<CustomResourceDefinition>(p => p.Kind == "ClaraAeTitle")))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(claraCrdList)) }
                }));
        }
    }
}