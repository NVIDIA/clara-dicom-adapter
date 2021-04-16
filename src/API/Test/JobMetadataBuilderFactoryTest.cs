/*
 * Apache License, Version 2.0
 * Copyright 2021 NVIDIA Corporation
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
            _dicomToolkit.Setup(p => p.Open(It.IsAny<string>())).Returns(MockDicomFile());
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value)).Returns(true);

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.Open(It.IsAny<string>()), Times.Never());
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value), Times.Never());
        }

        [Fact]
        public void Build_WithHasValidHeaderThrowsException()
        {
            var value = "value";
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Throws(new Exception("error"));
            _dicomToolkit.Setup(p => p.Open(It.IsAny<string>())).Returns(MockDicomFile());
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value)).Returns(true);

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.Open(It.IsAny<string>()), Times.Never());
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value), Times.Never());
        }

        [Fact]
        public void Build_WithOpenThrowsException()
        {
            var value = "value";
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Returns(true);
            _dicomToolkit.Setup(p => p.Open(It.IsAny<string>())).Throws(new Exception("error"));
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value)).Returns(true);

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.Open(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value), Times.Never());
        }

        [Fact]
        public void Build_WithTryGetStringReturningFalse()
        {
            var value = "value";
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Returns(true);
            _dicomToolkit.Setup(p => p.Open(It.IsAny<string>())).Returns(MockDicomFile());
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value)).Returns(false);

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.Open(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value), Times.Exactly(files.Count * dicomTags.Count));
        }

        [Fact]
        public void Build_WithTryGetStringThrowingException()
        {
            var value = "value";
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Returns(true);
            _dicomToolkit.Setup(p => p.Open(It.IsAny<string>())).Returns(MockDicomFile());
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value))
                .Throws(new Exception("error"));

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.Open(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value), Times.Exactly(files.Count * dicomTags.Count));
        }

        delegate bool TryGetStringDelegate(DicomFile dicomFile, DicomTag dicomTag, out string value);


        [Fact]
        public void Build_ReturnsWithAllValidDicomTagsParsedFromAllFilesWithUniqueValues()
        {
            var value = Guid.NewGuid().ToString("N");
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Returns((string value) =>
            {
                return true;
            });
            _dicomToolkit.Setup(p => p.Open(It.IsAny<string>())).Returns(MockDicomFile());
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out value)).Returns(true);

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "00100010", "00200020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            foreach (var key in dicomTags)
            {
                Assert.True(result.ContainsKey(key));
                Assert.Equal(value, result[key]);
            }

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.Open(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out It.Ref<string>.IsAny), Times.Exactly(files.Count * dicomTags.Count));
        }

        [Fact]
        public void Build_ReturnsWithAllValidDicomTagsParsedFromAllFilesWithMultipleValues()
        {
            var counter = new HashSet<DicomTag>();
            var expected = new Dictionary<string, string>();
            _dicomToolkit.Setup(p => p.HasValidHeader(It.IsAny<string>())).Returns((string value) =>
            {
                return true;
            });
            _dicomToolkit.Setup(p => p.Open(It.IsAny<string>())).Returns(MockDicomFile());
            _dicomToolkit.Setup(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out It.Ref<string>.IsAny))
                .Returns(new TryGetStringDelegate((DicomFile dicomFile, DicomTag dicomTag, out string value) =>
                {
                    value = Guid.NewGuid().ToString("N");
                    var count = 0;
                    if(counter.Contains(dicomTag))
                    {
                        count++;
                    }
                    expected.Add($"{dicomTag.Group:X4}{dicomTag.Element:X4}-{count}", value);
                    counter.Add(dicomTag);
                    return true;
                }));

            var factory = new JobMetadataBuilderFactory(_logger.Object, _dicomToolkit.Object);

            var dicomTags = new List<string>() { "0010,0010", "0020,0020" };
            var files = new List<string>() { "/file1", "/file2" };
            var result = factory.Build(true, dicomTags, files);

            Assert.True(result.ContainsKey("Instances"));
            Assert.Equal("2", result["Instances"]);

            foreach(var key in expected.Keys)
            {
                Assert.True(result.ContainsKey(key));
                Assert.Equal(expected[key], result[key]);
            }

            _dicomToolkit.Verify(p => p.HasValidHeader(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.Open(It.IsAny<string>()), Times.Exactly(files.Count));
            _dicomToolkit.Verify(p => p.TryGetString(It.IsAny<DicomFile>(), It.IsAny<DicomTag>(), out It.Ref<string>.IsAny), Times.Exactly(files.Count * dicomTags.Count));
        }

        private DicomFile MockDicomFile()
        {
            var dicomFile = new DicomFile();            
            return dicomFile;
        }
    }
}
