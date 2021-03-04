using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System.IO.Abstractions;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit.Services.Disk
{
    public class StorageInfoProviderTest
    {
        private const long OneGb = 1000000000;
        private Mock<IFileSystem> _fileSystem;
        private Mock<ILogger<StorageInfoProvider>> _logger;
        private IOptions<DicomAdapterConfiguration> _configuration;
        private Mock<IDriveInfo> _driveInfo;

        public StorageInfoProviderTest()
        {
            _fileSystem = new Mock<IFileSystem>();
            _logger = new Mock<ILogger<StorageInfoProvider>>();
            _configuration = Options.Create(new DicomAdapterConfiguration());
            _driveInfo = new Mock<IDriveInfo>();

            _fileSystem.Setup(p => p.DriveInfo.FromDriveName(It.IsAny<string>()))
                    .Returns(_driveInfo.Object);
        }

        [Fact(DisplayName = "Available free space")]
        public void AvailableFreeSpace()
        {
            _driveInfo.Setup(p => p.AvailableFreeSpace).Returns(100);
            var storageInfoProvider = new StorageInfoProvider(_configuration, _fileSystem.Object, _logger.Object);

            Assert.Equal(100, storageInfoProvider.AvailableFreeSpace);
        }

        [Fact(DisplayName = "Space is available...")]
        public void HasSpaceAvailableTo()
        {
            _driveInfo.Setup(p => p.AvailableFreeSpace).Returns(90 * OneGb);
            _driveInfo.Setup(p => p.TotalSize).Returns(100 * OneGb);
            var storageInfoProvider = new StorageInfoProvider(_configuration, _fileSystem.Object, _logger.Object);

            Assert.True(storageInfoProvider.HasSpaceAvailableForExport);
            Assert.True(storageInfoProvider.HasSpaceAvailableToRetrieve);
            Assert.True(storageInfoProvider.HasSpaceAvailableToStore);

            _logger.VerifyLogging($"Space used: 10.00%. Available: {90 * OneGb}.", LogLevel.Trace, Times.Exactly(3));
        }

        [Fact(DisplayName = "Space usage is above watermark")]
        public void SpaceUsageAboveWatermark()
        {
            _driveInfo.Setup(p => p.AvailableFreeSpace).Returns(50 * OneGb);
            _driveInfo.Setup(p => p.TotalSize).Returns(100 * OneGb);
            _configuration.Value.Storage.Watermark = 10;
            var storageInfoProvider = new StorageInfoProvider(_configuration, _fileSystem.Object, _logger.Object);

            Assert.False(storageInfoProvider.HasSpaceAvailableForExport);
            Assert.False(storageInfoProvider.HasSpaceAvailableToRetrieve);
            Assert.False(storageInfoProvider.HasSpaceAvailableToStore);
            
            _logger.VerifyLogging($"Space used: 50.00%. Available: {50 * OneGb}.", LogLevel.Trace, Times.Exactly(3));
        }

        [Fact(DisplayName = "Reserved space is low")]
        public void ReservedSpaceIsLow()
        {
            _driveInfo.Setup(p => p.AvailableFreeSpace).Returns(50 * OneGb);
            _driveInfo.Setup(p => p.TotalSize).Returns(100 * OneGb);
            _configuration.Value.Storage.ReservedSpaceGb = 100;
            var storageInfoProvider = new StorageInfoProvider(_configuration, _fileSystem.Object, _logger.Object);

            Assert.False(storageInfoProvider.HasSpaceAvailableForExport);
            Assert.False(storageInfoProvider.HasSpaceAvailableToRetrieve);
            Assert.False(storageInfoProvider.HasSpaceAvailableToStore);

            _logger.VerifyLogging($"Space used: 50.00%. Available: {50 * OneGb}.", LogLevel.Trace, Times.Exactly(3));
        }
    }
}