using Dicom;
using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.Common;
using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.API.Test
{
    public class JobMetadataBuilderFactoryTest
    {
        private readonly Mock<ILogger<JobMetadataBuilderFactory>> _logger;
        private readonly Mock<IDicomToolkit> _dicomToolkit;

        public JobMetadataBuilderFactoryTest()
        {
            _logger = new Mock<ILogger<JobMetadataBuilderFactory>>();
            _dicomToolkit = new Mock<IDicomToolkit>();
        }

        [Fact]
        public void Build_WithUplaodMetadataDisabled()
        {
            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var result = factory.Build(false, null, new List<string>());

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("0", result["Instances"]);
        }

        [Fact]
        public void Build_WithoutMetadataDefined()
        {
            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var result = factory.Build(true, null, new List<string>());

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("0", result["Instances"]);
        }

        [Fact]
        public void Build_WithoutValidDicomHeader()
        {
            var value = "value";
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Returns(false);
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value)).Returns(true);

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value), Times.Never());
        }

        [Fact]
        public void Build_WithHasValidHeaderThrowingException()
        {
            var value = "value";
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Throws(new Exception("error"));
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value)).Returns(true);

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value), Times.Never());
        }

        [Fact]
        public void Build_WithTryGetStringReturningFalse()
        {
            var value = "value";
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Returns(true);
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value)).Returns(false);

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value), Times.Exactly(files.Count * dicomTags.Count));
        }

        [Fact]
        public void Build_WithTryGetStringThrowingException()
        {
            var value = "value";
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Returns(true);
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value))
                .Throws(new Exception("error"));

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value), Times.Exactly(files.Count * dicomTags.Count));
        }

        [Fact]
        public void Build_ReturnsWithAllValidDicomTagsParsedFromLastFile()
        {
            var value = "value";
            _dicomToolkit.SetupSequence(p => p.HasValidHeader(It.IsAny<string>()))
                .Returns(false)
                .Returns(true);
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value)).Returns(true);

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value), Times.Exactly(files.Count));
        }

        [Fact]
        public void Build_ReturnsWithAllValidDicomTagsParsedFromFirstFile()
        {
            var value = "value";
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Returns(true);
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value)).Returns(true);

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Once());
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<string>(), It.IsAny<DicomTag>(), out value), Times.Exactly(files.Count));
        }
    }
}
