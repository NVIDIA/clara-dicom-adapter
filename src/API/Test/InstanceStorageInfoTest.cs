﻿/*
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
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions.TestingHelpers;
using Dicom;
using Dicom.Network;
using Moq;
using Nvidia.Clara.DicomAdapter.Common;
using Serilog;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.API.Test
{
    public class InstanceStorageInfoTest
    {
        private DicomDataset _dataset;
        private DicomCStoreRequest _request;
        private MockFileSystem _fileSystem;
        private string _storageRoot;
        private string _studyInstanceUid;
        private string _seriesInstanceUid;
        private string _sopInstanceUid;

        public InstanceStorageInfoTest()
        {
            _studyInstanceUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
            _seriesInstanceUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
            _sopInstanceUid = DicomUIDGenerator.GenerateDerivedFromUUID().UID;
            _storageRoot = Path.DirectorySeparatorChar + "clara";
            _dataset = new DicomDataset
            {
                { DicomTag.SOPClassUID, "1.2.3" },
                { DicomTag.PatientID, "PID" },
                { DicomTag.StudyInstanceUID, _studyInstanceUid },
                { DicomTag.SeriesInstanceUID, _seriesInstanceUid },
                { DicomTag.SOPInstanceUID, _sopInstanceUid }
            };
            var file = new DicomFile(_dataset);
            _request = new DicomCStoreRequest(file);
            _fileSystem = new MockFileSystem(new Dictionary<string, MockFileData>
            {
                {_storageRoot, new MockDirectoryData() }
            });
        }

        [RetryFact(DisplayName = "Create with null DicomCStoreRequest shall throw exception")]
        public void CreateWithNullDicomCStoreRequestShallThrow()
        {
            Assert.Throws<ArgumentNullException>(() =>
            {
                InstanceStorageInfo.CreateInstanceStorageInfo(
                    null,
                    _storageRoot,
                    "AETITLE");
            });
        }

        [RetryFact(DisplayName = "Create without storageRootFullPath shall throw exception")]
        public void CreateWithoutStorageRootFullPathShallThrow()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                InstanceStorageInfo.CreateInstanceStorageInfo(
                    _request,
                    string.Empty,
                    "AETITLE");
            });
        }

        [RetryFact(DisplayName = "Create without sourceAeTitle shall throw exception")]
        public void CreateWithoutSourceAeTitleShallThrow()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                InstanceStorageInfo.CreateInstanceStorageInfo(
                    _request,
                    _storageRoot,
                    " ");
            });
        }

        [RetryFact(DisplayName = "Create a valid instance of InstanceStorageInfo")]
        public void CreateWithValidData()
        {
            var instance = InstanceStorageInfo.CreateInstanceStorageInfo(
                _request,
                _storageRoot,
                "MyAeTitle",
                _fileSystem);

            var expectedAaeStoragePath = Path.Combine(_storageRoot, "MyAeTitle");
            var expectedPatientStoragePath = Path.Combine(expectedAaeStoragePath, "PID");
            var expectedSeriesStoragePath = Path.Combine(expectedPatientStoragePath, Path.Combine(_studyInstanceUid, _seriesInstanceUid));
            var expectedInstanceStoragePath = Path.Combine(expectedSeriesStoragePath, $"{_sopInstanceUid}.dcm");

            Assert.Equal("1.2.3", instance.SopClassUid);
            Assert.Equal("PID", instance.PatientId);
            Assert.Equal(_studyInstanceUid, instance.StudyInstanceUid);
            Assert.Equal(_seriesInstanceUid, instance.SeriesInstanceUid);
            Assert.Equal(_sopInstanceUid, instance.SopInstanceUid);
            Assert.Equal("MyAeTitle", instance.CalledAeTitle);
            Assert.Equal(_storageRoot, instance.StorageRootPath);
            Assert.Equal(expectedAaeStoragePath, instance.AeStoragePath);
            Assert.Equal(expectedPatientStoragePath, instance.PatientStoragePath);
            Assert.Equal(expectedInstanceStoragePath, instance.InstanceStorageFullPath);

            Assert.True(_fileSystem.Directory.Exists(expectedSeriesStoragePath));

            var logger = new Mock<ILogger>();
            logger.Setup(p => p.Information("", ""));
        }
    }
}
