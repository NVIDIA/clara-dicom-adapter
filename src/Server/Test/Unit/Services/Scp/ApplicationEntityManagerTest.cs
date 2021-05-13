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

using Dicom;
using Dicom.Network;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class ApplicationEntityManagerTest
    {
        private Mock<IHostApplicationLifetime> _hostApplicationLifetime;
        private Mock<IServiceScopeFactory> _serviceScopeFactory;
        private Mock<IServiceScope> _serviceScope;
        private Mock<ILoggerFactory> _loggerFactory;
        private Mock<ILogger<ApplicationEntityManager>> _logger;
        private Mock<IInstanceStoredNotificationService> _notificationService;
        private Mock<IJobs> _jobsApi;
        private Mock<IJobRepository> _jobStore;
        private MockFileSystem _fileSystem;
        private Mock<IDicomToolkit> _dicomToolkit;
        private Mock<IInstanceCleanupQueue> _cleanupQueue;
        private Mock<IClaraAeChangedNotificationService> _claraAeChangedNotificationService;
        private Mock<IDicomAdapterRepository<ClaraApplicationEntity>> _claraApplicationEntityRepository;
        private IServiceProvider _serviceProvider;
        private IOptions<DicomAdapterConfiguration> _connfiguration;
        private Mock<IStorageInfoProvider> _storageInfoProvider;

        public ApplicationEntityManagerTest()
        {
            _hostApplicationLifetime = new Mock<IHostApplicationLifetime>();
            _serviceScopeFactory = new Mock<IServiceScopeFactory>();
            _serviceScope = new Mock<IServiceScope>();
            _loggerFactory = new Mock<ILoggerFactory>();
            _logger = new Mock<ILogger<ApplicationEntityManager>>();
            _notificationService = new Mock<IInstanceStoredNotificationService>();
            _jobsApi = new Mock<IJobs>();
            _jobStore = new Mock<IJobRepository>();
            _fileSystem = new MockFileSystem();
            _dicomToolkit = new Mock<IDicomToolkit>();
            _cleanupQueue = new Mock<IInstanceCleanupQueue>();
            _claraAeChangedNotificationService = new Mock<IClaraAeChangedNotificationService>();
            _claraApplicationEntityRepository = new Mock<IDicomAdapterRepository<ClaraApplicationEntity>>();
            _connfiguration = Options.Create<DicomAdapterConfiguration>(new DicomAdapterConfiguration());
            _storageInfoProvider = new Mock<IStorageInfoProvider>();

            var services = new ServiceCollection();
            services.AddScoped(p => _loggerFactory.Object);
            services.AddScoped(p => _notificationService.Object);
            services.AddScoped(p => _jobsApi.Object);
            services.AddScoped(p => _jobStore.Object);
            services.AddScoped<IFileSystem>(p => _fileSystem);
            services.AddScoped(p => _dicomToolkit.Object);
            services.AddScoped(p => _cleanupQueue.Object);
            services.AddScoped(p => _claraApplicationEntityRepository.Object);

            _serviceProvider = services.BuildServiceProvider();

            _serviceScopeFactory.Setup(p => p.CreateScope()).Returns(_serviceScope.Object);
            _serviceScope.Setup(p => p.ServiceProvider).Returns(_serviceProvider);

            _loggerFactory.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns((string type) =>
            {
                return _logger.Object;
            });
        }

        [RetryFact(DisplayName = "HandleCStoreRequest - Shall throw if AE Title not configured")]
        public void HandleCStoreRequest_ShallThrowIfAENotConfigured()
        {
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableToStore).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);
            var manager = new ApplicationEntityManager(_hostApplicationLifetime.Object,
                                                       _serviceScopeFactory.Object,
                                                       _claraAeChangedNotificationService.Object,
                                                       _connfiguration,
                                                       _storageInfoProvider.Object);

            var request = GenerateRequest();
            var exception = Assert.Throws<ArgumentException>(() =>
            {
                manager.HandleCStoreRequest(request, "BADAET", 1);
            });

            Assert.Equal("Called AE Title 'BADAET' is not configured", exception.Message);
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableToStore, Times.Never());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        [RetryFact(DisplayName = "HandleCStoreRequest - Shall save instance through AE Handler")]
        public void HandleCStoreRequest_ShallSaveInstanceThroughAEHandler()
        {
            var aet = "TESTAET";

            var data = new List<ClaraApplicationEntity>()
            {
                new ClaraApplicationEntity()
                {
                    AeTitle = aet,
                    Name =aet,
                    Processor = typeof(MockJobProcessor).AssemblyQualifiedName
                }
            };
            _claraApplicationEntityRepository.Setup(p => p.AsQueryable()).Returns(data.AsQueryable());
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableToStore).Returns(true);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);
            var manager = new ApplicationEntityManager(_hostApplicationLifetime.Object,
                                                       _serviceScopeFactory.Object,
                                                       _claraAeChangedNotificationService.Object,
                                                       _connfiguration,
                                                       _storageInfoProvider.Object);

            var request = GenerateRequest();
            manager.HandleCStoreRequest(request, aet, 2);

            _logger.VerifyLogging($"{aet} added to AE Title Manager", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Patient ID: {request.Dataset.GetSingleValue<string>(DicomTag.PatientID)}", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Study Instance UID: {request.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID)}", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Series Instance UID: {request.Dataset.GetSingleValue<string>(DicomTag.SeriesInstanceUID)}", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith($"Storage File Path:", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Instance saved with handler", LogLevel.Debug, Times.Once());

            _claraApplicationEntityRepository.Verify(p => p.AsQueryable(), Times.Once());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableToStore, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.Never());
        }

        [RetryFact(DisplayName = "HandleCStoreRequest - Throws when available storage space is low")]
        public void HandleCStoreRequest_ThrowWhenOnLowStorageSpace()
        {
            _storageInfoProvider.Setup(p => p.HasSpaceAvailableToStore).Returns(false);
            _storageInfoProvider.Setup(p => p.AvailableFreeSpace).Returns(100);
            var aet = "TESTAET";

            var data = new List<ClaraApplicationEntity>()
            {
                new ClaraApplicationEntity()
                {
                    AeTitle = aet,
                    Name =aet,
                    Processor = typeof(MockJobProcessor).AssemblyQualifiedName
                }
            };
            _claraApplicationEntityRepository.Setup(p => p.AsQueryable()).Returns(data.AsQueryable());
            var manager = new ApplicationEntityManager(_hostApplicationLifetime.Object,
                                                       _serviceScopeFactory.Object,
                                                       _claraAeChangedNotificationService.Object,
                                                       _connfiguration,
                                                       _storageInfoProvider.Object);

            var request = GenerateRequest();
            Assert.Throws<InsufficientStorageAvailableException>(() =>
            {
                manager.HandleCStoreRequest(request, aet, 2);
            });

            _logger.VerifyLogging($"{aet} added to AE Title Manager", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Patient ID: {request.Dataset.GetSingleValue<string>(DicomTag.PatientID)}", LogLevel.Information, Times.Never());
            _logger.VerifyLogging($"Study Instance UID: {request.Dataset.GetSingleValue<string>(DicomTag.StudyInstanceUID)}", LogLevel.Information, Times.Never());
            _logger.VerifyLogging($"Series Instance UID: {request.Dataset.GetSingleValue<string>(DicomTag.SeriesInstanceUID)}", LogLevel.Information, Times.Never());
            _logger.VerifyLoggingMessageBeginsWith($"Storage File Path:", LogLevel.Information, Times.Never());
            _logger.VerifyLogging($"Instance saved with handler", LogLevel.Debug, Times.Never());

            _claraApplicationEntityRepository.Verify(p => p.AsQueryable(), Times.Once());
            _storageInfoProvider.Verify(p => p.HasSpaceAvailableToStore, Times.AtLeastOnce());
            _storageInfoProvider.Verify(p => p.AvailableFreeSpace, Times.AtLeastOnce());
        }

        [RetryFact(DisplayName = "IsAeTitleConfigured")]
        public void IsAeTitleConfigured()
        {
            var aet = "TESTAET";
            var data = new List<ClaraApplicationEntity>()
            {
                new ClaraApplicationEntity()
                {
                    AeTitle = aet,
                    Name =aet,
                    Processor = typeof(MockJobProcessor).AssemblyQualifiedName
                }
            };
            _claraApplicationEntityRepository.Setup(p => p.AsQueryable()).Returns(data.AsQueryable());
            var manager = new ApplicationEntityManager(_hostApplicationLifetime.Object,
                                                       _serviceScopeFactory.Object,
                                                       _claraAeChangedNotificationService.Object,
                                                       _connfiguration,
                                                       _storageInfoProvider.Object);

            Assert.True(manager.IsAeTitleConfigured(aet));
            Assert.False(manager.IsAeTitleConfigured("BAD"));
        }

        [RetryFact(DisplayName = "NextAssociationNumber - Shall reset to zero")]
        public void NextAssociationNumber_ShallResetToZero()
        {
            var manager = new ApplicationEntityManager(_hostApplicationLifetime.Object,
                                                       _serviceScopeFactory.Object,
                                                       _claraAeChangedNotificationService.Object,
                                                       _connfiguration,
                                                       _storageInfoProvider.Object);

            for (uint i = 1; i < 10; i++)
            {
                Assert.Equal(i, manager.NextAssociationNumber());
            }
        }

        [RetryFact(DisplayName = "GetService - Shall return request service")]
        public void GetService_ShallReturnRequestedServicec()
        {
            var manager = new ApplicationEntityManager(_hostApplicationLifetime.Object,
                                                       _serviceScopeFactory.Object,
                                                       _claraAeChangedNotificationService.Object,
                                                       _connfiguration,
                                                       _storageInfoProvider.Object);

            Assert.Equal(manager.GetService<ILoggerFactory>(), _loggerFactory.Object);
            Assert.Equal(manager.GetService<IJobRepository>(), _jobStore.Object);
        }

        private DicomCStoreRequest GenerateRequest()
        {
            var dataset = new DicomDataset();
            dataset.Add(DicomTag.PatientID, "PID");
            dataset.Add(DicomTag.StudyInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.Add(DicomTag.SeriesInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.Add(DicomTag.SOPInstanceUID, DicomUIDGenerator.GenerateDerivedFromUUID());
            dataset.Add(DicomTag.SOPClassUID, DicomUID.SecondaryCaptureImageStorage.UID);
            var file = new DicomFile(dataset);
            return new DicomCStoreRequest(file);
        }
    }
}