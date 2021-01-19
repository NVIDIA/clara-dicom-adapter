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

using Microsoft.Extensions.Hosting;
using Moq;
using System.Threading;
using Xunit;
using xRetry;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class HostedServiceControllerTest
    {
        private Mock<IHostedService> hostedService;
        private CancellationTokenSource cancellationTokenSource;

        public HostedServiceControllerTest()
        {
            hostedService = new Mock<IHostedService>();
            cancellationTokenSource = new CancellationTokenSource();
        }

        [RetryFact(DisplayName = "StartAsync")]
        public void StartAsync()
        {
            hostedService.Setup(p => p.StartAsync(It.IsAny<CancellationToken>()));
            var controller = new HostedServiceController<IHostedService>(hostedService.Object);
            controller.StartAsync(cancellationTokenSource.Token);

            hostedService.Verify(p => p.StartAsync(It.IsAny<CancellationToken>()), Times.Once());
        }

        [RetryFact(DisplayName = "StopAsync")]
        public void StopAsync()
        {
            hostedService.Setup(p => p.StopAsync(It.IsAny<CancellationToken>()));
            var controller = new HostedServiceController<IHostedService>(hostedService.Object);
            controller.StopAsync(cancellationTokenSource.Token);

            hostedService.Verify(p => p.StopAsync(It.IsAny<CancellationToken>()), Times.Once());
        }
    }
}