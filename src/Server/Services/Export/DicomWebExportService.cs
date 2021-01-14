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
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Export
{
    internal class DicomWebExportService : ExportServiceBase
    {
        private readonly IDicomWebClient _dicomWebClient;
        private readonly IInferenceRequestStore _inferenceRequestStore;
        private readonly ILogger<DicomWebExportService> _logger;
        private readonly DataExportConfiguration _dataExportConfiguration;

        protected override string Agent { get; }
        protected override int Concurrentcy { get; }

        public DicomWebExportService(
            IDicomWebClient dicomWebClient,
            IInferenceRequestStore inferenceRequestStore,
            ILogger<DicomWebExportService> logger,
            IPayloads payloadsApi,
            IResultsService resultsService,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
            : base(logger, payloadsApi, resultsService, dicomAdapterConfiguration)
        {
            if (dicomAdapterConfiguration is null)
            {
                throw new ArgumentNullException(nameof(dicomAdapterConfiguration));
            }

            _dicomWebClient = dicomWebClient ?? throw new ArgumentNullException(nameof(dicomWebClient));
            _inferenceRequestStore = inferenceRequestStore ?? throw new ArgumentNullException(nameof(inferenceRequestStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dataExportConfiguration = dicomAdapterConfiguration.Value.Dicom.Scu.ExportSettings;

            Agent = _dataExportConfiguration.Agent;
            Concurrentcy = dicomAdapterConfiguration.Value.Dicom.Scu.MaximumNumberOfAssociations;
        }

        protected override async Task<OutputJob> ExportDataBlockCallback(OutputJob outputJob, CancellationToken cancellationToken)
        {
            using var loggerScope = _logger.BeginScope(new Dictionary<string, object> { { "TaskId", outputJob.TaskId }, { "JobId", outputJob.JobId }, { "PayloadId", outputJob.PayloadId } });
            var inferenceRequest = await _inferenceRequestStore.Get(outputJob.JobId, outputJob.PayloadId);
            if (inferenceRequest == null)
            {
                _logger.Log(LogLevel.Error, "The specified job cannot be found in the inference request store and will not be exported.");
                await ReportFailure(outputJob, cancellationToken);
                return null;
            }

            var destinations = inferenceRequest.OutputResources.Where(p => p.Interface == API.Rest.InputInterfaceType.DicomWeb);

            if (destinations.Count() == 0)
            {
                _logger.Log(LogLevel.Error, "The inference request contains no `outputResources` nor any DICOMweb export destinations.");
                await ReportFailure(outputJob, cancellationToken);
                return null;
            }

            foreach (var destination in destinations)
            {
                var authenticationHeader = AuthenticationHeaderValueExtensions.ConvertFrom(destination.ConnectionDetails.AuthType, destination.ConnectionDetails.AuthId);
                _dicomWebClient.ConfigureAuthentication(authenticationHeader);
                await ExportToDicomWebDestination(outputJob, destination, cancellationToken);
            }

            return outputJob;
        }

        private async Task ExportToDicomWebDestination(OutputJob outputJob, API.Rest.RequestOutputDataResource destination, CancellationToken cancellationToken)
        {
            while (outputJob.PendingDicomFiles.Count > 0)
            {
                var files = new List<DicomFile>();
                try
                {
                    var counter = 10;
                    while (counter-- > 0)
                    {
                        files.Add(outputJob.PendingDicomFiles.Dequeue());
                    }
                    await _dicomWebClient.Stow.Store(files, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, "Failed to export data to DICOMweb destination.");
                    outputJob.FailureCount += files.Count;
                    files.Clear();
                }
            }
        }

        protected override IEnumerable<OutputJob> ConvertDataBlockCallback(IList<TaskResponse> tasks, CancellationToken cancellationToken)
        {
            foreach (var task in tasks)
            {
                yield return new OutputJob(task);
            }
        }
    }
}