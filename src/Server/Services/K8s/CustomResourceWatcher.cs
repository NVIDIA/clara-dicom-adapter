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
using k8s;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.Common;

namespace Nvidia.Clara.DicomAdapter.Server.Services.K8s
{
    /// <summary>
    /// A Kubernetes Custom Resource Watcher.
    /// Due to stability issues with the k8s client, this watcher tracks change events and notifies
    /// subscribers through the provided delegate.
    /// </summary>
    public class CustomResourceWatcher<S, T>
        where S : CustomResourceList<T>
        where T : CustomResource
    {
        private readonly object SyncLock = new object();
        private readonly ILogger _logger;
        private readonly IKubernetesWrapper _client;
        private readonly CustomResourceDefinition _crd;
        private readonly Action<WatchEventType, T> _handle;
        private readonly Dictionary<string, T> _cache;
        private readonly CancellationToken _cancellationToken;
        private System.Timers.Timer _timer;

        public CustomResourceWatcher(
            ILogger logger,
            IKubernetesWrapper client,
            CustomResourceDefinition crd,
            CancellationToken cancellationToken,
            Action<WatchEventType, T> handle)
        {
            Guard.Against.Null(logger, "iLogger");
            Guard.Against.Null(client, "client");
            Guard.Against.Null(crd, "crd");
            Guard.Against.OutOfRange(crd.ApiVersion.Split('/').Count(), "crd.ApiVersion", 2, 2);

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _crd = crd ?? throw new ArgumentNullException(nameof(crd));
            _cancellationToken = cancellationToken;
            _handle = handle ?? throw new ArgumentNullException(nameof(handle));
            _cache = new Dictionary<string, T>();
        }

        public void Start(double interval)
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                _logger.Log(LogLevel.Information, "Cancallation requested, CRD watcher will not be set.");
                return;
            }

            _logger.Log(LogLevel.Information, $"{GetType()} Start called with interval {interval}ms");
            _timer = new System.Timers.Timer(interval);
            _timer.Elapsed += async (s, e) => await Poll();
            _timer.AutoReset = false;
            _timer.Start();
        }

        public void Stop()
        {
            _logger.Log(LogLevel.Information, $"{GetType()} Stop called");
            _timer.Stop();
            _timer.Dispose();
        }

        private async Task Poll()
        {
            if (_cancellationToken.IsCancellationRequested)
            {
                _logger.Log(LogLevel.Information, "Cancallation requested, CRD watcher stopped.");
                return;
            }

            try
            {
                _logger.Log(LogLevel.Debug, $"Retrieving changes for CRD {_crd.ApiVersion}/{_crd.Kind}");
                var result = await _client.ListNamespacedCustomObjectWithHttpMessagesAsync(_crd).ConfigureAwait(false);

                if (result is null)
                {
                    _logger.Log(LogLevel.Warning, $"No CRD found, make sure CRD is setup correctly with expected version: {_crd.ApiVersion}/{_crd.Kind}");
                    return;
                }

                result.Response.EnsureSuccessStatusCode();

                var json = await result.Response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<S>(json);

                if (data == null)
                {
                    throw new CrdPollException($"Data serialized to null: {json}");
                }

                if (data.Items.IsNullOrEmpty())
                {
                    _logger.Log(LogLevel.Warning, $"No CRD found in type: {_crd.ApiVersion}/{_crd.Kind}");

                    if (_cache.Any())
                    {
                        RemoveDeleted(_cache.Keys.AsEnumerable());
                    }
                    return;
                }

                foreach (var item in data?.Items)
                {
                    if (!_cache.ContainsKey(item.Metadata.Name))
                    {
                        _handle(WatchEventType.Added, item as T);
                        _cache.Add(item.Metadata.Name, item);
                        _logger.Log(LogLevel.Debug, $"CRD {_crd.ApiVersion}/{_crd.Kind} > {item.Metadata.Name} added");
                    }
                }
                var toBeRemoved = _cache.Keys.Except(data.Items.Select(p => p.Metadata.Name));
                RemoveDeleted(toBeRemoved);
            }
            catch (System.Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, $"Error polling CRD of type {_crd.ApiVersion}/{_crd.Kind}.");
            }
            finally
            {
                lock (SyncLock)
                {
                    _timer.Start();
                }
            }
        }

        private void RemoveDeleted(IEnumerable<string> toBeRemoved)
        {
            foreach (var key in toBeRemoved)
            {
                var item = _cache[key] as T;
                _handle(WatchEventType.Deleted, item);
                _cache.Remove(key);
                _logger.Log(LogLevel.Debug, $"CRD {_crd.ApiVersion}/{_crd.Kind} > {item.Metadata.Name} removed");
            }
        }
    }
}
