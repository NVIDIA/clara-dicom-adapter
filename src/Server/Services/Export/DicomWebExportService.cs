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
using Dicom;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.Dicom.DicomWeb.Client;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Export
{
    internal class DicomWebExportService : ExportServiceBase
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IInferenceRequestStore _inferenceRequestStore;
        private readonly ILogger<DicomWebExportService> _logger;
        private readonly DataExportConfiguration _dataExportConfiguration;

        protected override string Agent { get; }
        protected override int Concurrentcy { get; }

        public DicomWebExportService(
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
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

            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
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
            if (inferenceRequest is null)
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
                var dicomWebClient = new DicomWebClient(_httpClientFactory.CreateClient("dicomweb"), _loggerFactory.CreateLogger<DicomWebClient>());
                dicomWebClient.ConfigureServiceUris(new Uri(destination.ConnectionDetails.Uri, UriKind.Absolute));
                dicomWebClient.ConfigureAuthentication(authenticationHeader);

                _logger.Log(LogLevel.Debug, $"Exporting data to {destination.ConnectionDetails.Uri}.");
                await ExportToDicomWebDestination(dicomWebClient, outputJob, destination, cancellationToken);
            }

            return outputJob;
        }

        private async Task ExportToDicomWebDestination(IDicomWebClient dicomWebClient, OutputJob outputJob, API.Rest.RequestOutputDataResource destination, CancellationToken cancellationToken)
        {
            while (outputJob.PendingDicomFiles.Count > 0)
            {
                var files = new List<DicomFile>();
                try
                {
                    var counter = 10;
                    while (counter-- > 0 && outputJob.PendingDicomFiles.Count > 0)
                    {
                        files.Add(outputJob.PendingDicomFiles.Dequeue());
                    }
                    var result = await dicomWebClient.Stow.Store(files, cancellationToken);
                    CheckAndLogResult(result);
                    outputJob.SuccessfulExport += files.Count;
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, "Failed to export data to DICOMweb destination.");
                    outputJob.FailureCount += files.Count;
                }
                finally
                {
                    files.Clear();
                }

            }
        }

        private void CheckAndLogResult(DicomWebResponse<string> result)
        {
            Guard.Against.Null(result, nameof(result));
            switch (result.StatusCode)
            {
                case System.Net.HttpStatusCode.OK:
                    _logger.Log(LogLevel.Information, "All data exported successfully.");
                    break;
                default:
                    throw new Exception("One or more instances failed to be stored by destination, will retry later.");
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