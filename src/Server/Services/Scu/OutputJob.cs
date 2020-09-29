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
using Dicom;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.ResultsService.Api;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Scu
{
    internal class OutputJob : TaskResponse
    {
        private const float FailureThreshold = 0.5f;
        private readonly IResultsService resultsService;

        public const int MaxRetry = 3;
        public string HostIp { get; set; }
        public int Port { get; set; }
        public string AeTitle { get; set; }
        public Dictionary<DicomFile, string> FailedDicomFiles { get; }
        public List<string> FailedFiles { get; }
        public List<DicomFile> ProcessedDicomFiles { get; }
        public Queue<DicomFile> PendingDicomFiles { get; }
        public ILogger<ScuService> Logger { get; private set; }

        public OutputJob(TaskResponse task, ILogger<ScuService> logger, IResultsService iResultsService, DestinationApplicationEntity destination)
        {
            CopyBaseProperties(task);
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            resultsService = iResultsService ?? throw new ArgumentNullException(nameof(iResultsService));

            AeTitle = destination.AeTitle;
            HostIp = destination.HostIp;
            Port = destination.Port;

            FailedDicomFiles = new Dictionary<DicomFile, string>();
            FailedFiles = new List<string>();
            ProcessedDicomFiles = new List<DicomFile>();
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

        public void ReportStatus(CancellationToken cancellationToken)
        {
            try
            {
                var failureRate = (Uris.Count() - ProcessedDicomFiles.Count) / Uris.Count();
                if (failureRate > FailureThreshold)
                {
                    var retry = Retries < MaxRetry;
                    resultsService.ReportFailure(TaskId, retry, cancellationToken);
                    Logger.LogInformation(
                        "Task marked as failed with failure rate={0}, total={1}, failed={2}, processed={3}, retry={4}",
                        failureRate,
                        Uris.Count(),
                        FailedDicomFiles.Count + FailedFiles.Count,
                        ProcessedDicomFiles.Count,
                        retry);
                }
                else
                {
                    resultsService.ReportSuccess(TaskId, cancellationToken);
                    Logger.LogInformation("Task marked as successful");
                }
            }
            catch (System.Exception ex)
            {
                Logger.LogError("Failed to report status back to Results Service: {0}", ex);
            }
        }

        public void LogFailedRequests()
        {
            foreach (var file in FailedFiles)
            {
                Logger.LogError("File {0} failed to download, corrupted or invalid", file);
            }
            foreach (var file in FailedDicomFiles.Keys)
            {
                Logger.LogError("C-STORE failed on {0} with response status {1}",
                    file.FileMetaInfo.MediaStorageSOPInstanceUID,
                    FailedDicomFiles[file]);
            }
        }
    }
}
