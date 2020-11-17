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

using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Scu;
using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks.Dataflow;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Services.Scu
{
    internal class ExportTaskWatcher : IDisposable
    {
        private static readonly object SyncRoot = new object();
        private readonly ILogger<ScuService> _logger;
        private readonly IResultsService _resultsService;
        private readonly ScuConfiguration _scuConfiguration;
        private System.Timers.Timer _workerTimer;
        private bool _isWatching;

        public ExportTaskWatcher(ILogger<ScuService> logger, IResultsService iResultsService, ScuConfiguration scuConfiguration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _resultsService = iResultsService ?? throw new ArgumentNullException(nameof(iResultsService));
            _scuConfiguration = scuConfiguration;
        }

        public void Start(ActionBlock<OutputJob> actionBlockQueue, CancellationToken cancellationToken)
        {
            Guard.Against.Null(actionBlockQueue, nameof(actionBlockQueue));
            Guard.Against.Null(cancellationToken, nameof(cancellationToken));

            if (_isWatching) return;

            lock (SyncRoot)
            {
                _isWatching = true;
            }

            _workerTimer = new System.Timers.Timer(500);
            _workerTimer.Elapsed += (sender, e) =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    return;
                }
                MonitorForTasks(actionBlockQueue, cancellationToken);
            };
            _workerTimer.AutoReset = false;
            _workerTimer.Start();
            _logger.LogInformation("Store SCU service started monitoring for export tasks.");
        }

        public void Stop()
        {
            lock (SyncRoot)
            {
                _isWatching = false;
                _workerTimer.Stop();
            }
            _logger.LogInformation("Store SCU service stopped monitoring export tasks.");
        }

        private void MonitorForTasks(ActionBlock<OutputJob> actionBlockQueue, CancellationToken cancellationToken)
        {
            try
            {
                QueryForTasks(actionBlockQueue, cancellationToken);
            }
            catch (System.Exception ex)
            {
                _logger.LogError("Error while querying for tasks from Results Service: {0}", ex);
            }
            finally
            {
                if (_isWatching)
                {
                    _workerTimer.Start();
                }
            }
        }

        private async void QueryForTasks(ActionBlock<OutputJob> actionBlockQueue, CancellationToken cancellationToken)
        {
            var tasks = await _resultsService.GetPendingJobs(cancellationToken, 10);
            if (tasks == null || tasks.Count == 0) return;
            _logger.LogInformation("Found {0} tasks from Results Service...", tasks.Count);

            var invalidTasks = new List<TaskResponse>();
            OutputJob outputJob = null;
            foreach (var task in tasks)
            {
                try
                {
                    outputJob = CreateOutputJobFromTask(task);
                }
                catch (System.Exception ex)
                {
                    _logger.LogError("Error creating export task for {0}: {1}", task.TaskId, ex);
                    invalidTasks.Add(task);
                    continue;
                }

                try
                {
                    actionBlockQueue.Post(outputJob);
                    _logger.LogInformation("Export task {0} queued", task.TaskId);
                }
                catch (System.Exception ex)
                {
                    _logger.LogError("Error queueing task {0}: {1}", outputJob.TaskId, ex);
                    //do nothing, will try again on next query.
                }
            }

            if (invalidTasks.Any())
                ReportFailures(invalidTasks, cancellationToken);
        }

        private void ReportFailures(List<TaskResponse> invalidTasks, CancellationToken cancellationToken)
        {
            foreach (var task in invalidTasks)
            {
                try
                {
                    _resultsService.ReportFailure(task.TaskId, false, cancellationToken);
                    _logger.LogWarning("Task {0} marked as failrue and will not be retried.", task.TaskId);
                }
                catch (System.Exception ex)
                {
                    _logger.LogWarning("Failed to mark task {0} as failure: {1}", task.TaskId, ex);
                }
            }
        }

        private OutputJob CreateOutputJobFromTask(TaskResponse task)
        {
            if (string.IsNullOrEmpty(task.Parameters))
                throw new ConfigurationException("Task Parameter is missing destination");

            var dest = JsonConvert.DeserializeObject<string>(task.Parameters);
            var destination = _scuConfiguration.Destinations
                .FirstOrDefault(p => p.Name.Equals(dest, StringComparison.InvariantCultureIgnoreCase));

            if (destination == null)
                throw new ConfigurationException($"Configured destination is invalid {dest}. Available destinations are: {string.Join(",", _scuConfiguration.Destinations.Select(p => p.Name).ToArray())}");

            return new OutputJob(task, _logger, _resultsService, destination);
        }

        public void Dispose()
        {
            lock (SyncRoot)
            {
                _isWatching = false;
                _workerTimer.Stop();
                _workerTimer.Dispose();
            }
        }
    }
}