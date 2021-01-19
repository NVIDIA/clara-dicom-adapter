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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Threading;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class ApplicationEntityHandlerTest
    {
        private CancellationTokenSource _cancellationTokenSource;
        private IServiceProvider _serviceProvider;
        private Mock<ILoggerFactory> _loggerFactory;
        private IFileSystem _fileSystem;
        private Mock<ILogger<ApplicationEntityHandler>> _logger;
        private Mock<IDicomToolkit> _dicomToolkit;
        private Mock<IInstanceStoredNotificationService> _notificationService;
        private Mock<IJobs> _jobsApi;
        private Mock<IJobStore> _jobStore;
        private Mock<IInstanceCleanupQueue> _cleanupQueue;
        private string _rootStoragePath;

        public ApplicationEntityHandlerTest()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _fileSystem = new MockFileSystem();
            _loggerFactory = new Mock<ILoggerFactory>();
            _logger = new Mock<ILogger<ApplicationEntityHandler>>();
            _dicomToolkit = new Mock<IDicomToolkit>();
            _notificationService = new Mock<IInstanceStoredNotificationService>();
            _jobsApi = new Mock<IJobs>();
            _jobStore = new Mock<IJobStore>();
            _cleanupQueue = new Mock<IInstanceCleanupQueue>();

            var services = new ServiceCollection();
            services.AddScoped<ILoggerFactory>(p => _loggerFactory.Object);
            services.AddScoped<ILogger<ApplicationEntityHandler>>(p => _logger.Object);
            services.AddScoped<IDicomToolkit>(p => _dicomToolkit.Object);
            services.AddScoped<IInstanceStoredNotificationService>(p => _notificationService.Object);
            services.AddScoped<IJobs>(p => _jobsApi.Object);
            services.AddScoped<IJobStore>(p => _jobStore.Object);
            services.AddScoped<IFileSystem>(p => _fileSystem);
            services.AddScoped<IInstanceCleanupQueue>(p => _cleanupQueue.Object);

            _serviceProvider = services.BuildServiceProvider();

            _loggerFactory.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns((string type) =>
            {
                return _logger.Object;
            });
            _rootStoragePath = "/storage";
        }

        [RetryFact(DisplayName = "Shall remove existing data at startup")]
        public void ShallRemoveExistingDataAtStartup()
        {
            var config = new ClaraApplicationEntity();
            config.AeTitle = "my-aet";
            config.IgnoredSopClasses = new List<string>() { DicomUID.SecondaryCaptureImageStorage.UID };
            config.Processor = "Nvidia.Clara.DicomAdapter.Test.Unit.MockJobProcessor, Nvidia.Clara.Dicom.Test.Unit";
            var rootPath = _fileSystem.Path.Combine(_rootStoragePath, config.AeTitle.RemoveInvalidPathChars());
            _fileSystem.Directory.CreateDirectory(rootPath);
            _fileSystem.File.Create(_fileSystem.Path.Combine(rootPath, "test.txt"));
#pragma warning disable xUnit2013
            Assert.Equal(1, _fileSystem.Directory.GetFiles(rootPath).Count());

            var handler = new ApplicationEntityHandler(_serviceProvider, config, _rootStoragePath, _cancellationTokenSource.Token, _fileSystem);

            _logger.VerifyLogging($"Existing AE Title storage directory {rootPath} found, deleting...", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Existing AE Title storage directory {rootPath} deleted.", LogLevel.Information, Times.Once());
            Assert.Equal(0, _fileSystem.Directory.GetFiles(rootPath).Count());
#pragma warning restore xUnit2013
        }

        [RetryFact(DisplayName = "Shall ignore instances with configured SOP Class UIDs")]
        public void ShallIngoreInstancesWithConfiguredSopClassUids()
        {
            _dicomToolkit.Setup(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()));

            var config = new ClaraApplicationEntity();
            config.AeTitle = "my-aet";
            config.IgnoredSopClasses = new List<string>() { DicomUID.SecondaryCaptureImageStorage.UID };
            config.Processor = "Nvidia.Clara.DicomAdapter.Test.Unit.MockJobProcessor, Nvidia.Clara.Dicom.Test.Unit";
            var handler = new ApplicationEntityHandler(_serviceProvider, config, _rootStoragePath, _cancellationTokenSource.Token, _fileSystem);

            var request = InstanceGenerator.GenerateDicomCStoreRequest();
            var instance = InstanceStorageInfo.CreateInstanceStorageInfo(request, _rootStoragePath, config.AeTitle, 1, _fileSystem);
            handler.Save(request, instance);

            _logger.VerifyLogging($"Instance with SOP Class {DicomUID.SecondaryCaptureImageStorage.UID} ignored based on configured AET {config.AeTitle}", LogLevel.Warning, Times.Once());
            _dicomToolkit.Verify(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()), Times.Never());
        }

        [RetryFact(DisplayName = "Shall respect retry policy on failures")]
        public void ShallRespectRetryPolicyOnFailures()
        {
            _dicomToolkit.Setup(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>())).Throws<Exception>();

            var config = new ClaraApplicationEntity();
            config.AeTitle = "my-aet";
            config.Processor = "Nvidia.Clara.DicomAdapter.Test.Unit.MockJobProcessor, Nvidia.Clara.Dicom.Test.Unit";
            var handler = new ApplicationEntityHandler(_serviceProvider, config, _rootStoragePath, _cancellationTokenSource.Token, _fileSystem);

            var request = InstanceGenerator.GenerateDicomCStoreRequest();
            var instance = InstanceStorageInfo.CreateInstanceStorageInfo(request, _rootStoragePath, config.AeTitle, 1, _fileSystem);

            var exception = Assert.Throws<Exception>(() =>
            {
                handler.Save(request, instance);
            });

            _logger.VerifyLogging(LogLevel.Error, Times.Exactly(3));
            _dicomToolkit.Verify(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()), Times.Exactly(4));
        }

        [RetryFact(DisplayName = "Shall overwrite instance if configured")]
        public void ShouldOverwriteInstanceIfConfigured()
        {
            _dicomToolkit.Setup(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()));
            _notificationService.Setup(p => p.NewInstanceStored(It.IsAny<InstanceStorageInfo>()));

            var config = new ClaraApplicationEntity();
            config.AeTitle = "my-aet";
            config.Processor = "Nvidia.Clara.DicomAdapter.Test.Unit.MockJobProcessor, Nvidia.Clara.Dicom.Test.Unit";
            config.OverwriteSameInstance = true;
            var handler = new ApplicationEntityHandler(_serviceProvider, config, _rootStoragePath, _cancellationTokenSource.Token, _fileSystem);

            var request = InstanceGenerator.GenerateDicomCStoreRequest();
            var instance = InstanceStorageInfo.CreateInstanceStorageInfo(request, _rootStoragePath, config.AeTitle, 1, _fileSystem);

            handler.Save(request, instance);
            _fileSystem.File.Create(instance.InstanceStorageFullPath);
            handler.Save(request, instance);

            _logger.VerifyLogging(LogLevel.Error, Times.Never());
            _logger.VerifyLogging("Overwriting existing instance.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Instance saved successfully.", LogLevel.Debug, Times.Exactly(2));
            _logger.VerifyLogging("Instance stored and notified successfully.", LogLevel.Information, Times.Exactly(2));

            _dicomToolkit.Verify(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()), Times.Exactly(2));
            _notificationService.Verify(p => p.NewInstanceStored(instance), Times.Exactly(2));
        }

        [RetryFact(DisplayName = "Shall save and notify")]
        public void ShallSaveAndNotify()
        {
            _dicomToolkit.Setup(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()));
            _notificationService.Setup(p => p.NewInstanceStored(It.IsAny<InstanceStorageInfo>()));

            var config = new ClaraApplicationEntity();
            config.AeTitle = "my-aet";
            config.Processor = "Nvidia.Clara.DicomAdapter.Test.Unit.MockJobProcessor, Nvidia.Clara.Dicom.Test.Unit";
            var handler = new ApplicationEntityHandler(_serviceProvider, config, _rootStoragePath, _cancellationTokenSource.Token, _fileSystem);

            var request = InstanceGenerator.GenerateDicomCStoreRequest();
            var instance = InstanceStorageInfo.CreateInstanceStorageInfo(request, _rootStoragePath, config.AeTitle, 1, _fileSystem);

            handler.Save(request, instance);
            _fileSystem.File.Create(instance.InstanceStorageFullPath);
            handler.Save(request, instance);

            _logger.VerifyLogging(LogLevel.Error, Times.Never());
            _logger.VerifyLogging("Instance already exists, skipping.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging("Instance saved successfully.", LogLevel.Debug, Times.Once());
            _logger.VerifyLogging("Instance stored and notified successfully.", LogLevel.Information, Times.Once());

            _dicomToolkit.Verify(p => p.Save(It.IsAny<DicomFile>(), It.IsAny<string>()), Times.Exactly(1));
            _notificationService.Verify(p => p.NewInstanceStored(instance), Times.Once());
        }
    }
}