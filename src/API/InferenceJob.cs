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

using Ardalis.GuardClauses;
using Newtonsoft.Json;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Generic;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// Status of an inference job submission.
    /// </summary>
    public enum InferenceJobStatus
    {
        Success,
        Fail
    }

    /// <summary>
    /// State of a inference job submission.
    /// </summary>
    public enum InferenceJobState
    {
        Queued,
        Creating,
        Created,
        MetadataUploading,
        MetadataUploaded,
        PayloadUploading,
        PayloadUploaded,
        Starting,
        Completed,
        Faulted
    }

    /// <summary>
    /// InferenceJob is used to track status a of job that is to be submitted to the Clara Platform service.
    /// It is used internally by the JobSubmissionService.
    /// </summary>
    public class InferenceJob : Job
    {
        public Guid InferenceJobId { get; set; } = Guid.NewGuid();
        public string PipelineId { get; set; }
        public string JobPayloadsStoragePath { get; private set; }
        public int TryCount { get; set; } = 0;
        public InferenceJobState State { get; set; } = InferenceJobState.Queued;
        public DateTime LastUpdate { get; set; } = DateTime.MinValue;
        public string JobName { get; set; }
        public JobPriority Priority { get; set; }
        public string Source { get; set; }

        [JsonIgnore]
        public IList<InstanceStorageInfo> Instances { get; set; }

        public InferenceJob()
        {
            JobId = Guid.NewGuid().ToString("N");
            PayloadId = Guid.NewGuid().ToString("N");
            Instances = new List<InstanceStorageInfo>();
        }

        public void SetStoragePath(string targetStoragePath)
        {
            Guard.Against.NullOrWhiteSpace(targetStoragePath, nameof(targetStoragePath));

            JobPayloadsStoragePath = targetStoragePath;
        }
    }
}