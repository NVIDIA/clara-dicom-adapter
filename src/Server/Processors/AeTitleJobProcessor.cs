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
using Dicom;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Processors
{
    [ProcessorValidation(ValidatorType = typeof(AeTitleJobProcessorValidator))]
    public class AeTitleJobProcessor : JobProcessorBase
    {
        private class InstanceCollection : List<InstanceStorageInfo>, IDisposable
        {
            public const int MAX_RETRY = 3;
            private Stopwatch _lastReceived;
            public string Key { get; }
            public int RetryCount { get; set; }

            public InstanceCollection(string key)
            {
                Guard.Against.NullOrWhiteSpace(key, nameof(key));
                _lastReceived = new Stopwatch();
                Key = key;
                RetryCount = 0;
            }

            public void AddInstance(InstanceStorageInfo value)
            {
                Guard.Against.Null(value, nameof(value));

                Add(value);
                _lastReceived.Reset();
                _lastReceived.Start();
            }

            public TimeSpan ElapsedTime()
            {
                return _lastReceived.Elapsed;
            }

            public bool IncrementAndRetry()
            {
                return ++RetryCount < MAX_RETRY;
            }

            public void Dispose()
            {
                _lastReceived.Stop();
                Clear();
            }
        }

        public const int DEFAULT_TIMEOUT_SECONDS = 5;
        public const int DEFAULT_JOB_RETRY_DELAY_MS = 5000;

        private readonly object SyncRoot = new object();
        private readonly ClaraApplicationEntity _configuration;
        private readonly IDicomToolkit _dicomToolkit;
        private readonly ILogger<AeTitleJobProcessor> _logger;
        private readonly Dictionary<string, InstanceCollection> _instances;
        private readonly Dictionary<string, string> _pipelines;
        private DicomTag _grouping;
        private bool _disposed = false;
        private int _timeout;
        private JobPriority _priority;
        private System.Timers.Timer _timer;
        private int _jobRetryDelay;
        private BlockingCollection<InstanceCollection> _jobs;
        private Task _jobProcessingTask;

        public override string Name => "AE Title Job Processor";
        public override string AeTitle => _configuration.AeTitle;

        public AeTitleJobProcessor(
            ClaraApplicationEntity configuration,
            IInstanceStoredNotificationService instanceStoredNotificationService,
            ILoggerFactory loggerFactory,
            IJobRepository jobStore,
            IInstanceCleanupQueue cleanupQueue,
            IDicomToolkit dicomToolkit,
            CancellationToken cancellationToken) : base(instanceStoredNotificationService, loggerFactory, jobStore, cleanupQueue, cancellationToken)
        {
            if (loggerFactory is null)
            {
                throw new ArgumentNullException(nameof(loggerFactory));
            }

            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _dicomToolkit = dicomToolkit ?? throw new ArgumentNullException(nameof(dicomToolkit));
            _instances = new Dictionary<string, InstanceCollection>();
            _pipelines = new Dictionary<string, string>();

            _logger = loggerFactory.CreateLogger<AeTitleJobProcessor>();

            _timer = new System.Timers.Timer(1000);
            _timer.AutoReset = false;
            _timer.Elapsed += OnTimedEvent;
            _timer.Enabled = true;

            _jobs = new BlockingCollection<InstanceCollection>();

            InitializeSettings();
            _jobProcessingTask = ProcessJobs();
        }

        ~AeTitleJobProcessor() => Dispose(false);

        public override void HandleInstance(InstanceStorageInfo value)
        {
            if (value is null)
            {
                throw new ArgumentNullException(nameof(value));
            };
            if (!value.CalledAeTitle.Equals(_configuration.AeTitle))
            {
                throw new InstanceNotSupportedException(value);
            };

            if (!_dicomToolkit.TryGetString(value.InstanceStorageFullPath, _grouping, out string key) ||
                string.IsNullOrWhiteSpace(key))
            {
                _logger.Log(LogLevel.Error, "Instance missing required DICOM key for grouping by {0}, ignoring", _grouping.ToString());
                return;
            }

            InstanceCollection collection = null;
            lock (SyncRoot)
            {
                if (_instances.TryGetValue(key, out InstanceCollection val))
                {
                    collection = val;
                }
                else
                {
                    collection = new InstanceCollection(key);
                    _instances.Add(key, collection);
                    _logger.Log(LogLevel.Debug, "New collection created for {0}", key);
                }
                collection.AddInstance(value);
            }
            _logger.Log(LogLevel.Debug, "Instance received and added with key {0}", key);
        }

        protected override void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            if (disposing)
            {
                _logger.Log(LogLevel.Debug, $"AE Title Job Process for {_configuration.AeTitle} disposing");

                _timer.Stop();
                _timer.Dispose();
                _jobs.CompleteAdding();
                _jobs.Dispose();
            }

            lock (SyncRoot)
            {
                _disposed = true;
            }

            base.Dispose(disposing);
        }

        private void OnTimedEvent(Object source, System.Timers.ElapsedEventArgs e)
        {
            _timer.Enabled = false;
            foreach (var key in _instances.Keys)
            {
                lock (SyncRoot)
                {
                    if (_instances[key].ElapsedTime().TotalSeconds > _timeout)
                    {
                        if (_instances[key].Count == 0)
                        {
                            _logger.Log(LogLevel.Warning, "Something's wrong, found no instances in collection with key={0}, grouping={1}", key, _grouping);
                            continue;
                        }
                        else
                        {
                            _ = _jobs.TryAdd(_instances[key]);
                            _instances.Remove(key);
                            _logger.Log(LogLevel.Information, $"Timeout elapsed waiting for {_grouping} {key}");
                        }
                    }
                }
            }
            _timer.Enabled = true;
        }

        private Task ProcessJobs()
        {
            return Task.Run(async () =>
            {
                while (!CancellationToken.IsCancellationRequested)
                {
                    lock (SyncRoot)
                    {
                        if (_disposed)
                        {
                            break;
                        }
                    }

                    try
                    {
                        var collection = _jobs.Take(CancellationToken);
                        if (!await ProcessJobs(collection))
                        {
                            if (collection.IncrementAndRetry())
                            {
                                _ = Task.Run(async () =>
                                {
                                    await Task.Delay(_jobRetryDelay);
                                    if (_jobs.TryAdd(collection))
                                    {
                                        _logger.Log(LogLevel.Information, $"Failed to submit job, will retry later: PatientId={collection.First().PatientId}, Study={collection.First().StudyInstanceUid}");
                                    }
                                });
                            }
                            else
                            {
                                _logger.Log(LogLevel.Error, $"Failed to submit job after {InstanceCollection.MAX_RETRY} retries: PatientId={collection.First().PatientId}, Study={collection.First().StudyInstanceUid}");
                                RemoveInstances(collection);
                                collection.Dispose();
                            }
                        }
                    }
                    catch (OperationCanceledException ex)
                    {
                        _logger.Log(LogLevel.Warning, "AE Title Job Processor canceled: {0}", ex.Message);
                    }
                    catch (InvalidOperationException ex)
                    {
                        _logger.Log(LogLevel.Warning, "AE Title Job Processor disposed: {0}", ex.Message);
                    }
                    catch (NullReferenceException ex)
                    {
                        _logger.Log(LogLevel.Warning, "Null instance collection found: {0}", ex.Message);
                    }
                }
            }, CancellationToken);
        }

        private async Task<bool> ProcessJobs(InstanceCollection collection)
        {
            Guard.Against.Null(collection, nameof(collection));

            using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "AE Title", _configuration.AeTitle } });
            _logger.Log(LogLevel.Information, "Processing a new job with grouping={0}, key={1}", _grouping, collection.Key);
            _instances.Remove(collection.Key, out _);

            // Setup a new job for each of the defined pipelines
            foreach (var pipelineKey in _pipelines.Keys)
            {
                try
                {
                    if (!_pipelines.TryGetValue(pipelineKey, out string pipelineId))
                    {
                        _logger.Log(LogLevel.Warning, "Something went wrong, pipeline was removed? '{0}'", pipelineKey);
                        continue;
                    }
                    var jobName = GenerateJobName(pipelineKey, collection.First());
                    using var _ = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobName", jobName }, { "PipelineId", pipelineId } });

                    _logger.Log(LogLevel.Information, "Job name generated.");
                    var basePath = collection.First().AeStoragePath;
                    await SubmitPipelineJob(jobName, pipelineId, _priority, basePath, collection);
                }
                catch (System.Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, "Failed to submit a new pipeline ({0}) job.", pipelineKey);
                    return false;
                }
            }

            // Cleanup instance
            RemoveInstances(collection);
            collection.Dispose();
            return true;
        }

        private string GenerateJobName(string pipelineName, InstanceStorageInfo instance)
        {
            Guard.Against.NullOrWhiteSpace(pipelineName, nameof(pipelineName));
            Guard.Against.Null(instance, nameof(instance));

            return $"{instance.CalledAeTitle}-{pipelineName}-{DateTime.UtcNow:yyyyMMddHHmmss}";
        }

        private void InitializeSettings()
        {
            _logger.Log(LogLevel.Information, "Initializing AE Title {0} with processor {1}", _configuration.AeTitle, _configuration.Processor);
            string setting = string.Empty;

            if (_configuration.ProcessorSettings.TryGetValue("timeout", out setting))
            {
                if (int.TryParse(setting, out int timeout))
                {
                    _timeout = timeout < 5 ? 5 : timeout;
                }
                else
                {
                    throw new ConfigurationException($"Invalid processor setting 'timeout' specified for AE Title {_configuration.AeTitle}");
                }
            }
            else
            {
                _timeout = DEFAULT_TIMEOUT_SECONDS;
            }
            _logger.Log(LogLevel.Information, "AE Title {0} Processor Setting: timeout={1}s", _configuration.AeTitle, _timeout);

            if (_configuration.ProcessorSettings.TryGetValue("jobRetryDelay", out setting))
            {
                if (int.TryParse(setting, out int delay))
                {
                    _jobRetryDelay = delay;
                }
                else
                {
                    throw new ConfigurationException($"Invalid processor setting 'jobRetryDelay' specified for AE Title {_configuration.AeTitle}");
                }
            }
            else
            {
                _jobRetryDelay = DEFAULT_JOB_RETRY_DELAY_MS;
            }
            _logger.Log(LogLevel.Information, "AE Title {0} Processor Setting: jobRetryDelay={1}ms", _configuration.AeTitle, _jobRetryDelay);

            if (_configuration.ProcessorSettings.TryGetValue("priority", out setting))
            {
                if (Enum.TryParse(setting, true, out JobPriority priority))

                {
                    _priority = priority;
                }
                else
                {
                    throw new ConfigurationException($"Invalid processor setting 'priority' specified for AE Title {_configuration.AeTitle}");
                }
            }
            else
            {
                _priority = JobPriority.Normal;
            }
            _logger.Log(LogLevel.Information, "AE Title {0} Processor Setting: priority={1}", _configuration.AeTitle, _priority);

            if (_configuration.ProcessorSettings.TryGetValue("groupBy", out setting))
            {
                try
                {
                    _grouping = DicomTag.Parse(setting);
                }
                catch (System.Exception ex)
                {
                    throw new ConfigurationException($"Invalid processor setting 'groupBy' specified for AE Title {_configuration.AeTitle}", ex);
                }
            }
            else
            {
                _grouping = DicomTag.StudyInstanceUID;
            }
            _logger.Log(LogLevel.Information, "AE Title {0} Processor Setting: groupBy={1}", _configuration.AeTitle, _grouping);

            foreach (var key in _configuration.ProcessorSettings.Keys)
            {
                if (key.StartsWith("pipeline-", StringComparison.OrdinalIgnoreCase))
                {
                    var name = key.Substring(9);
                    var value = _configuration.ProcessorSettings[key];
                    _pipelines.Add(name, value);
                    _logger.Log(LogLevel.Information, "Pipeline {0}={1} added for AE Title {2}", name, value, _configuration.AeTitle);
                }
            }

            if (_pipelines.Count == 0)
            {
                throw new ConfigurationException($"No pipeline defined for AE Title {_configuration.AeTitle}");
            }
        }
    }
}