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

using Dicom;
using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Export
{
    internal class OutputJob : TaskResponse
    {
        public string HostIp { get; set; }
        public int Port { get; set; }
        public string AeTitle { get; set; }
        public List<string> FailedFiles { get; }
        public int SuccessfulExport { get; set; }
        public int SuccessfulDownload { get; set; }
        public int FailureCount { get; set; }
        public Queue<DicomFile> PendingDicomFiles { get; }

        public float ExportFailureRate
        {
            get
            {
                return (Uris.Count() - SuccessfulExport) / (float)Uris.Count();
            }
        }

        public float DownloadFailureRate
        {
            get
            {
                return (Uris.Count() - SuccessfulDownload) / (float)Uris.Count();
            }
        }

        public OutputJob()
        {
        }

        public OutputJob(TaskResponse task)
        {
            if (task is null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            CopyBaseProperties(task);

            FailedFiles = new List<string>();
            SuccessfulExport = 0;
            FailureCount = 0;
            PendingDicomFiles = new Queue<DicomFile>();
        }

        private void CopyBaseProperties(TaskResponse task)
        {
            var properties = task.GetType().GetProperties();

            properties.ToList().ForEach(property =>
            {
                var isPresent = GetType().GetProperty(property.Name);
                if (isPresent != null)
                {
                    //If present get the value and map it
                    var value = task.GetType().GetProperty(property.Name).GetValue(task, null);
                    GetType().GetProperty(property.Name).SetValue(this, value, null);
                }
            });
        }
    }
}