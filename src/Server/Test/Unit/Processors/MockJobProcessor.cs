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

using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using System;
using System.Collections.Generic;
using System.Threading;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    [ProcessorValidation(ValidatorType = typeof(MockJobProcessorValidator))]
    internal class MockJobProcessor : JobProcessorBase
    {
        private readonly ClaraApplicationEntity _configuration;

        public MockJobProcessor(
            ClaraApplicationEntity configuration,
            IInstanceStoredNotificationService instanceStoredNotificationService,
            ILoggerFactory loggerFactory,
            IJobRepository jobStore,
            IInstanceCleanupQueue cleanupQueue,
            CancellationToken cancellationToken) : base(instanceStoredNotificationService, loggerFactory, jobStore, cleanupQueue, cancellationToken)
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

    internal class MockJobProcessorValidator : IJobProcessorValidator
    {
        public void Validate(string aeTitle, Dictionary<string, string> processorSettings)
        {
            // noop
        }
    }
}