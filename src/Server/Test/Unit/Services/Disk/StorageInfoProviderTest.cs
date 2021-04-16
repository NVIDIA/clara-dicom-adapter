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
        private const long OneGB = 1000000000;
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
            _fileSystem.Setup(p => p.Directory.CreateDirectory(It.IsAny<string>()));
        }

        [Fact(DisplayName = "Available free space")]
        public void AvailableFreeSpace()
        {
            var totalSize = 10 * OneGB;
            var freeSpace = 9 * OneGB;
            _driveInfo.Setup(p => p.AvailableFreeSpace).Returns(freeSpace);
            _driveInfo.Setup(p => p.TotalSize).Returns(totalSize);
            _configuration.Value.Storage.Watermark = 10;
            _configuration.Value.Storage.ReserveSpaceGB = 1;

            var storageInfoProvider = new StorageInfoProvider(_configuration, _fileSystem.Object, _logger.Object);

            Assert.Equal(freeSpace, storageInfoProvider.AvailableFreeSpace);
            _logger.VerifyLogging($"Storage Size: {totalSize:N0}. Reserved: {(9 * OneGB):N0}.", LogLevel.Information, Times.Once());
        }

        [Fact(DisplayName = "Space is available...")]
        public void HasSpaceAvailableTo()
        {
            var totalSize = 10 * OneGB;
            var freeSpace = 9 * OneGB;
            _driveInfo.Setup(p => p.AvailableFreeSpace).Returns(freeSpace);
            _driveInfo.Setup(p => p.TotalSize).Returns(totalSize);
            _configuration.Value.Storage.Watermark = 90;
            _configuration.Value.Storage.ReserveSpaceGB = 1;
            var storageInfoProvider = new StorageInfoProvider(_configuration, _fileSystem.Object, _logger.Object);

            Assert.True(storageInfoProvider.HasSpaceAvailableForExport);
            Assert.True(storageInfoProvider.HasSpaceAvailableToRetrieve);
            Assert.True(storageInfoProvider.HasSpaceAvailableToStore);

            _logger.VerifyLogging($"Storage Size: {totalSize:N0}. Reserved: {(OneGB):N0}. Available: {freeSpace:N0}.", LogLevel.Debug, Times.Exactly(3));
        }

        [Fact(DisplayName = "Space usage is above watermark")]
        public void SpaceUsageAboveWatermark()
        {
            var totalSize = 10 * OneGB;
            var freeSpace = 5 * OneGB;
            _driveInfo.Setup(p => p.AvailableFreeSpace).Returns(freeSpace);
            _driveInfo.Setup(p => p.TotalSize).Returns(totalSize);
            _configuration.Value.Storage.Watermark = 10;
            _configuration.Value.Storage.ReserveSpaceGB = 1;
            var storageInfoProvider = new StorageInfoProvider(_configuration, _fileSystem.Object, _logger.Object);

            Assert.False(storageInfoProvider.HasSpaceAvailableForExport);
            Assert.False(storageInfoProvider.HasSpaceAvailableToRetrieve);
            Assert.False(storageInfoProvider.HasSpaceAvailableToStore);

            _logger.VerifyLogging($"Storage Size: {totalSize:N0}. Reserved: {(9 * OneGB):N0}. Available: {freeSpace:N0}.", LogLevel.Debug, Times.Exactly(3));
        }

        [Fact(DisplayName = "Reserved space is low")]
        public void ReservedSpaceIsLow()
        {
            var totalSize = 10 * OneGB;
            var freeSpace = 5 * OneGB;
            _driveInfo.Setup(p => p.AvailableFreeSpace).Returns(freeSpace);
            _driveInfo.Setup(p => p.TotalSize).Returns(totalSize);
            _configuration.Value.Storage.Watermark = 99;
            _configuration.Value.Storage.ReserveSpaceGB = 9;
            var storageInfoProvider = new StorageInfoProvider(_configuration, _fileSystem.Object, _logger.Object);

            Assert.False(storageInfoProvider.HasSpaceAvailableForExport);
            Assert.False(storageInfoProvider.HasSpaceAvailableToRetrieve);
            Assert.False(storageInfoProvider.HasSpaceAvailableToStore);

            _logger.VerifyLogging($"Storage Size: {totalSize:N0}. Reserved: {(9 * OneGB):N0}. Available: {freeSpace:N0}.", LogLevel.Debug, Times.Exactly(3));
        }
    }
}