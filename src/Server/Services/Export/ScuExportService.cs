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

using Dicom.Network;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Export;
using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using DicomClient = Dicom.Network.Client.DicomClient;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Scu
{
    internal class ScuExportService : ExportServiceBase
    {
        private readonly ILogger<ScuExportService> _logger;
        private readonly ScuConfiguration _scuConfiguration;

        protected override string Agent { get; }
        protected override int Concurrentcy { get; }

        public ScuExportService(
            ILogger<ScuExportService> logger,
            IPayloads payloadsApi,
            IResultsService resultsService,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
            : base(logger, payloadsApi, resultsService, dicomAdapterConfiguration)
        {
            if (dicomAdapterConfiguration is null)
            {
                throw new ArgumentNullException(nameof(dicomAdapterConfiguration));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _scuConfiguration = dicomAdapterConfiguration.Value.Dicom.Scu;
            Agent = _scuConfiguration.AeTitle;
            Concurrentcy = _scuConfiguration.MaximumNumberOfAssociations;
        }

        protected override IEnumerable<OutputJob> ConvertDataBlockCallback(IList<TaskResponse> tasks, CancellationToken cancellationToken)
        {
            foreach (var task in tasks)
            {
                OutputJob outputJob;
                try
                {
                    outputJob = CreateOutputJobFromTask(task);
                }
                catch (Exception)
                {
                    ReportFailure(task, cancellationToken).Wait();
                    continue;
                }
                yield return outputJob;
            }
        }

        private OutputJob CreateOutputJobFromTask(TaskResponse task)
        {
            if (string.IsNullOrEmpty(task.Parameters))
                throw new ConfigurationException("Task Parameter is missing destination");

            var dest = JsonConvert.DeserializeObject<string>(task.Parameters);
            var destination = _scuConfiguration.Destinations
                .FirstOrDefault(p => p.Name.Equals(dest, StringComparison.InvariantCultureIgnoreCase));

            if (destination == null)
                throw new ConfigurationException($"Configured destination is invalid {dest}. Available destinations are: {string.Join(",", _scuConfiguration.Destinations.Select(p => p.Name).ToArray())}");

            return new OutputJob(task)
            {
                AeTitle = destination.AeTitle,
                HostIp = destination.HostIp,
                Port = destination.Port
            };
        }

        protected override async Task<OutputJob> ExportDataBlockCallback(OutputJob outputJob, CancellationToken cancellationToken)
        {
            using var loggerScope = _logger.BeginScope(new Dictionary<string, object> { { "JobId", outputJob.JobId }, { "PayloadId", outputJob.PayloadId } });

            if (outputJob.PendingDicomFiles.Count > 0)
            {
                var countDownEventHandle = new CountdownEvent(outputJob.PendingDicomFiles.Count);
                DicomClient client = null;
                try
                {
                    client = new DicomClient(
                        outputJob.HostIp,
                        outputJob.Port,
                        false,
                        _scuConfiguration.AeTitle,
                        outputJob.AeTitle);

                    client.AssociationAccepted += (sender, args) => _logger.LogInformation("Association accepted.");
                    client.AssociationRejected += (sender, args) => _logger.LogInformation("Association rejected.");
                    client.AssociationReleased += (sender, args) => _logger.LogInformation("Association release.");

                    client.Options = new DicomServiceOptions
                    {
                        LogDataPDUs = _scuConfiguration.LogDataPdus,
                        LogDimseDatasets = _scuConfiguration.LogDimseDatasets
                    };
                    client.NegotiateAsyncOps();
                    GenerateRequests(outputJob, client, countDownEventHandle);
                    _logger.LogInformation("Sending job to {0}@{1}:{2}", outputJob.AeTitle, outputJob.HostIp, outputJob.Port);
                    await client.SendAsync(cancellationToken).ConfigureAwait(false);
                    countDownEventHandle.Wait(cancellationToken);
                    _logger.LogInformation("Job sent to {0} completed", outputJob.AeTitle);
                }
                catch (Exception ex)
                {
                    HandleCStoreException(ex, outputJob, client);
                }
            }

            return outputJob;
        }

        private void GenerateRequests(
            OutputJob job,
            DicomClient client,
            CountdownEvent countDownEventHandle)
        {
            while (job.PendingDicomFiles.Count > 0)
            {
                try
                {
                    var request = new DicomCStoreRequest(job.PendingDicomFiles.Dequeue());

                    request.OnResponseReceived += (req, response) =>
                    {
                        if (response.Status != DicomStatus.Success)
                        {
                            job.FailureCount++;
                            _logger.Log(LogLevel.Error, $"Failed to export instance {request.File} with error {response.Status}");
                        }
                        else
                        {
                            job.SuccessfulExport++;
                            _logger.Log(LogLevel.Information, "Instance {0} sent successfully", request.File.FileMetaInfo.MediaStorageSOPInstanceUID.UID);
                        }
                        countDownEventHandle.Signal();
                    };

                    client.AddRequestAsync(request).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    _logger.LogError("Error while adding DICOM C-STORE request: {0}", exception);
                }
            }
        }

        private void HandleCStoreException(Exception ex, OutputJob job, DicomClient client)
        {
            var exception = ex;

            if (exception is AggregateException)
            {
                exception = exception.InnerException;
            }

            if (exception is DicomAssociationAbortedException abortEx)
            {
                _logger.LogError("Association aborted with reason {0}, exception {1}", abortEx.AbortReason, abortEx);
            }
            else if (exception is DicomAssociationRejectedException rejectEx)
            {
                _logger.LogError("Association rejected with reason {0}, exception {1}", rejectEx.RejectReason, rejectEx);
            }
            else if (exception is IOException && exception?.InnerException is SocketException socketException)
            {
                _logger.LogError("Association aborted with error {0}, exception {1}", socketException.Message, socketException);
            }
            else
            {
                _logger.LogError("Job failed with error {0}", exception);
            }
        }
    }
}