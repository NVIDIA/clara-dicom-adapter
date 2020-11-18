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

using Moq;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Common.Test
{
    public class FileSystemExtensionsTest
    {
        [Fact]
        public void CreateDirectoryIfNotExists_ShallCreateDirectoryIfNotExists()
        {
            var dirToBeCreated = "/my/dir5";
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(p => p.Directory.Exists(It.IsAny<string>())).Returns(false);
            fileSystem.Setup(p => p.Directory.CreateDirectory(It.IsAny<string>()));
            fileSystem.Object.Directory.CreateDirectoryIfNotExists(dirToBeCreated);

            fileSystem.Verify(p => p.Directory.Exists(It.IsAny<string>()), Times.Once());
            fileSystem.Verify(p => p.Directory.CreateDirectory(dirToBeCreated), Times.Once());
        }

        [Fact]
        public void CreateDirectoryIfNotExists_ShallNotCreateDirectoryIfExists()
        {
            var dirToBeCreated = "/my/dir5";
            var fileSystem = new Mock<IFileSystem>();
            fileSystem.Setup(p => p.Directory.Exists(It.IsAny<string>())).Returns(true);
            fileSystem.Setup(p => p.Directory.CreateDirectory(It.IsAny<string>()));
            fileSystem.Object.Directory.CreateDirectoryIfNotExists(dirToBeCreated);

            fileSystem.Verify(p => p.Directory.Exists(It.IsAny<string>()), Times.Once());
            fileSystem.Verify(p => p.Directory.CreateDirectory(dirToBeCreated), Times.Never());
        }

        [Fact]
        public void TryDelete_ReturnsTrueOnSuccessful()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                { "/src/"     , new MockDirectoryData() }
            });

            Assert.True(fileSystem.Directory.TryDelete("/src"));
        }

        [Fact]
        public void TryDelete_ReturnsFalseOnFailure()
        {
            var fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
            });

            Assert.False(fileSystem.Directory.TryDelete("/src"));
        }
    }
}