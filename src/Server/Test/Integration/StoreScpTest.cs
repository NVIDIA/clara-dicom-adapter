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
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Integration
{
    [Collection("DICOM Adapter")]
    public class StoreScpTest : IClassFixture<ScpTestFileSetsFixture>, IAsyncDisposable
    {
        private const string AE1 = "Clara1";
        private const string AE2 = "Clara2";
        private readonly DicomAdapterFixture _dicomAdapterFixture;
        private readonly TestFileSetsFixture _testFileSetsFixture;

        public StoreScpTest(DicomAdapterFixture dicomAdapterFixture, ScpTestFileSetsFixture testFileSetsFixture)
        {
            _dicomAdapterFixture = dicomAdapterFixture;
            _testFileSetsFixture = testFileSetsFixture;

            _dicomAdapterFixture.ResetMocks();
        }

        public async ValueTask DisposeAsync()
        {
            await _dicomAdapterFixture.DisposeAsync();
        }

        [RetryFact(DisplayName = "C-STORE SCP Rejects unknown source")]
        public void ScpShouldRejectUnknownSource()
        {
            var testCase = "1-scp-explicitVrLittleEndian";
            int exitCode = 0;
            var output = DcmtkLauncher.StoreScu(
                _testFileSetsFixture.FileSetPaths[testCase].First().FileDirectoryPath,
                 "-xb",
                 $"-v -aet UNKNOWN -aec {AE1}",
                 out exitCode);

            Assert.Equal(1, exitCode);
            output.Where(p => p == "F: Reason: Calling AE Title Not Recognized").Should().HaveCount(1);
        }

        [RetryFact(DisplayName = "C-STORE SCP Rejects unknown called AE Title")]
        public void ScpShouldRejectUnknownCalledAeTitle()
        {
            var testCase = "1-scp-explicitVrLittleEndian";
            int exitCode = 0;
            var output = DcmtkLauncher.StoreScu(
                _testFileSetsFixture.FileSetPaths[testCase].First().FileDirectoryPath, "-xb", $"-v -aet PACS1 -aec UNKNOWN",
                 out exitCode);

            Assert.Equal(1, exitCode);
            output.Where(p => p == "F: Reason: Called AE Title Not Recognized").Should().HaveCount(1);
        }

        [RetryFact(10, DisplayName = "C-STORE SCP shall be able to accept multiple associations over multiple AE Titles")]
        public void ScpShallAcceptMultipleAssociationsOverMultipleAetitles()
        {
            var testCase = "2-2-patients-2-studies";
            // Clara1 with 4 studies launched
            // Clara2 with 2 patients * 2 pipelines launched
            var jobCreatedEvent = new CountdownEvent(8);
            var jobStoredEvent = new CountdownEvent(8);
            var jobs = new List<Job>();
            var instanceStoredCounter = new MockedStoredInstanceObserver();
            var jobStoreInstanceCount = 0;
            _dicomAdapterFixture.GetIInstanceStoredNotificationService().Subscribe(instanceStoredCounter);

            _dicomAdapterFixture.Jobs.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>()))
                .Callback((string pipelineId, string jobName, JobPriority jobPriority) =>
                {
                    jobCreatedEvent.Signal();
                    Console.WriteLine(">>>>> Job.Create {0} - {1} - {2}", pipelineId, jobName, jobPriority);
                })
                .Returns((string pipelineId, string jobName, JobPriority jobPriority) => Task.FromResult(new Job
                {
                    JobId = Guid.NewGuid().ToString(),
                    PayloadId = Guid.NewGuid().ToString()
                }));


            _dicomAdapterFixture.JobStore.Setup(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()))
                .Callback((Job job, string jobName, IList<InstanceStorageInfo> instances) =>
                {
                    Console.WriteLine(">>>>> JobStore.New {0} - {1} (files:{2})", job.JobId, jobName, instances.Count);
                    Interlocked.Add(ref jobStoreInstanceCount, instances.Count);
                    jobStoredEvent.Signal();
                });

            var outputs = new List<StringBuilder>();
            var processes = new List<Process>();
            var paths = new List<string>();

            var rootPath = Path.Combine(TestFileSetsFixture.ApplicationEntryDirectory, testCase);

            foreach (var patient in new[] { "P0", "P1" })
            {
                foreach (var study in new[] { "S0", "S1" })
                {
                    var path = Path.Combine(rootPath, patient, study);
                    paths.Add(path);
                    paths.Add(path);

                    var output1 = new StringBuilder();
                    var proc1 = DcmtkLauncher.StoreScuNoWait(path, $"-xb", $"-v -aet PACS1 -aec {AE1}", output1);
                    outputs.Add(output1);
                    processes.Add(proc1);

                    var output2 = new StringBuilder();
                    var proc2 = DcmtkLauncher.StoreScuNoWait(path, $"-xb", $"-v -aet PACS1 -aec {AE2}", output2);
                    outputs.Add(output2);
                    processes.Add(proc2);
                }
            }

            int totalInstanceSent = 0;
            int threadSleep = 2000;
            for (var i = 0; i < processes.Count; i++)
            {
                processes[i].WaitForExit();
                Console.WriteLine(">>>>> Association #{0}", i);
                // if (processes[i].ExitCode != 0)
                //     Console.WriteLine(">>>>> {0}", outputs[i].ToString());
                Assert.Equal(0, processes[i].ExitCode);
                // Console.WriteLine(outputs[i].ToString());

                // make sure we are sending correct number of files
                var instanceSent = Directory.GetFiles(paths[i]).Count();
                totalInstanceSent += instanceSent;
                Thread.Sleep(threadSleep);
                outputs[i].ToString().Split(Environment.NewLine)
                    .Where(p => p.Contains("I: Received Store Response (Success)"))
                    .Should()
                    .HaveCount(instanceSent, outputs[i].ToString());
                threadSleep -= 125;
            }
            Assert.True(jobCreatedEvent.Wait(TimeSpan.FromSeconds(60)));
            Assert.True(jobStoredEvent.Wait(TimeSpan.FromSeconds(30)));
            Assert.Equal(totalInstanceSent, instanceStoredCounter.InstanceCount);
            Assert.Equal(2400, jobStoreInstanceCount);
            _dicomAdapterFixture.JobStore.Verify(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()), Times.Exactly(8));
        }

        [RetryFact(DisplayName = "C-STORE SCP shall be able to compose a single job from multiple associations")]
        public void ScpShallComposeSingleJobFromMultipleAssociations()
        {
            var testCase = "3-single-study-multi-series";
            var jobs = new List<Job>();
            var jobCreatedEvent = new CountdownEvent(1);
            var jobStoredEvent = new CountdownEvent(1);
            var instanceStoredCounter = new MockedStoredInstanceObserver();
            var jobStoreInstanceCount = 0;
            _dicomAdapterFixture.GetIInstanceStoredNotificationService().Subscribe(instanceStoredCounter);

            _dicomAdapterFixture.Jobs.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>()))
                .Callback((string pipelineId, string jobName, JobPriority jobPriority) =>
                {
                    Console.WriteLine(">>>>> Job.Create {0} - {1} - {2}", pipelineId, jobName, jobPriority);
                    jobCreatedEvent.Signal();
                })
                .Returns((string pipelineId, string jobName, JobPriority jobPriority) => Task.FromResult(new Job
                {
                    JobId = Guid.NewGuid().ToString(),
                    PayloadId = Guid.NewGuid().ToString()
                }));

            _dicomAdapterFixture.JobStore.Setup(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()))
                .Callback((Job job, string jobName, IList<InstanceStorageInfo> instances) =>
                {
                    jobStoredEvent.Signal();
                    jobStoreInstanceCount = instances.Count();
                });

            var outputs = new List<StringBuilder>();
            var processes = new List<Process>();
            var paths = new List<string>();

            var rootPath = Path.Combine(TestFileSetsFixture.ApplicationEntryDirectory, testCase);

            for (var i = 0; i < 5; i++)
            {
                var path = Path.Combine(rootPath, i.ToString());
                paths.Add(path);
                var output1 = new StringBuilder();
                var proc1 = DcmtkLauncher.StoreScuNoWait(path, $"-xb", $"-v -aet PACS1 -aec {AE1}", output1);
                outputs.Add(output1);
                processes.Add(proc1);
            }

            var totalInstanceSent = 0;
            for (var i = 0; i < processes.Count; i++)
            {
                processes[i].WaitForExit();
                Console.WriteLine(">>>>> Association #{0}", i);
                if (processes[i].ExitCode != 0)
                    Console.WriteLine(">>>>> {0}", outputs[i].ToString());
                Assert.Equal(0, processes[i].ExitCode);
                // Console.WriteLine(outputs[i].ToString());

                // make sure we are sending correct number of files
                var instanceSent = Directory.GetFiles(paths[i]).Count();
                totalInstanceSent += instanceSent;
                Thread.Sleep(750);
                outputs[i].ToString().Split(Environment.NewLine)
                    .Where(p => p.Contains("I: Received Store Response (Success)"))
                    .Should()
                    .HaveCount(instanceSent, outputs[i].ToString());
            }
            Assert.True(jobCreatedEvent.Wait(TimeSpan.FromSeconds(30)));
            Assert.True(jobStoredEvent.Wait(TimeSpan.FromSeconds(30)));
            Assert.Equal(totalInstanceSent, instanceStoredCounter.InstanceCount);
            Assert.Equal(totalInstanceSent, jobStoreInstanceCount);
            _dicomAdapterFixture.JobStore.Verify(p => p.Add(It.IsAny<Job>(), It.IsAny<string>(), It.IsAny<IList<InstanceStorageInfo>>()), Times.Once());
        }
    }

    internal class MockedStoredInstanceObserver : IObserver<InstanceStorageInfo>
    {
        private int instanceCount;

        public int InstanceCount { get => instanceCount; }

        public void OnCompleted()
        {
        }

        public void OnError(Exception error)
        {
        }

        public void OnNext(InstanceStorageInfo value)
        {
            Interlocked.Increment(ref instanceCount);
        }
    }
}