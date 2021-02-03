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
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Server.Common;
using System;
using System.Collections.Generic;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Scp
{
    public enum ChangedEventType
    {
        Added,
        Updated,
        Deleted
    }

    public class ClaraApplicationChangedEvent
    {
        public ClaraApplicationEntity ApplicationEntity { get; }

        public ChangedEventType Event { get; }

        public ClaraApplicationChangedEvent(ClaraApplicationEntity applicationEntity, ChangedEventType eventType)
        {
            ApplicationEntity = applicationEntity;
            Event = eventType;
        }
    }

    /// <summary>
    /// Interface for notifying any chnges to configured Clara Application Entities for Clara SCP service.
    /// </summary>
    public interface IClaraAeChangedNotificationService : IObservable<ClaraApplicationChangedEvent>
    {
        /// <summary>
        /// Notifies a new change.
        /// </summary>
        /// <param name="claraApplicationChangedEvent">Change event</param>
        void Notify(ClaraApplicationChangedEvent claraApplicationChangedEvent);
    }

    /// <inheritdoc/>
    public sealed class ClaraAeChangedNotificationService : IClaraAeChangedNotificationService
    {
        private readonly ILogger<ClaraAeChangedNotificationService> _logger;
        private readonly IList<IObserver<ClaraApplicationChangedEvent>> _observers;

        public ClaraAeChangedNotificationService(ILogger<ClaraAeChangedNotificationService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _observers = new List<IObserver<ClaraApplicationChangedEvent>>();
        }

        public IDisposable Subscribe(IObserver<ClaraApplicationChangedEvent> observer)
        {
            if (!_observers.Contains(observer))
            {
                _observers.Add(observer);
            }

            return new Unsubscriber<ClaraApplicationChangedEvent>(_observers, observer);
        }

        public void Notify(ClaraApplicationChangedEvent claraApplicationChangedEvent)
        {
            Guard.Against.Null(claraApplicationChangedEvent, nameof(claraApplicationChangedEvent));

            _logger.Log(LogLevel.Information, $"Notifying {_observers.Count} observers of Clara Application Entity {claraApplicationChangedEvent.Event}.");

            foreach (var observer in _observers)
            {
                try
                {
                    observer.OnNext(claraApplicationChangedEvent);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, "Error notifying observer.");
                }
            }
        }
    }
}