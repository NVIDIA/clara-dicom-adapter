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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.Platform;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// <code>JobProcessorBase</code> is an abstraction layer to simplify the job submission process to Clara
    /// Platform API.  This allows one to customize the grouping of received DICOM instances and group them 
    /// based on their workflow needs.!
    /// 
    /// <see cref="JobProcessorBase.Name">Name</see>, <see cref="JobProcessorBase.AeTitle">AeTitle</see> and 
    /// <see cref="JobProcessorBase.HandleInstance(InstanceStorageInfo)">HandleInstance(InstanceStorageInfo value)</see>
    /// are the required properties and method to be implemented.  
    /// <see cref="JobProcessorBase.SubmitPipelineJob(string, string, JobPriority, string, IList{InstanceStorageInfo})">SubmitPipelineJob(...)</see> 
    /// may be used to submit a new job to the Clara Platform API.
    /// <see cref="JobProcessorBase.RemoveInstances(List{InstanceStorageInfo})">RemoveInstances(...)</see> shall be called once job is submitted and can be removed from the
    /// temporary storage.
    /// </summary>
    public abstract class JobProcessorBase : IDisposable, IObserver<InstanceStorageInfo>
    {
        private readonly IInstanceStoredNotificationService _instanceStoredNotificationService;
        private readonly ILogger _logger;
        private readonly IJobs _jobsApi;
        private readonly IPayloads _payloadsApi;
        private readonly IInstanceCleanupQueue _cleanupQueue;
        private bool _disposed = false;
        private IDisposable _cancelSubscription;
        protected CancellationToken CancellationToken { get; }

        public abstract string Name { get; }
        public abstract string AeTitle { get; }

        public JobProcessorBase(
            IInstanceStoredNotificationService instanceStoredNotificationService,
            ILoggerFactory loggerFactory,
            IJobs jobsApi,
            IPayloads payloadsApi,
            IInstanceCleanupQueue cleanupQueue,
            CancellationToken cancellationToken)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _instanceStoredNotificationService = instanceStoredNotificationService ?? throw new ArgumentNullException(nameof(instanceStoredNotificationService));
            _logger = loggerFactory.CreateLogger<JobProcessorBase>();
            _jobsApi = jobsApi ?? throw new ArgumentNullException(nameof(jobsApi));
            _payloadsApi = payloadsApi ?? throw new ArgumentNullException(nameof(payloadsApi));
            _cleanupQueue = cleanupQueue ?? throw new ArgumentNullException(nameof(cleanupQueue));
            CancellationToken = cancellationToken;
            _cancelSubscription = _instanceStoredNotificationService.Subscribe(this);
        }

        ~JobProcessorBase() => Dispose(false);

        protected async Task SubmitPipelineJob(string jobName, string pipelineId, JobPriority jobPriority, string basePath, IList<InstanceStorageInfo> instances)
        {
            Guard.Against.NullOrWhiteSpace(pipelineId, nameof(pipelineId));
            if (instances.IsNullOrEmpty())
                throw new ArgumentNullException(nameof(instances));

            jobName = jobName.FixJobName();
            Guard.Against.NullOrWhiteSpace(jobName, nameof(jobName));
            
            _logger.Log(LogLevel.Information, "Submitting a new job '{0}' with pipeline '{1}', priority={2}, instance count={3}", jobName, pipelineId, jobPriority, instances.Count);

            var job = await _jobsApi.Create(pipelineId, jobName, jobPriority);
            using (_logger.BeginScope(new Dictionary<string, object> { { "JobId", job.JobId }, { "PayloadId", job.PayloadId } }))
            {
                await UploadFiles(job, basePath, instances);
                await _jobsApi.Start(job);
            }
        }

        protected void RemoveInstances(List<InstanceStorageInfo> instances)
        {
            _logger.Log(LogLevel.Debug, $"Notifying Disk Reclaimer to delete {instances.Count} instances.");
            foreach (var instance in instances)
            {
                _cleanupQueue.QueueInstance(instance);
            }
            _logger.Log(LogLevel.Information, $"Notified Disk Reclaimer to delete {instances.Count} instances.");
        }

        private async Task UploadFiles(Job job, string basePath, IList<InstanceStorageInfo> instances)
        {
            _logger.Log(LogLevel.Information, "Uploading {0} instances", instances.Count);
            await _payloadsApi.Upload(job.PayloadId, basePath, instances.Select(p => p.InstanceStorageFullPath));
            _logger.Log(LogLevel.Information, "Upload to payload completed");
        }

        public override string ToString()
        {
            return Name;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        // Protected implementation of Dispose pattern.
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _cancelSubscription.Dispose();
            }

            _disposed = true;
        }

        public abstract void HandleInstance(InstanceStorageInfo value);

        public void OnCompleted()
        {
            //not used
        }

        public void OnError(Exception error)
        {
            _logger.Log(LogLevel.Error, error, "Error occurred while processing instance.");
        }

        public void OnNext(InstanceStorageInfo value)
        {
            if (value.CalledAeTitle.CompareTo(AeTitle) == 0)
            {
                HandleInstance(value);
            }
            else
            {
                throw new InstanceNotSupportedException(value);
            }
        }
    }
}
