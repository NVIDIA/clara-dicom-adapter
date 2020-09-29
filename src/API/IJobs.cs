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

using System.Threading.Tasks;
using Nvidia.Clara.Platform;

namespace Nvidia.Clara.DicomAdapter.API
{    
    public class Job
    {
        public string JobId { get; set; }
        public string PayloadId { get; set; }
    }

    /// <summary>
    /// Interface wrapper for the Platform Jobs API
    /// </summary>
    public interface IJobs
    {
        /// <summary>
        /// Creates a new job
        /// </summary>
        /// <param name="pipelineId">Pipeline ID to create a new job from.</param>
        /// <param name="jobName">Name of the job.</param>
        /// <param name="jobPriority">Priority of the job.</param>
        Task<Job> Create(string pipelineId, string jobName, JobPriority jobPriority);

        /// <summary>
        /// Starts the job
        /// </summary>
        /// <param name="job">Job to start.</param>
        Task Start(Job job);
    }
}
