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

using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;
using System;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit.Services.Scp
{
    public class ClaraAeChangedNotificationServiceTest
    {
        private readonly Mock<ILogger<ClaraAeChangedNotificationService>> _logger;

        public ClaraAeChangedNotificationServiceTest()
        {
            _logger = new Mock<ILogger<ClaraAeChangedNotificationService>>();
        }

        [Fact(DisplayName = "Workflow Test")]
        public void WorkflowTest()
        {
            var service = new ClaraAeChangedNotificationService(_logger.Object);
            var observer = new Mock<IObserver<ClaraApplicationChangedEvent>>();
            observer.Setup(p => p.OnNext(It.IsAny<ClaraApplicationChangedEvent>()));

            var cancel = service.Subscribe(observer.Object);
            service.Notify(new ClaraApplicationChangedEvent(new API.ClaraApplicationEntity(), ChangedEventType.Added));
            service.Notify(new ClaraApplicationChangedEvent(new API.ClaraApplicationEntity(), ChangedEventType.Deleted));
            service.Notify(new ClaraApplicationChangedEvent(new API.ClaraApplicationEntity(), ChangedEventType.Updated));

            observer.Verify(p => p.OnNext(It.IsAny<ClaraApplicationChangedEvent>()), Times.Exactly(3));

            cancel.Dispose();
            observer.Reset();
            service.Notify(new ClaraApplicationChangedEvent(new API.ClaraApplicationEntity(), ChangedEventType.Updated));
            observer.Verify(p => p.OnNext(It.IsAny<ClaraApplicationChangedEvent>()), Times.Never());
        }
    }
}