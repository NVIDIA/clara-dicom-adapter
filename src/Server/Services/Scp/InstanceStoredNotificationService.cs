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
using Nvidia.Clara.DicomAdapter.API;
using System;
using System.Collections.Generic;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Scp
{
    /// <summary>
    /// Service for publishing/observing DICOM instances stored notifications.
    /// </summary>
    public sealed class InstanceStoredNotificationService : IInstanceStoredNotificationService
    {
        private readonly ILogger<InstanceStoredNotificationService> _logger;
        private IList<IObserver<InstanceStorageInfo>> _observers;
        private readonly IInstanceCleanupQueue _cleanupQueue;

        public InstanceStoredNotificationService(ILogger<InstanceStoredNotificationService> logger,
            IInstanceCleanupQueue cleanupQueue)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _observers = new List<IObserver<InstanceStorageInfo>>();
            _cleanupQueue = cleanupQueue ?? throw new ArgumentNullException(nameof(cleanupQueue));
        }

        public IDisposable Subscribe(IObserver<InstanceStorageInfo> observer)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }
            return new Unsubscriber<InstanceStorageInfo>(_observers, observer);
        }

        public void NewInstanceStored(InstanceStorageInfo instance)
        {
            Guard.Against.Null(instance, nameof(instance));

            _logger.Log(LogLevel.Information, "Notifying {0} observers of new instance stored {1}", _observers.Count, instance.SopInstanceUid);

            var observerHandledInstances = 0;
            foreach (var observer in _observers)
            {
                try
                {
                    observer.OnNext(instance);
                    observerHandledInstances++;
                }
                catch (InstanceNotSupportedException)
                {
                    //no op
                }
                catch (ArgumentNullException ex)
                {
                    _logger.Log(LogLevel.Error, ex, "Received a null instance");
                }
            }

            if (observerHandledInstances == 0)
            {
                _logger.Log(LogLevel.Warning, "Instance not supported by any of the configured AE Titles, notifying Storage Space Reclaimer Service.");
                _cleanupQueue.QueueInstance(instance.InstanceStorageFullPath);
            }
        }
    }

    /// <summary>
    /// Unsubscriber class is intended to be used as the return value of <code>InstanceStoredNotificationService.Subscribe</code>
    /// so the subscriber can easily unsubscribe to the events.
    /// </summary>
    /// <typeparam name="InstanceStorageInfo"></typeparam>

    internal class Unsubscriber<InstanceStorageInfo> : IDisposable
    {
        private IList<IObserver<InstanceStorageInfo>> _observers;
        private IObserver<InstanceStorageInfo> _observer;

        internal Unsubscriber(IList<IObserver<InstanceStorageInfo>> observers, IObserver<InstanceStorageInfo> observer)
        {
            _observers = observers ?? throw new ArgumentNullException(nameof(observers));
            _observer = observer ?? throw new ArgumentNullException(nameof(observer));
        }

        public void Dispose()
        {
            if (_observers.Contains(_observer))
            {
                _observers.Remove(_observer);
            }
        }
    }
}