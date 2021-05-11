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

using Ardalis.GuardClauses;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Server.Services.Export;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    internal class TestExportService : ExportServiceBase
    {
        public static string AgentName = "TestAgent";

        public event EventHandler ExportDataBlockCalled;

        public event EventHandler ConvertDataBlockCalled;

        public bool ConvertReturnsEmpty = false;
        public bool ExportReturnsNull = false;
        protected override string Agent => AgentName;

        protected override int Concurrentcy => 1;

        public TestExportService(
            ILogger logger,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration,
            IServiceScopeFactory serviceScopeFactory,
            IStorageInfoProvider storageInfoProvider)
            : base(logger, dicomAdapterConfiguration, serviceScopeFactory, storageInfoProvider)
        {
        }

        protected override IEnumerable<OutputJob> ConvertDataBlockCallback(IList<TaskResponse> jobs, CancellationToken cancellationToken)
        {
            Guard.Against.Null(jobs, nameof(jobs));

            if (ConvertDataBlockCalled != null)
            {
                ConvertDataBlockCalled(this, new EventArgs());
            }

            if (ConvertReturnsEmpty)
            {
                yield break;
            }
            else
            {
                foreach (var task in jobs)
                {
                    yield return new OutputJob(task);
                }
            }
        }

        protected override Task<OutputJob> ExportDataBlockCallback(OutputJob outputJob, CancellationToken cancellationToken)
        {
            if (ExportDataBlockCalled != null)
            {
                ExportDataBlockCalled(this, new EventArgs());
            }

            if (ExportReturnsNull || outputJob is null)
            {
                return null;
            }

            outputJob.SuccessfulExport++;
            return Task.FromResult(outputJob);
        }
    }

    public class ExportServiceBaseTest
    {
        private readonly Mock<ILogger> _logger;
        private readonly Mock<IPayloads> _payloadsApi;
        private readonly Mock<IResultsService> _resultsService;
        private readonly Mock<IStorageInfoProvider> _storageInfoProvider;
        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        private readonly CancellationTokenSource _cancellationTokenSource;
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactory;

        public ExportServiceBaseTest()
        {
            _logger = new Mock<ILogger>();
            _payloadsApi = new Mock<IPayloads>();
            _resultsService = new Mock<IResultsService>();
            _storageInfoProvider = new Mock<IStorageInfoProvider>();
            _configuration = Options.Create(new DicomAdapterConfiguration());
            _configuration.Value.Dicom.Scu.ExportSettings.PollFrequencyMs = 10;
            _cancellationTokenSource = new CancellationTokenSource();
            _serviceScopeFactory = new Mock<IServiceScopeFactory>();

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(x => x.GetService(typeof(IPayloads)))
                .Returns(_payloadsApi.Object);
            serviceProvider
                .Setup(x => x.GetService(typeof(IResultsService)))
                .Returns(_resultsService.Object);

            var scope = new Mock<IServiceScope>();
            scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

            _serviceScopeFactory.Setup(p => p.CreateScope()).Returns(scope.Object);

        }

        [RetryFact(DisplayName = "Data flow test - no pending tasks")]
        public async Task DataflowTest_NoPendingTasks()
        {
            var exportCalled = false;
            var convertCalled = false;
            var completedEvent = new ManualResetEvent(false);
            var service = new TestExportService(_logger.Object, _configuration, _serviceScopeFactory.Object, _storageInfoProvider.Object);
            service.ExportDataBlockCalled += (sender, args) =>
            {
                exportCalled = true;
            };
            service.ConvertDataBlockCalled += (sender, args) =>
            {
                convertCalled = true;
            };
            _resultsService.Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>())).Returns(Task.FromResult((IList<TaskResponse>)null));
            _resultsService.Setup(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            _resultsService.Setup(p => p.ReportFailure(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableForExport).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            await service.StartAsync(_cancellationTokenSource.Token);
            Thread.Sleep(3000);
            Assert.False(exportCalled);
            Assert.False(convertCalled);

            _resultsService.Verify(p => p.GetPendingJobs(TestExportService.AgentName, It.IsAny<CancellationToken>(), 10), Times.AtLeastOnce());
            await StopAndVerify(service);
            _logger.VerifyLogging($"Export Service completed timer routine.", LogLevel.Trace, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableForExport, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        [RetryFact(DisplayName = "Data flow test - insufficient storage space")]
        public async Task DataflowTest_InsufficientStorageSpace()
        {
            var exportCalled = false;
            var convertCalled = false;
            var completedEvent = new ManualResetEvent(false);
            var service = new TestExportService(_logger.Object, _configuration, _serviceScopeFactory.Object, _storageInfoProvider.Object);
            service.ExportDataBlockCalled += (sender, args) =>
            {
                exportCalled = true;
            };
            service.ConvertDataBlockCalled += (sender, args) =>
            {
                convertCalled = true;
            };
            _resultsService.Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>())).Returns(Task.FromResult((IList<TaskResponse>)null));
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableForExport).Returns(false);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            await service.StartAsync(_cancellationTokenSource.Token);
            Thread.Sleep(1000);
            Assert.False(exportCalled);
            Assert.False(convertCalled);

            _resultsService.Verify(p => p.GetPendingJobs(TestExportService.AgentName, It.IsAny<CancellationToken>(), 10), Times.Never());
            await StopAndVerify(service);
            _logger.VerifyLogging($"Export Service completed timer routine.", LogLevel.Trace, Times.Never());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableForExport, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.AtLeastOnce());
        }

        [RetryFact(DisplayName = "Data flow test - convert blocks returns empty")]
        public async Task DataflowTest_ConvertReturnsEmpty()
        {
            var exportCountdown = new CountdownEvent(1);
            var convertCountdown = new CountdownEvent(1);

            var service = new TestExportService(_logger.Object, _configuration, _serviceScopeFactory.Object, _storageInfoProvider.Object);
            service.ConvertReturnsEmpty = true;
            service.ExportDataBlockCalled += (sender, args) =>
            {
                exportCountdown.Signal();
            };
            service.ConvertDataBlockCalled += (sender, args) =>
            {
                convertCountdown.Signal();
            };

            var tasks = GenerateTaskResponse(1);

            _resultsService.Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>())).Returns(Task.FromResult(tasks));
            _resultsService.Setup(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            _resultsService.Setup(p => p.ReportFailure(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableForExport).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            await service.StartAsync(_cancellationTokenSource.Token);
            Assert.True(convertCountdown.Wait(3000));
            Assert.False(exportCountdown.Wait(3000));
            _resultsService.Verify(p => p.GetPendingJobs(TestExportService.AgentName, It.IsAny<CancellationToken>(), 10), Times.AtLeastOnce());
            await StopAndVerify(service);
            _logger.VerifyLogging($"Export Service completed timer routine.", LogLevel.Trace, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableForExport, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        [RetryFact(DisplayName = "Data flow test - payload download failure")]
        public async Task DataflowTest_PayloadDownloadFailure()
        {
            var exportCountdown = new CountdownEvent(1);
            var convertCountdown = new CountdownEvent(1);
            var reportCountdown = new CountdownEvent(1);

            var service = new TestExportService(_logger.Object, _configuration, _serviceScopeFactory.Object, _storageInfoProvider.Object);
            service.ExportDataBlockCalled += (sender, args) =>
            {
                exportCountdown.Signal();
            };
            service.ConvertDataBlockCalled += (sender, args) =>
            {
                convertCountdown.Signal();
            };
            service.ReportActionStarted += (sender, args) =>
            {
                reportCountdown.Signal();
            };

            var tasks = GenerateTaskResponse(1);

            _resultsService.Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>())).Returns(Task.FromResult(tasks));
            _resultsService.Setup(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            _resultsService.Setup(p => p.ReportFailure(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            _payloadsApi.Setup(p => p.Download(It.IsAny<string>(), It.IsAny<string>())).Throws(new Exception("error"));
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableForExport).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            await service.StartAsync(_cancellationTokenSource.Token);
            Assert.True(convertCountdown.Wait(3000));
            Assert.True(exportCountdown.Wait(3000));
            Assert.False(reportCountdown.Wait(3000));

            _resultsService.Verify(p => p.GetPendingJobs(TestExportService.AgentName, It.IsAny<CancellationToken>(), 10), Times.AtLeastOnce());
            _payloadsApi.Verify(p => p.Download(tasks.First().PayloadId, tasks.First().Uris.First()), Times.Once());

            _logger.VerifyLogging($"Failed to download file {tasks.First().Uris.First()}.", LogLevel.Warning, Times.Once());
            _logger.VerifyLogging($"Failure rate exceeded threshold and will not be exported.", LogLevel.Error, Times.Once());
            await StopAndVerify(service);
            _logger.VerifyLogging($"Error occurred while exporting.", LogLevel.Error, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableForExport, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        [RetryFact(DisplayName = "Data flow test - export returns null")]
        public async Task DataflowTest_ExportReturnsNull()
        {
            var exportCountdown = new CountdownEvent(1);
            var convertCountdown = new CountdownEvent(1);

            var service = new TestExportService(_logger.Object, _configuration, _serviceScopeFactory.Object, _storageInfoProvider.Object);
            service.ExportReturnsNull = true;

            service.ExportDataBlockCalled += (sender, args) =>
            {
                exportCountdown.Signal();
            };
            service.ConvertDataBlockCalled += (sender, args) =>
            {
                convertCountdown.Signal();
            };

            var tasks = GenerateTaskResponse(1);

            _resultsService.Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>())).Returns(Task.FromResult(tasks));
            _resultsService.Setup(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            _resultsService.Setup(p => p.ReportFailure(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            _payloadsApi.Setup(p => p.Download(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new PayloadFile
                {
                    Name = tasks.First().Uris.First(),
                    Data = InstanceGenerator.GenerateDicomData()
                }));
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableForExport).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            await service.StartAsync(_cancellationTokenSource.Token);
            Assert.True(convertCountdown.Wait(3000));
            Assert.True(exportCountdown.Wait(3000));

            _resultsService.Verify(p => p.GetPendingJobs(TestExportService.AgentName, It.IsAny<CancellationToken>(), 10), Times.AtLeastOnce());
            _payloadsApi.Verify(p => p.Download(tasks.First().PayloadId, tasks.First().Uris.First()), Times.Once());

            _logger.VerifyLogging($"Failed to download file {tasks.First().Uris.First()}.", LogLevel.Warning, Times.Never());
            _logger.VerifyLogging($"Failure rate exceeded threshold and will not be exported.", LogLevel.Error, Times.Never());

            await StopAndVerify(service);
            _logger.VerifyLogging($"Error occurred while exporting.", LogLevel.Error, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableForExport, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        [RetryFact(DisplayName = "Data flow test - completed entire data flow")]
        public async Task DataflowTest_CompletedEntireDataflow()
        {
            var exportCountdown = new CountdownEvent(1);
            var convertCountdown = new CountdownEvent(1);

            var service = new TestExportService(_logger.Object, _configuration, _serviceScopeFactory.Object, _storageInfoProvider.Object);

            service.ExportDataBlockCalled += (sender, args) =>
            {
                exportCountdown.Signal();
            };
            service.ConvertDataBlockCalled += (sender, args) =>
            {
                convertCountdown.Signal();
            };

            var tasks = GenerateTaskResponse(1);

            _resultsService.Setup(p => p.GetPendingJobs(It.IsAny<string>(), It.IsAny<CancellationToken>(), It.IsAny<int>())).Returns(Task.FromResult(tasks));
            _resultsService.Setup(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            _resultsService.Setup(p => p.ReportFailure(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).Returns(Task.FromResult(true));
            _payloadsApi.Setup(p => p.Download(It.IsAny<string>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new PayloadFile
                {
                    Name = tasks.First().Uris.First(),
                    Data = InstanceGenerator.GenerateDicomData()
                }));
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableForExport).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);

            await service.StartAsync(_cancellationTokenSource.Token);
            Assert.True(convertCountdown.Wait(3000));
            Assert.True(exportCountdown.Wait(3000));

            _resultsService.Verify(p => p.GetPendingJobs(TestExportService.AgentName, It.IsAny<CancellationToken>(), 10), Times.AtLeastOnce());
            _resultsService.Verify(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Once());
            _payloadsApi.Verify(p => p.Download(tasks.First().PayloadId, tasks.First().Uris.First()), Times.Once());

            _logger.VerifyLogging($"Failed to download file {tasks.First().Uris.First()}.", LogLevel.Warning, Times.Never());
            _logger.VerifyLogging($"Failure rate exceeded threshold and will not be exported.", LogLevel.Error, Times.Never());
            _logger.VerifyLogging($"Task marked as successful.", LogLevel.Error, Times.Never());

            await StopAndVerify(service);
            _logger.VerifyLogging($"Export Service completed timer routine.", LogLevel.Trace, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableForExport, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        internal static IList<TaskResponse> GenerateTaskResponse(int count)
        {
            var result = new List<TaskResponse>();

            for (int i = 0; i < count; i++)
            {
                result.Add(new TaskResponse
                {
                    Agent = TestExportService.AgentName,
                    JobId = Guid.NewGuid().ToString(),
                    PayloadId = Guid.NewGuid().ToString(),
                    PipelineId = Guid.NewGuid().ToString(),
                    TaskId = Guid.NewGuid(),
                    Uris = new string[] { Guid.NewGuid().ToString() }
                });
            }

            return result;
        }

        private async Task StopAndVerify(TestExportService service)
        {
            await service.StopAsync(_cancellationTokenSource.Token);
            _resultsService.Invocations.Clear();
            _logger.VerifyLogging($"Export Task Watcher Hosted Service is stopping.", LogLevel.Information, Times.Once());
            Thread.Sleep(500);
            _resultsService.Verify(p => p.GetPendingJobs(TestExportService.AgentName, It.IsAny<CancellationToken>(), 10), Times.Never());
        }
    }
}