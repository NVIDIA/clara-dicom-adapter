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

using System;
using System.Collections.Generic;

namespace Nvidia.Clara.DicomAdapter.API.Rest
{
    /// <summary>
    /// Defines the state of a running DICOM Adapter service.
    /// </summary>
    public enum ServiceStatus
    {
        /// <summary>
        /// Unknown - default, during start up.
        /// </summary>
        Unknown,

        /// <summary>
        /// Service is stopped.
        /// </summary>
        Stopped,

        /// <summary>
        /// Service is running.
        /// </summary>
        Running,

        /// <summary>
        /// Service has been cancelled by a cancellation token.
        /// </summary>
        Cancelled
    }


    /// <summary>
    /// Response message of a successful inference request.
    /// </summary>
    public class HealthStatusResponse
    {
        /// <summary>
        /// Gets or sets the number of active DIMSE connetions.
        /// </summary>
        public int ActiveDimseConnections { get; set; }

        /// <summary>
        /// Gets or sets status of DICOM Adapter services.
        /// </summary>
        public Dictionary<string, ServiceStatus> Services { get; set; } = new Dictionary<string, ServiceStatus>();
    }
}