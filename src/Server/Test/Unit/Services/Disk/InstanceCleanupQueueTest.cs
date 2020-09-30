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
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Threading;
using System.Threading.Tasks;
using Dicom;
using Dicom.Network;
using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class InstanceCleanupQueueTest
    {
        private Mock<ILogger<InstanceCleanupQueue>> _logger;
        private InstanceCleanupQueue _queue;
        private CancellationTokenSource _cancellationTokenSource;

        public InstanceCleanupQueueTest()
        {
            _cancellationTokenSource = new CancellationTokenSource();
            _logger = new Mock<ILogger<InstanceCleanupQueue>>();
            _queue = new InstanceCleanupQueue(_logger.Object);
        }

        [RetryFact(DisplayName = "QueueInstance - Shall throw if null")]
        public void QueueInstance_ShallThrowOnNull()
        {
            var exception = Assert.Throws<ArgumentNullException>(() =>
            {
                _queue.QueueInstance(null);
            });
        }

        [RetryFact(DisplayName = " Shall queue and dequeue items")]
        public void ShallQueueAndDequeueItems()
        {
            for (var i = 0; i < 10; i++)
            {
                _queue.QueueInstance(InstanceStorageInfo.CreateInstanceStorageInfo(
                    GenerateRequest(),
                    "/test",
                    "AET",
                    new MockFileSystem()
                ));
            }

            _cancellationTokenSource.CancelAfter(500);
            var items = new List<InstanceStorageInfo>();
            for (var i = 0; i < 10; i++)
            {
                items.Add(_queue.Dequeue(_cancellationTokenSource.Token));
            }

            Assert.Equal(10, items.Count);
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
