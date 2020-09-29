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
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Disk
{
    public class InstanceCleanupQueue : IInstanceCleanupQueue
    {
        private readonly BlockingCollection<InstanceStorageInfo> _workItems;
        private readonly ILogger<InstanceCleanupQueue> _logger;

        public InstanceCleanupQueue(ILogger<InstanceCleanupQueue> logger)
        {
            _workItems = new BlockingCollection<InstanceStorageInfo>();
            _logger = logger;
        }

        public void QueueInstance(InstanceStorageInfo workItem)
        {
            if (workItem is null)
            {
                throw new ArgumentNullException(nameof(workItem));
            }

            _workItems.Add(workItem);
            _logger.Log(LogLevel.Debug, "Instance added to cleanup queue {0}. Queue size: {1}", workItem.SopInstanceUid, _workItems.Count);
        }

        public InstanceStorageInfo Dequeue(CancellationToken cancellationToken)
        {
            return _workItems.Take(cancellationToken);
        }
    }
}
