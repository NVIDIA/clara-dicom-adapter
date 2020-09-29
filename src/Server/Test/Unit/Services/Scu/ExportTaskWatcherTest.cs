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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Scu;
using Nvidia.Clara.DicomAdapter.Server.Services.Services.Scu;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.ResultsService.Api;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class ExportTaskWatcherTest : IDisposable
    {
        private Mock<ILogger<ScuService>> _mockLogger;
        private Mock<IResultsService> _mockResultsService;
        private ActionBlock<OutputJob> _outputJobQueue;
        private CancellationTokenSource _cancellationSource;
        private ScuConfiguration _scuConfiguration;
        private List<TaskResponse> _tasks;
        private int _queuedTasks;

        public ExportTaskWatcherTest()
        {
            _mockLogger = new Mock<ILogger<ScuService>>();
            _mockResultsService = new Mock<IResultsService>();
            _cancellationSource = new CancellationTokenSource();
            _outputJobQueue = new ActionBlock<OutputJob>(
                job =>
                {
                    Console.WriteLine("\tProcessing job in queue...");
                    Interlocked.Increment(ref _queuedTasks);
                },
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = 2,
                    MaxMessagesPerTask = 1,
                    CancellationToken = _cancellationSource.Token
                });

            _scuConfiguration = new ScuConfiguration();
            _scuConfiguration.Destinations.Add(new DestinationApplicationEntity { Name = "dest1", Port = 104, AeTitle = "dest", HostIp = "1.2.3.4" });
            _scuConfiguration.Destinations.Add(new DestinationApplicationEntity { Name = "dest2", Port = 104, AeTitle = "dest", HostIp = "1.2.3.4" });
            _tasks = new List<TaskResponse>()
                {
                    new TaskResponse
                    {
                        TaskId = Guid.NewGuid(),
                        JobId = "job1",
                        PipelineId = "pipeline1",
                        PayloadId = "payload1",
                        Agent = "good",
                        Parameters = JsonConvert.SerializeObject("dest1"),
                        State = State.Pending,
                        Retries = 0,
                        Uris = new string[]{"uri1", "uri1"}
                    },
                    new TaskResponse
                    {
                        TaskId = Guid.NewGuid(),
                        JobId = "job2",
                        PipelineId = "pipeline2",
                        PayloadId = "payload2",
                        Agent = "good",
                        Parameters = JsonConvert.SerializeObject("dest2"),
                        State = State.Pending,
                        Retries = 0,
                        Uris = new string[]{"uri1", "uri1"}
                    },
                    new TaskResponse
                    {
                        TaskId = Guid.NewGuid(),
                        JobId = "job3",
                        PipelineId = "pipeline3",
                        PayloadId = "payload3",
                        Agent = "bad",
                        Parameters = JsonConvert.SerializeObject("dest3"),
                        State = State.Pending,
                        Retries = 0,
                        Uris = new string[]{"uri1", "uri1"}
                    },
                    new TaskResponse
                    {
                        TaskId = Guid.NewGuid(),
                        JobId = "job4",
                        PipelineId = "pipeline4",
                        PayloadId = "payload4",
                        Agent = "bad",
                        Parameters = JsonConvert.SerializeObject(""),
                        State = State.Pending,
                        Retries = 0,
                        Uris = new string[]{"uri1", "uri1"}
                    },
                    new TaskResponse
                    {
                        TaskId = Guid.NewGuid(),
                        JobId = "job5",
                        PipelineId = "pipeline5",
                        PayloadId = "payload5",
                        Agent = "bad",
                        Parameters = JsonConvert.SerializeObject(""),
                        State = State.Pending,
                        Retries = 0,
                        Uris = new string[]{"uri1", "uri1"}
                    }
                };
            _queuedTasks = 0;
        }

        // [RetryFact(DisplayName = "Shall throw if queue is null")]
        // public void ShallThrowIfQueueIsNull()
        // {
        //     Assert.Throws<ArgumentNullException>(() =>
        //     {
        //         using (var watcher = new ExportTaskWatcher(null, mockResultsService.Object, scuConfiguration))
        //         {
        //         }
        //     });
        // }

        // [RetryFact(DisplayName = "Shall throw if cancellation token is null")]
        // public void ShallThrowIfCancellationTokenIsNull()
        // {
        //     Assert.Throws<ArgumentNullException>(() =>
        //     {
        //         var watcher = new ExportTaskWatcher(mockLogger.Object, null, scuConfiguration);
        //     });
        // }

        // [RetryFact(DisplayName = "Shall queue tasks")]
        // public void ShallQueueAllTasks()
        // {
        //     //Given
        //     mockResultsService.Setup(p => p.GetPendingJobs(cancellationSource.Token, 10))
        //         .ReturnsAsync(() =>
        //         {
        //             var temp = tasks.Take(2).ToList();
        //             tasks.Clear();
        //             return temp;
        //         });

        //     //When
        //     var watcher = new ExportTaskWatcher(mockLogger.Object, mockResultsService.Object, scuConfiguration);
        //     watcher.Start(outputJobQueue, cancellationSource.Token);
        //     Thread.Sleep(1000);

        //     //Then
        //     Console.WriteLine("Queued tasks {0}", queuedTasks);
        //     Assert.True(2 == queuedTasks);
        //     mockLogger.VerifyLogging(LogLevel.Information, Times.AtLeast(2));
        //     mockResultsService.Verify(p => p.GetPendingJobs(cancellationSource.Token, 10), Times.AtLeastOnce());
        //     watcher.Dispose();
        // }

        // [RetryFact(DisplayName = "Shall report failures with bad 'Parameters'")]
        // public void ShallReportBadTasks()
        // {
        //     //Given
        //     mockResultsService.Setup(p => p.ReportFailure(It.IsAny<Guid>(), false, cancellationSource.Token));
        //     mockResultsService.Setup(p => p.GetPendingJobs(cancellationSource.Token, 10))
        //         .ReturnsAsync(() =>
        //         {
        //             var temp = tasks.ToList();
        //             tasks.Clear();
        //             return temp;
        //         });

        //     //When
        //     var watcher = new ExportTaskWatcher(mockLogger.Object, mockResultsService.Object, scuConfiguration);
        //     watcher.Start(outputJobQueue, cancellationSource.Token);
        //     Thread.Sleep(1250);

        //     //Then
        //     Console.WriteLine("Queued tasks {0}", queuedTasks);
        //     Assert.True(2 == queuedTasks);
        //     mockResultsService.Verify(p => p.GetPendingJobs(cancellationSource.Token, 10), Times.AtLeast(2));
        //     mockResultsService.Verify(p => p.ReportFailure(It.IsAny<Guid>(), false, cancellationSource.Token), Times.AtLeast(3));
        //     watcher.Dispose();
        // }

        // [RetryFact(DisplayName = "Shall cancel watcher upon request")]
        // public void ShallCancelWatcher()
        // {
        //     //Given
        //     mockResultsService.Setup(p => p.GetPendingJobs(cancellationSource.Token, 10))
        //         .ReturnsAsync(() =>
        //         {
        //             return new List<TaskResponse>();
        //         });

        //     //When
        //     var watcher = new ExportTaskWatcher(mockLogger.Object, mockResultsService.Object, scuConfiguration);
        //     watcher.Start(outputJobQueue, cancellationSource.Token);
        //     Thread.Sleep(750);
        //     cancellationSource.Cancel();
        //     Thread.Sleep(500);

        //     //Then
        //     Console.WriteLine("Queued tasks {0}", queuedTasks);
        //     Assert.True(0 == queuedTasks);
        //     mockResultsService.Verify(p => p.GetPendingJobs(cancellationSource.Token, 10), Times.AtLeast(1));
        //     mockLogger.VerifyLogging(LogLevel.Information, Times.AtLeast(1));
        //     watcher.Dispose();
        // }

        public void Dispose()
        {
            _outputJobQueue.Complete();
            _outputJobQueue = null;
            _cancellationSource.Cancel();
            _cancellationSource.Dispose();
        }
    }
}
