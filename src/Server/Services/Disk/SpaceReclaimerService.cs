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
using System.IO.Abstractions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Disk
{
    public class SpaceReclaimerService : IHostedService
    {
        private readonly ILogger<SpaceReclaimerService> _logger;
        private readonly IInstanceCleanupQueue _taskQueue;
        private readonly IFileSystem _fileSystem;

        public SpaceReclaimerService(IInstanceCleanupQueue taskQueue, ILogger<SpaceReclaimerService> logger, IFileSystem fileSystem)
        {
            _taskQueue = taskQueue ?? throw new ArgumentNullException(nameof(taskQueue));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
        }

        private void BackgroundProcessing(CancellationToken stoppingToken)
        {
            _logger.Log(LogLevel.Information, "Disk Space Reclaimer Hosted Service is running.");
            while (!stoppingToken.IsCancellationRequested)
            {
                _logger.Log(LogLevel.Debug, "Waiting for instance...");
                var workItem = _taskQueue.Dequeue(stoppingToken);

                if (workItem == null) continue; // likely cancelled

                try
                {
                    _logger.Log(LogLevel.Debug, "Deleting file {0}", workItem.InstanceStorageFullPath);
                    if (_fileSystem.File.Exists(workItem.InstanceStorageFullPath))
                    {
                        _fileSystem.File.Delete(workItem.InstanceStorageFullPath);
                        _logger.Log(LogLevel.Debug, "File deleted {0}", workItem.InstanceStorageFullPath);
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, $"Error occurred deleting file {workItem.InstanceStorageFullPath}.");
                }
            }
            _logger.Log(LogLevel.Information, "Cancellation requested.");
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var task = Task.Run(() =>
            {
                BackgroundProcessing(cancellationToken);
            });

            if (task.IsCompleted)
                return task;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Disk Space Reclaimer Hosted Service is stopping.");
            return Task.CompletedTask;
        }
    }
}
