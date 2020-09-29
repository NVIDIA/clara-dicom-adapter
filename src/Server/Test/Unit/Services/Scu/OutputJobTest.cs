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
using System.Threading;
using Dicom;
using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Scu;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.ResultsService.Api;
using xRetry;
using Xunit;
using LogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class OutputJobTest
    {
        private Mock<ILogger<ScuService>> _logger;
        private Mock<IResultsService> _resultsService;
        private DestinationApplicationEntity _config;
        private CancellationTokenSource _cancellationTokenSource;

        public OutputJobTest()
        {
            _logger = new Mock<ILogger<ScuService>>();
            _resultsService = new Mock<IResultsService>();
            _config = new DestinationApplicationEntity();
            _cancellationTokenSource = new CancellationTokenSource();
            _resultsService.Setup(p => p.ReportFailure(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()));
            _resultsService.Setup(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>()));
        }

        [RetryFact(DisplayName = "ReportStatus - Shall report failure if exceeded threshold with retry")]
        public void ReportStatus_ShallReportFailureWithRetry()
        {
            var task = new TaskResponse
            {
                TaskId = Guid.NewGuid(),
                Uris = new[] { "file1", "file2", "file3" }
            };
            _config.AeTitle = "AET";
            _config.HostIp = "IP";
            _config.Port = 1000;
            var job = new OutputJob(task, _logger.Object, _resultsService.Object, _config);
            job.ReportStatus(_cancellationTokenSource.Token);

            _resultsService.Verify(p => p.ReportFailure(task.TaskId, true, _cancellationTokenSource.Token), Times.Once());
            _resultsService.Verify(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never());
            _logger.VerifyLoggingMessageBeginsWith($"Task marked as failed with failure rate", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "ReportStatus - Shall report failure if exceeded threshold without retry")]
        public void ReportStatus_ShallReportFailureWithoutRetry()
        {
            var task = new TaskResponse
            {
                TaskId = Guid.NewGuid(),
                Uris = new[] { "file1", "file2", "file3" },
                Retries = 3
            };
            _config.AeTitle = "AET";
            _config.HostIp = "IP";
            _config.Port = 1000;
            var job = new OutputJob(task, _logger.Object, _resultsService.Object, _config);
            job.ReportStatus(_cancellationTokenSource.Token);

            _resultsService.Verify(p => p.ReportFailure(task.TaskId, false, _cancellationTokenSource.Token), Times.Once());
            _resultsService.Verify(p => p.ReportSuccess(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never());
            _logger.VerifyLoggingMessageBeginsWith($"Task marked as failed with failure rate", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "ReportStatus - Shall report success")]
        public void ReportStatus_ShallReportSuccess()
        {
            var task = new TaskResponse
            {
                TaskId = Guid.NewGuid(),
                Uris = new[] { "file1", "file2", "file3" },
                Retries = 3
            };
            _config.AeTitle = "AET";
            _config.HostIp = "IP";
            _config.Port = 1000;

            var job = new OutputJob(task, _logger.Object, _resultsService.Object, _config);
            job.ProcessedDicomFiles.Add(new DicomFile());
            job.ProcessedDicomFiles.Add(new DicomFile());
            job.ReportStatus(_cancellationTokenSource.Token);

            _resultsService.Verify(p => p.ReportFailure(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()), Times.Never());
            _resultsService.Verify(p => p.ReportSuccess(task.TaskId, _cancellationTokenSource.Token), Times.Once());
            _logger.VerifyLogging($"Task marked as successful", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "LogFailedRequests - Shall log all failures")]
        public void LogFailedRequests_ShallLogAllFailrus()
        {
            var task = new TaskResponse();
            _config.AeTitle = "AET";
            _config.HostIp = "IP";
            _config.Port = 1000;
            var job = new OutputJob(task, _logger.Object, _resultsService.Object, _config);
            job.FailedFiles.Add("FILE1");

            var dicomFile = new DicomFile();
            dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID = DicomUIDGenerator.GenerateDerivedFromUUID();
            job.FailedDicomFiles.Add(dicomFile, "STATUS");

            job.LogFailedRequests();

            _logger.VerifyLogging("File FILE1 failed to download, corrupted or invalid", LogLevel.Error, Times.Once());
            _logger.VerifyLogging($"C-STORE failed on {dicomFile.FileMetaInfo.MediaStorageSOPInstanceUID} with response status STATUS", LogLevel.Error, Times.Once());
        }
    }
}
