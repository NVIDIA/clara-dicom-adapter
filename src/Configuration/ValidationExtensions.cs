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
using Nvidia.Clara.DicomAdapter.API;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nvidia.Clara.DicomAdapter.Configuration
{
    public static class ValidationExtensions
    {
        public static bool IsValid(this ClaraApplicationEntity claraApplicationEntity, IEnumerable<string> existingAeTitles, out IList<string> validationErrors)
        {
            Guard.Against.Null(existingAeTitles, nameof(existingAeTitles));
            Guard.Against.Null(claraApplicationEntity, nameof(claraApplicationEntity));

            validationErrors = new List<string>();

            var valid = true;
            valid &= IsAeTitleValid(claraApplicationEntity.GetType().Name, claraApplicationEntity.AeTitle, validationErrors);

            if (existingAeTitles.Any(p => p.Equals(claraApplicationEntity.AeTitle, StringComparison.Ordinal)))
            {
                validationErrors.Add($"Clara AE Title {claraApplicationEntity.AeTitle} already exists.");
                valid = false;
            }

            return valid;
        }

        public static bool IsValid(this DestinationApplicationEntity destinationApplicationEntity, IEnumerable<string> existingDestinationNames, out IList<string> validationErrors)
        {
            Guard.Against.Null(destinationApplicationEntity, nameof(destinationApplicationEntity));

            validationErrors = new List<string>();

            var valid = true;
            valid &= !string.IsNullOrWhiteSpace(destinationApplicationEntity.Name);
            valid &= IsAeTitleValid(destinationApplicationEntity.GetType().Name, destinationApplicationEntity.AeTitle, validationErrors);
            valid &= IsValidHostNameIp(destinationApplicationEntity.AeTitle, destinationApplicationEntity.HostIp, validationErrors);
            valid &= IsPortValid(destinationApplicationEntity.GetType().Name, destinationApplicationEntity.Port, validationErrors);

            if (existingDestinationNames.Any(p => p.Equals(destinationApplicationEntity.Name, StringComparison.Ordinal)))
            {
                validationErrors.Add($"Destination with name {destinationApplicationEntity.Name} already exists.");
                valid = false;
            }

            return valid;
        }

        public static bool IsValid(this SourceApplicationEntity sourceApplicationEntity, IEnumerable<string> existingAeTitles, out IList<string> validationErrors)
        {
            Guard.Against.Null(existingAeTitles, nameof(existingAeTitles));
            Guard.Against.Null(sourceApplicationEntity, nameof(sourceApplicationEntity));

            validationErrors = new List<string>();

            var valid = true;
            valid &= IsAeTitleValid(sourceApplicationEntity.GetType().Name, sourceApplicationEntity.AeTitle, validationErrors);
            valid &= IsValidHostNameIp(sourceApplicationEntity.AeTitle, sourceApplicationEntity.HostIp, validationErrors);

            if (existingAeTitles.Any(p => p.Equals(sourceApplicationEntity.AeTitle, StringComparison.Ordinal)))
            {
                validationErrors.Add($"Source with AE Title {sourceApplicationEntity.AeTitle} already exists.");
                valid = false;
            }
            return valid;
        }

        public static bool IsAeTitleValid(string source, string aeTitle, IList<string> validationErrors = null)
        {
            if (!string.IsNullOrWhiteSpace(aeTitle) && aeTitle.Length <= 15) return true;

            validationErrors?.Add($"'{aeTitle}' is not a valid AE Title (source: {source}).");
            return false;
        }

        public static bool IsValidHostNameIp(string source, string hostIp, IList<string> validationErrors = null)
        {
            if (!string.IsNullOrWhiteSpace(hostIp)) return true;

            validationErrors?.Add($"Invalid host name/IP address '{hostIp}' specified for {source}.");
            return false;
        }

        public static bool IsPortValid(string source, int port, IList<string> validationErrors = null)
        {
            if (port > 0 && port <= 65535) return true;

            validationErrors?.Add($"Invalid port number '{port}' specified for {source}.");
            return false;
        }
    }
}