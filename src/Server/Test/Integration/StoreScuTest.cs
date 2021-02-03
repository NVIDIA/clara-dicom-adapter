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

using FluentAssertions;
using Moq;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Integration
{
   /// <summary>
   /// If these tests are failing, enable log output in DICOM Fixture to make sure DICOM Adapter is started without
   /// errors.
   /// </summary>
   [Collection("DICOM Adapter")]
   public class StoreScuTest : IClassFixture<ScuTestFileSetsFixture>, IAsyncDisposable
   {
        private const string AET_ClaraSCU = "ClaraSCU";
        private const int ScpPort = 11112;
       private static readonly string ApplicationEntryDirectory = AppDomain.CurrentDomain.BaseDirectory;
       private readonly DicomAdapterFixture _dicomAdapterFixture;
       private readonly ScuTestFileSetsFixture _testFileFixture;
       private int _downloadCount;
       private int _failedCount;
       private int _succeededCount;

       public StoreScuTest(DicomAdapterFixture fixture, ScuTestFileSetsFixture testFiles)
       {
           _dicomAdapterFixture = fixture;
           _testFileFixture = testFiles;
           _downloadCount = 0;
           _failedCount = 0;
           _succeededCount = 0;

           _dicomAdapterFixture.Payloads.Reset();
           _dicomAdapterFixture.Payloads.Setup(p => p.Download(It.IsAny<string>(), It.IsAny<string>()))
               .Callback(() =>
               {
                   _downloadCount++;
               })
               .ReturnsAsync((string payloadId, string path) =>
                   new API.PayloadFile
                   {
                       Data = File.ReadAllBytes(path)
                   });

           _dicomAdapterFixture.Jobs.Reset();
           _dicomAdapterFixture.ResultsService.Reset();
           _dicomAdapterFixture.ResultsService.Setup(p => p.ReportFailure(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()))
               .Callback((() =>
               {
                   _failedCount++;
               }));
           _dicomAdapterFixture.ResultsService.Setup(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
               .Callback((() =>
               {
                   _succeededCount++;
               }));
       }

       [RetryFact(DisplayName = "C-STORE SCU test using different transfer syntaxes")]
       public void ScuShallReceiveProposedTransferSyntaxes()
       {
           var testCase = "1-scu-with-multiple-transferSyntaxes";
           var queue = new Queue<TaskResponse>();

           _dicomAdapterFixture.ResultsService.Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
               .ReturnsAsync((string agent, CancellationToken token, int count) =>
               {
                   if(agent != AET_ClaraSCU) return null;

                   IList<TaskResponse> items = new List<TaskResponse>();
                   while (queue.Count() > 0)
                   {
                       items.Add(queue.Dequeue());
                   }
                   return items;
               });

           string[] scpLogs = null;
           var fileCount = _testFileFixture.FileSetPaths[testCase].Count;
           using (var scp = new StoreScpWrapper("+xa", ScpPort))
           {
               AddToQueue(queue, testCase);
               int timeout = 0;
               while (_downloadCount < fileCount)
               {
                   Assert.InRange(timeout++, 0, 20 * fileCount);
                   Thread.Sleep(500);
               }

               timeout = 0;
               while ((_succeededCount + _failedCount) == 0)
               {
                   Assert.InRange(timeout++, 0, 20 * fileCount);
                   Thread.Sleep(500);
               }

               scpLogs = scp.GetLogs();
           }

           Thread.Sleep(500);

           scpLogs.Filter("Received Store Request")
               .Should()
               .HaveCount(fileCount);

           Assert.Equal(1, _succeededCount);
           Assert.Equal(0, _failedCount);
       }

       [Theory(DisplayName = "C-STORE SCU shall report failure status")]
       [InlineData("3-scu-that-would-fail-and-retry", "ABORT initiated (due to command line options)", "--abort-after", 1)]
       [InlineData("4-scu-that-would-fail-and-retry", "Refusing Association (forced via command line)", "--refuse", 0)]
       public void ScuShallRetryOnFailure(string testCase, string expectedScpError, string args, int receivedInstanceCount)
       {
           var queue = new Queue<TaskResponse>();

           _dicomAdapterFixture.ResultsService.Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
               .ReturnsAsync((string agent, CancellationToken token, int count) =>
               {
                   if(agent != AET_ClaraSCU) return null;
                   IList<TaskResponse> items = new List<TaskResponse>();
                   while (queue.Count() > 0)
                   {
                       items.Add(queue.Dequeue());
                   }
                   return items;
               });

           string[] scpLogs = null;
           var fileCount = _testFileFixture.FileSetPaths[testCase].Count;
           using (var scp = new StoreScpWrapper($"+x= {args}", ScpPort))
           {
               AddToQueue(queue, testCase);
               int timeout = 0;
               while (_downloadCount < fileCount)
               {
                   Assert.InRange(timeout++, 0, 20 * fileCount);
                   Thread.Sleep(500);
               }

               timeout = 0;
               while ((_succeededCount + _failedCount) == 0)
               {
                   Assert.InRange(timeout++, 0, 20 * fileCount);
                   Thread.Sleep(500);
               }

               scpLogs = scp.GetLogs();
           }

           scpLogs.Filter("Received Store Request")
               .Should()
               .HaveCount(receivedInstanceCount);

           scpLogs.Filter(expectedScpError)
               .Should()
               .HaveCountGreaterOrEqualTo(fileCount);

           Assert.Equal(0, _succeededCount);
           Assert.Equal(1, _failedCount);
       }

       [RetryFact(DisplayName = "ScuService download payload shall not crash on failure")]
       public void ScuDownloadFailureFromPayloadShallNotCrash()
       {
           _dicomAdapterFixture.Payloads.Reset();
           _dicomAdapterFixture.Payloads.Setup(p => p.Download(It.IsAny<string>(), It.IsAny<string>()))
               .Throws(new Exception());

           var testCase = "1-scu-with-multiple-transferSyntaxes";
           var queue = new Queue<TaskResponse>();

           _dicomAdapterFixture.ResultsService.Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>()))
               .ReturnsAsync((string agent, CancellationToken token, int count) =>
               {
                   if(agent != AET_ClaraSCU) return null;
                   IList<TaskResponse> items = new List<TaskResponse>();
                   while (queue.Count() > 0)
                   {
                       items.Add(queue.Dequeue());
                   }
                   return items;
               });

           string[] scpLogs = null;
           var fileCount = _testFileFixture.FileSetPaths[testCase].Count;
           using (var scp = new StoreScpWrapper("+xa", ScpPort))
           {
               AddToQueue(queue, testCase);
               Thread.Sleep(3000);

               scpLogs = scp.GetLogs();
           }

           scpLogs.Filter("Received Store Request")
               .Should()
               .HaveCount(0);

           Assert.Equal(0, _succeededCount);
           Assert.Equal(1, _failedCount);
       }

       private Queue<TaskResponse> AddToQueue(Queue<TaskResponse> queue, string name)
       {
           queue.Enqueue(new TaskResponse
           {
               TaskId = Guid.NewGuid(),
               JobId = name,
               PipelineId = name,
               PayloadId = name,
               Agent = AET_ClaraSCU,
               Parameters = Newtonsoft.Json.JsonConvert.SerializeObject("PACS1"),
               State = State.Pending,
               Retries = 0,
               Uris = _testFileFixture.FileSetPaths[name].Select(p => p.FilePath).ToArray()
           });

           return queue;
       }

       public async ValueTask DisposeAsync()
       {
           await _dicomAdapterFixture.DisposeAsync();
       }
   }
}