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

using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// Interface of the Results Service RESTful API
    /// </summary>
    public interface IResultsService
    {
        /// <summary>
        /// Retrieves a list of pending tasks assigned to an agent from Results Service
        /// </summary>
        /// <param name="agent">name of the agent</param>
        /// <param name="count">number of tasks to retrieve</param>
        /// <returns>List of tasks</returns>
        Task<IList<TaskResponse>> GetPendingJobs(string agent, CancellationToken cancellationToken, int count);

        /// <summary>
        /// Reports successful status to the Results Service for the specified task
        /// </summary>
        /// <param name="taskId">task to update</param>
        /// <returns>bool if call was successful; false otherwise</returns>
        Task<bool> ReportSuccess(Guid taskId, CancellationToken cancellationToken);

        /// <summary>
        /// Reports failed status to the Results Service for the specified task
        /// </summary>
        /// <param name="taskId">task to update</param>
        /// <param name="retryLater">indicates if task will be retries at a later time</param>
        /// <returns>bool if call was successful; false otherwise</returns>
        Task<bool> ReportFailure(Guid taskId, bool retryLater, CancellationToken cancellationToken);
    }
}