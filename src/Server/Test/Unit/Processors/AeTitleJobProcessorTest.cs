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
using Dicom.Network;
using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Processors;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.Platform;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class AeTitleJobProcessorTest
    {
        private CancellationTokenSource _cancellationTokenSource;
        private ClaraApplicationEntity _configuration;
        private IInstanceStoredNotificationService _notificationService;
        private Mock<ILoggerFactory> _loggerFactory;
        private Mock<IJobs> _jobsApi;
        private Mock<IJobStore> _jobStore;
        private Mock<IInstanceCleanupQueue> _cleanupQueue;
        private Mock<ILogger<InstanceStoredNotificationService>> _loggerNotificationService;
        private Mock<ILogger<JobProcessorBase>> _loggerJobProcessorBase;
        private Mock<ILogger<AeTitleJobProcessor>> _logger;
        private Mock<IDicomToolkit> _dicomToolkit;
        private IList<InstanceStorageInfo> _instances;
        private IFileSystem _fileSystem;
        private string _patient1;
        private string _patient2;
        private DicomUID _study1;
        private DicomUID _study2;
        private DicomUID _study3;
        private DicomUID _series1;
        private DicomUID _series2;
        private DicomUID _series3;
        private DicomUID _series4;
        private string _aeTitle;

        public delegate void OutAction(string arg1, DicomTag agr2, out string arg3);

        public AeTitleJobProcessorTest()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _configuration = new ClaraApplicationEntity();
            _loggerNotificationService = new Mock<ILogger<InstanceStoredNotificationService>>();
            _loggerJobProcessorBase = new Mock<ILogger<JobProcessorBase>>();
            _logger = new Mock<ILogger<AeTitleJobProcessor>>();
            _cleanupQueue = new Mock<IInstanceCleanupQueue>();
            _notificationService = new InstanceStoredNotificationService(_loggerNotificationService.Object, _cleanupQueue.Object);
            _loggerFactory = new Mock<ILoggerFactory>();
            _jobsApi = new Mock<IJobs>();
            _jobStore = new Mock<IJobStore>();
            _instances = new List<InstanceStorageInfo>();
            _dicomToolkit = new Mock<IDicomToolkit>();
            _fileSystem = new MockFileSystem();

            _cleanupQueue.Setup(p => p.QueueInstance(It.IsAny<string>()));

            _loggerFactory.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns((string type) =>
            {
                return _logger.Object;
            });
            string expectedValue = string.Empty;
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out expectedValue))
                .Callback(new AeTitleJobProcessorTest.OutAction((string path, DicomTag tag, out string value) =>
                 {
                     var instance = _instances.First(p => p.InstanceStorageFullPath == path);
                     value = tag == DicomTag.PatientID ? instance.PatientId :
                                     tag == DicomTag.StudyInstanceUID ? instance.StudyInstanceUid :
                                     instance.SeriesInstanceUid;
                 })).Returns(true);

            _patient1 = "PATIENT1";
            _patient2 = "PATIENT2";
            _study1 = DicomUIDGenerator.GenerateDerivedFromUUID();
            _study2 = DicomUIDGenerator.GenerateDerivedFromUUID();
            _study3 = DicomUIDGenerator.GenerateDerivedFromUUID();
            _series1 = DicomUIDGenerator.GenerateDerivedFromUUID();
            _series2 = DicomUIDGenerator.GenerateDerivedFromUUID();
            _series3 = DicomUIDGenerator.GenerateDerivedFromUUID();
            _series4 = DicomUIDGenerator.GenerateDerivedFromUUID();
            _aeTitle = "AET1";

            GenerateInstances();
        }

        private void GenerateInstances()
        {
            var request = GenerateRequest(_patient1, _study1, _series1);
            var instance = InstanceStorageInfo.CreateInstanceStorageInfo(request, _fileSystem.Path.DirectorySeparatorChar.ToString(), _aeTitle, 1, _fileSystem);
            _instances.Add(instance);
            _fileSystem.File.CreateText(instance.InstanceStorageFullPath);

            request = GenerateRequest(_patient1, _study1, _series2);
            instance = InstanceStorageInfo.CreateInstanceStorageInfo(request, _fileSystem.Path.DirectorySeparatorChar.ToString(), _aeTitle, 1, _fileSystem);
            _instances.Add(instance);
            _fileSystem.File.CreateText(instance.InstanceStorageFullPath);

            request = GenerateRequest(_patient1, _study2, _series3);
            instance = InstanceStorageInfo.CreateInstanceStorageInfo(request, _fileSystem.Path.DirectorySeparatorChar.ToString(), _aeTitle, 1, _fileSystem);
            _instances.Add(instance);
            _fileSystem.File.CreateText(instance.InstanceStorageFullPath);

            request = GenerateRequest(_patient2, _study3, _series4);
            instance = InstanceStorageInfo.CreateInstanceStorageInfo(request, _fileSystem.Path.DirectorySeparatorChar.ToString(), _aeTitle, 1, _fileSystem);
            _instances.Add(instance);
            _fileSystem.File.CreateText(instance.InstanceStorageFullPath);
        }

        private DicomCStoreRequest GenerateRequest(string patientId, DicomUID studyInstanceUid, DicomUID seriesInstanceUid)
        {
            var dataset = new DicomDataset();
            dataset.Add(DicomTag.PatientID, patientId);
            dataset.Add(DicomTag.StudyInstanceUID, studyInstanceUid);
            dataset.Add(DicomTag.SeriesInstanceUID, seriesInstanceUid);
            dataset.Add(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.Add(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage.UID);
            var file = new DicomFile(dataset);
            return new DicomCStoreRequest(file);
        }

        [RetryFact(DisplayName = "InitializeSettings - Shall use default values if not specified")]
        public void InitializeSettings_ShallUseDefaultIfNotSpecified()
        {
            _configuration.AeTitle = "AET1";
            _configuration.ProcessorSettings.Add("pipeline-test", "PIPELINEID");
            _configuration.ProcessorSettings.Add("priority", "higher");
            var processor = new AeTitleJobProcessor(_configuration, _notificationService, _loggerFactory.Object, _jobsApi.Object, _jobStore.Object, _cleanupQueue.Object, _dicomToolkit.Object, _cancellationTokenSource.Token);

            _logger.VerifyLogging($"AE Title AET1 Processor Setting: timeout={AeTitleJobProcessor.DEFAULT_TIMEOUT_SECONDS}s", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"AE Title AET1 Processor Setting: groupBy={DicomTag.StudyInstanceUID}", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"AE Title AET1 Processor Setting: priority={JobPriority.Higher}", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Pipeline test=PIPELINEID added for AE Title AET1", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "InitializeSettings - throws if no pipeline defined")]
        public void InitializeSettings_ThrowsIfNoPipelineDefined()
        {
            _configuration.AeTitle = "AET1";

            var exception = Assert.Throws<ConfigurationException>(() =>
            {
                var processor = new AeTitleJobProcessor(_configuration, _notificationService, _loggerFactory.Object, _jobsApi.Object, _jobStore.Object, _cleanupQueue.Object, _dicomToolkit.Object, _cancellationTokenSource.Token);
            });

            Assert.Equal("No pipeline defined for AE Title AET1", exception.Message);

            _logger.VerifyLogging($"AE Title AET1 Processor Setting: timeout={AeTitleJobProcessor.DEFAULT_TIMEOUT_SECONDS}s", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"AE Title AET1 Processor Setting: groupBy={DicomTag.StudyInstanceUID}", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "ProcessJobs - Shall retry up to 3 times")]
        public void ProcessJobs_ShallRetryUpTo3Times()
        {
            var countDownEvent = new CountdownEvent(3);
            _jobsApi.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>()))
                .Callback(() =>
                {
                    countDownEvent.Signal();
                })
                .Throws(new System.Exception());

            _configuration.AeTitle = _aeTitle;
            _configuration.ProcessorSettings.Add("timeout", "1");
            _configuration.ProcessorSettings.Add("jobRetryDelay", "100");
            _configuration.ProcessorSettings.Add("pipeline-first", "PIPELINE1");

            var processor = new AeTitleJobProcessor(_configuration, _notificationService, _loggerFactory.Object, _jobsApi.Object, _jobStore.Object, _cleanupQueue.Object, _dicomToolkit.Object, _cancellationTokenSource.Token);

            _notificationService.NewInstanceStored(_instances.First());
            Assert.True(countDownEvent.Wait(7000));

            _jobsApi.Verify(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>()), Times.Exactly(3));
            _logger.VerifyLogging($"Failed to submit job, will retry later: PatientId={_instances.First().PatientId}, Study={_instances.First().StudyInstanceUid}", LogLevel.Information, Times.Exactly(2));
            _logger.VerifyLogging($"Failed to submit job after 3 retries: PatientId={_instances.First().PatientId}, Study={_instances.First().StudyInstanceUid}", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ProcessJobs - Shall ignore instance without specified DICOM tag")]
        public void ProcessJobs_ShallIgnoreInstancesWithoutSpeicifiedDicomTag()
        {
            _jobsApi.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>()));

            string expectedValue = string.Empty;
            _dicomToolkit.Reset();
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out expectedValue)).Returns(false);

            _configuration.AeTitle = _aeTitle;
            _configuration.ProcessorSettings.Add("timeout", "1");
            _configuration.ProcessorSettings.Add("jobRetryDelay", "100");
            _configuration.ProcessorSettings.Add("pipeline-first", "PIPELINE1");

            var processor = new AeTitleJobProcessor(_configuration, _notificationService, _loggerFactory.Object, _jobsApi.Object, _jobStore.Object, _cleanupQueue.Object, _dicomToolkit.Object, _cancellationTokenSource.Token);

            _notificationService.NewInstanceStored(_instances.First());
            Thread.Sleep(500);

            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out expectedValue), Times.Once());

            _logger.VerifyLogging($"Instance missing required DICOM key for grouping by {DicomTag.StudyInstanceUID}, ignoring", LogLevel.Error, Times.Once());
        }

        [RetryFact(DisplayName = "ProcessJobs - Shall stop upon cancellation request")]
        public void ProcessJobs_ShallStopUponCancellationRequest()
        {
            _configuration.AeTitle = _aeTitle;
            _configuration.ProcessorSettings.Add("timeout", "1");
            _configuration.ProcessorSettings.Add("pipeline-first", "PIPELINE1");

            var processor = new AeTitleJobProcessor(_configuration, _notificationService, _loggerFactory.Object, _jobsApi.Object, _jobStore.Object, _cleanupQueue.Object, _dicomToolkit.Object, _cancellationTokenSource.Token);
            _cancellationTokenSource.CancelAfter(250);
            Thread.Sleep(500);
            _logger.VerifyLoggingMessageBeginsWith($"AE Title Job Processor canceled", LogLevel.Warning, Times.Once());
        }

        [Theory(DisplayName = "Trigger jobs with different grouping and priority")]
        [InlineData("0010,0020", JobPriority.Higher)]
        [InlineData("0020,000D", JobPriority.Immediate)]
        [InlineData("0020,000E", JobPriority.Lower)]
        public void TriggerJobsWithGroupingAndPriority(string dicomTag, JobPriority jobPriority)
        {
            var grouping = DicomTag.Parse(dicomTag);
            _jobsApi.Setup(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<JobPriority>()))
                .Returns(Task.FromResult(new Job { JobId = "JOB", PayloadId = "PAYLOAD" }));

            _configuration.AeTitle = _aeTitle;
            _configuration.ProcessorSettings.Add("timeout", "1");
            _configuration.ProcessorSettings.Add("groupBy", grouping.ToString());
            _configuration.ProcessorSettings.Add("priority", jobPriority.ToString());
            _configuration.ProcessorSettings.Add("pipeline-first", "PIPELINE1");
            _configuration.ProcessorSettings.Add("pipeline-second", "PIPELINE2");

            var processor = new AeTitleJobProcessor(_configuration, _notificationService, _loggerFactory.Object, _jobsApi.Object, _jobStore.Object, _cleanupQueue.Object, _dicomToolkit.Object, _cancellationTokenSource.Token);

            foreach (var instance in _instances)
            {
                _notificationService.NewInstanceStored(instance);
            }

            Thread.Sleep(6500);

            // Verify configuration
            _logger.VerifyLogging($"AE Title AET1 Processor Setting: timeout={AeTitleJobProcessor.DEFAULT_TIMEOUT_SECONDS}s", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"AE Title AET1 Processor Setting: priority={jobPriority.ToString()}", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"AE Title AET1 Processor Setting: groupBy={grouping}", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Pipeline first=PIPELINE1 added for AE Title {_aeTitle}", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Pipeline second=PIPELINE2 added for AE Title {_aeTitle}", LogLevel.Information, Times.Once());

            // Verify notified instances
            switch (dicomTag)
            {
                case "00100020":
                    _logger.VerifyLogging($"Instance received and added with key {_patient1}", LogLevel.Debug, Times.Exactly(3));
                    _logger.VerifyLogging($"Instance received and added with key {_patient2}", LogLevel.Debug, Times.Exactly(1));
                    break;

                case "0020000D":
                    _logger.VerifyLogging($"Instance received and added with key {_study1.UID}", LogLevel.Debug, Times.Exactly(2));
                    _logger.VerifyLogging($"Instance received and added with key {_study2.UID}", LogLevel.Debug, Times.Exactly(1));
                    _logger.VerifyLogging($"Instance received and added with key {_study3.UID}", LogLevel.Debug, Times.Exactly(1));
                    break;

                case "0020000E":
                    _logger.VerifyLogging($"Instance received and added with key {_series1.UID}", LogLevel.Debug, Times.Exactly(1));
                    _logger.VerifyLogging($"Instance received and added with key {_series2.UID}", LogLevel.Debug, Times.Exactly(1));
                    _logger.VerifyLogging($"Instance received and added with key {_series3.UID}", LogLevel.Debug, Times.Exactly(1));
                    _logger.VerifyLogging($"Instance received and added with key {_series4.UID}", LogLevel.Debug, Times.Exactly(1));
                    break;
            }

            // Verify timer events
            switch (dicomTag)
            {
                case "00100020":
                    _logger.VerifyLogging($"New collection created for {_patient1}", LogLevel.Debug, Times.Once());
                    _logger.VerifyLogging($"New collection created for {_patient2}", LogLevel.Debug, Times.Once());
                    _logger.VerifyLogging($"Timeout elapsed waiting for {grouping} {_patient1}", LogLevel.Information, Times.Once());
                    _logger.VerifyLogging($"Timeout elapsed waiting for {grouping} {_patient2}", LogLevel.Information, Times.Once());
                    break;

                case "0020000D":
                    _logger.VerifyLogging($"New collection created for {_study1.UID}", LogLevel.Debug, Times.Once());
                    _logger.VerifyLogging($"New collection created for {_study2.UID}", LogLevel.Debug, Times.Once());
                    _logger.VerifyLogging($"New collection created for {_study3.UID}", LogLevel.Debug, Times.Once());
                    _logger.VerifyLogging($"Timeout elapsed waiting for {grouping} {_study1.UID}", LogLevel.Information, Times.Once());
                    _logger.VerifyLogging($"Timeout elapsed waiting for {grouping} {_study2.UID}", LogLevel.Information, Times.Once());
                    _logger.VerifyLogging($"Timeout elapsed waiting for {grouping} {_study3.UID}", LogLevel.Information, Times.Once());
                    break;

                case "0020000E":
                    _logger.VerifyLogging($"New collection created for {_series1.UID}", LogLevel.Debug, Times.Once());
                    _logger.VerifyLogging($"New collection created for {_series2.UID}", LogLevel.Debug, Times.Once());
                    _logger.VerifyLogging($"New collection created for {_series3.UID}", LogLevel.Debug, Times.Once());
                    _logger.VerifyLogging($"New collection created for {_series4.UID}", LogLevel.Debug, Times.Once());
                    _logger.VerifyLogging($"Timeout elapsed waiting for {grouping} {_series1.UID}", LogLevel.Information, Times.Once());
                    _logger.VerifyLogging($"Timeout elapsed waiting for {grouping} {_series2.UID}", LogLevel.Information, Times.Once());
                    _logger.VerifyLogging($"Timeout elapsed waiting for {grouping} {_series3.UID}", LogLevel.Information, Times.Once());
                    _logger.VerifyLogging($"Timeout elapsed waiting for {grouping} {_series4.UID}", LogLevel.Information, Times.Once());
                    break;
            }

            // Verify jobs generated
            switch (dicomTag)
            {
                case "00100020":
                    _logger.VerifyLogging($"Job generated {_aeTitle}-first-{_patient1} for pipeline PIPELINE1", LogLevel.Information, Times.Once());
                    _logger.VerifyLogging($"Job generated {_aeTitle}-second-{_patient1} for pipeline PIPELINE2", LogLevel.Information, Times.Once());
                    _logger.VerifyLogging($"Job generated {_aeTitle}-first-{_patient2} for pipeline PIPELINE1", LogLevel.Information, Times.Once());
                    _logger.VerifyLogging($"Job generated {_aeTitle}-second-{_patient2} for pipeline PIPELINE2", LogLevel.Information, Times.Once());
                    break;

                case "0020000D":
                    _logger.VerifyLogging($"Job generated {_aeTitle}-first-{_patient1} for pipeline PIPELINE1", LogLevel.Information, Times.Exactly(2));
                    _logger.VerifyLogging($"Job generated {_aeTitle}-second-{_patient1} for pipeline PIPELINE2", LogLevel.Information, Times.Exactly(2));
                    _logger.VerifyLogging($"Job generated {_aeTitle}-first-{_patient2} for pipeline PIPELINE1", LogLevel.Information, Times.Exactly(1));
                    _logger.VerifyLogging($"Job generated {_aeTitle}-second-{_patient2} for pipeline PIPELINE2", LogLevel.Information, Times.Exactly(1));

                    break;

                case "0020000E":
                    _logger.VerifyLogging($"Job generated {_aeTitle}-first-{_patient1} for pipeline PIPELINE1", LogLevel.Information, Times.Exactly(3));
                    _logger.VerifyLogging($"Job generated {_aeTitle}-second-{_patient1} for pipeline PIPELINE2", LogLevel.Information, Times.Exactly(3));
                    _logger.VerifyLogging($"Job generated {_aeTitle}-first-{_patient2} for pipeline PIPELINE1", LogLevel.Information, Times.Exactly(1));
                    _logger.VerifyLogging($"Job generated {_aeTitle}-second-{_patient2} for pipeline PIPELINE2", LogLevel.Information, Times.Exactly(1));
                    break;
            }

            // Verify new pipeline job events
            switch (dicomTag)
            {
                case "00100020":
                    _logger.VerifyLoggingMessageBeginsWith($"Submitting a new job", LogLevel.Information, Times.Exactly(4));
                    _logger.VerifyLogging($"Uploading 3 instances", LogLevel.Information, Times.Exactly(2));
                    _logger.VerifyLogging($"Uploading 1 instances", LogLevel.Information, Times.Exactly(2));
                    _logger.VerifyLogging($"Upload to payload completed", LogLevel.Information, Times.Exactly(4));
                    _jobsApi.Verify(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), jobPriority), Times.Exactly(4));
                    _jobsApi.Verify(p => p.Start(It.IsAny<Job>()), Times.Exactly(4));
                    break;

                case "0020000D":
                    _logger.VerifyLoggingMessageBeginsWith($"Submitting a new job", LogLevel.Information, Times.Exactly(6));
                    _logger.VerifyLogging($"Uploading 2 instances", LogLevel.Information, Times.Exactly(2));
                    _logger.VerifyLogging($"Uploading 1 instances", LogLevel.Information, Times.Exactly(4));
                    _logger.VerifyLogging($"Upload to payload completed", LogLevel.Information, Times.Exactly(6));
                    _jobsApi.Verify(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), jobPriority), Times.Exactly(6));
                    _jobsApi.Verify(p => p.Start(It.IsAny<Job>()), Times.Exactly(6));
                    break;

                case "0020000E":
                    _logger.VerifyLoggingMessageBeginsWith($"Submitting a new job", LogLevel.Information, Times.Exactly(8));
                    _logger.VerifyLogging($"Uploading 1 instances", LogLevel.Information, Times.Exactly(8));
                    _logger.VerifyLogging($"Upload to payload completed", LogLevel.Information, Times.Exactly(8));
                    _jobsApi.Verify(p => p.Create(It.IsAny<string>(), It.IsAny<string>(), jobPriority), Times.Exactly(8));
                    _jobsApi.Verify(p => p.Start(It.IsAny<Job>()), Times.Exactly(8));
                    break;
            }

            // Verify cleanups
            switch (dicomTag)
            {
                case "00100020":
                    _logger.VerifyLoggingMessageBeginsWith($"Notifying Disk Reclaimer to delete", LogLevel.Debug, Times.Exactly(2));
                    _logger.VerifyLoggingMessageBeginsWith($"Notified Disk Reclaimer to delete", LogLevel.Information, Times.Exactly(2));
                    break;

                case "0020000D":
                    _logger.VerifyLoggingMessageBeginsWith($"Notifying Disk Reclaimer to delete", LogLevel.Debug, Times.Exactly(3));
                    _logger.VerifyLoggingMessageBeginsWith($"Notified Disk Reclaimer to delete", LogLevel.Information, Times.Exactly(3));
                    break;

                case "0020000E":
                    _logger.VerifyLoggingMessageBeginsWith($"Notifying Disk Reclaimer to delete", LogLevel.Debug, Times.Exactly(4));
                    _logger.VerifyLoggingMessageBeginsWith($"Notified Disk Reclaimer to delete", LogLevel.Information, Times.Exactly(4));
                    break;
            }

            foreach (var instance in _instances)
            {
                _cleanupQueue.Verify(p => p.QueueInstance(instance.InstanceStorageFullPath), Times.Once());
            }
        }
    }
}