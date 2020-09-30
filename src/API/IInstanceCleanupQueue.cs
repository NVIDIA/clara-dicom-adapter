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
using System.Threading;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// Interface of Instance Cleanup Queue
    /// </summary>
    public interface IInstanceCleanupQueue
    {
        /// <summary>
        /// Queue an new instance to be cleaned up.
        /// </summary>
        /// <param name="workItem">Instance to be cleaned up.</param>
        void QueueInstance(InstanceStorageInfo workItem);

        /// <summary>
        /// Dequeue an instance from the queue for cleanup.
        /// </summary>
        /// <param name="cancellationToken">Propagates notification that operations should be canceled.</param>
        InstanceStorageInfo Dequeue(CancellationToken cancellationToken);
    }
}