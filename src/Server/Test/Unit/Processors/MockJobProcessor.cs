﻿/*
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
using System.Threading;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Processors;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.DicomAdapter.Server.Services.Scp;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class MockJobProcessor : JobProcessorBase
    {
        private readonly ClaraApplicationEntity _configuration;

        public MockJobProcessor(
            ClaraApplicationEntity configuration,
            IInstanceStoredNotificationService instanceStoredNotificationService,
            ILoggerFactory loggerFactory,
            IJobs jobsApi,
            IPayloads payloadsApi,
            IInstanceCleanupQueue cleanupQueue,
            CancellationToken cancellationToken) : base(instanceStoredNotificationService, loggerFactory, jobsApi, payloadsApi, cleanupQueue, cancellationToken)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public override string Name => "Mock";

        public override string AeTitle => "AET";

        public override void HandleInstance(InstanceStorageInfo value)
        {
            //noop
        }
    }
}
