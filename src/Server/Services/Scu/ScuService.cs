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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;
using Dicom;
using Dicom.Network;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Services.Scu;
using DicomClient = Dicom.Network.Client.DicomClient;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Scu
{
    internal class ScuService : IHostedService, IDisposable
    {
        private ExportTaskWatcher _watcher;
        private ActionBlock<OutputJob> _outputJobQueue;
        private readonly ILogger<ScuService> _logger;
        private readonly IPayloads _payloadsApi;
        private readonly IHostApplicationLifetime _appLifetime;
        private readonly IOptions<DicomAdapterConfiguration> _dicomAdapterConfiguration;
        private CancellationToken _token;

        public ScuService(
            ILogger<ScuService> logger,
            IPayloads iPayloads,
            IResultsService resultsService,
            IHostApplicationLifetime appLifetime,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _payloadsApi = iPayloads ?? throw new ArgumentNullException(nameof(iPayloads));
            _appLifetime = appLifetime ?? throw new ArgumentNullException(nameof(appLifetime));
            _dicomAdapterConfiguration = dicomAdapterConfiguration ?? throw new ArgumentNullException(nameof(dicomAdapterConfiguration));
            _watcher = new ExportTaskWatcher(logger, resultsService, dicomAdapterConfiguration.Value.Dicom.Scu);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _appLifetime.ApplicationStarted.Register(OnStarted);
            _appLifetime.ApplicationStopping.Register(OnStopping);
            _appLifetime.ApplicationStopped.Register(OnStopped);

            Task.Run(() =>
            {
                _logger.LogInformation("Clara DICOM Adapter (SCU Service) {Version} loading...",
                Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyFileVersionAttribute>().Version);
                _token = cancellationToken;
                _outputJobQueue = new ActionBlock<OutputJob>(
                    SendCStoreRequest,
                    new ExecutionDataflowBlockOptions
                    {
                        MaxDegreeOfParallelism = _dicomAdapterConfiguration.Value.Dicom.Scu.MaximumNumberOfAssociations,
                        MaxMessagesPerTask = 1,
                        CancellationToken = _token
                    });
                _watcher.Start(_outputJobQueue, _token);
                _logger.LogInformation("SCU service started.");
            });
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _logger.LogInformation($"Stopping job watcher with {_outputJobQueue.InputCount} jobs in the queue.");
            _watcher.Stop();
            _outputJobQueue.Complete();
        }

        private void OnStarted()
        {
        }

        private void OnStopping()
        {
        }

        private void OnStopped()
        {
        }

        private async Task SendCStoreRequest(OutputJob job)
        {
            await DownloadFromPayloadsService(job);
            if (job.PendingDicomFiles.Count > 0)
            {
                var countDownEventHandle = new CountdownEvent(job.PendingDicomFiles.Count);
                DicomClient client = null;
                try
                {
                    client = new DicomClient(
                        job.HostIp,
                        job.Port,
                        false,
                        _dicomAdapterConfiguration.Value.Dicom.Scu.AeTitle,
                        job.AeTitle);
                    client.AssociationAccepted += (sender, args) =>
                       job.Logger.LogInformation("Association accepted.");
                    client.AssociationRejected += (sender, args) =>
                       job.Logger.LogInformation("Association rejected.");
                    client.AssociationReleased += (sender, args) =>
                       job.Logger.LogInformation("Association release.");

                    client.Options = new DicomServiceOptions
                    {
                        LogDataPDUs = _dicomAdapterConfiguration.Value.Dicom.Scu.LogDataPdus,
                        LogDimseDatasets = _dicomAdapterConfiguration.Value.Dicom.Scu.LogDimseDatasets
                    };
                    client.NegotiateAsyncOps();
                    GenerateRequests(job, client, countDownEventHandle);
                    job.Logger.LogInformation("Sending job to {0}@{1}:{2}", job.AeTitle, job.HostIp, job.Port);
                    await client.SendAsync(_token).ConfigureAwait(false);
                    countDownEventHandle.Wait(_token);
                    job.Logger.LogInformation("Job sent to {0} completed", job.AeTitle);
                }
                catch (Exception ex)
                {
                    HandleCStoreException(ex, job, client);
                }
            }
            job.LogFailedRequests();
            job.ReportStatus(_token);
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
                            job.FailedDicomFiles.Add(request.File, response.Status.ToString());
                        }
                        else
                        {
                            job.ProcessedDicomFiles.Add(request.File);
                            job.Logger.LogInformation("Instance {0} sent successfully", request.File.FileMetaInfo.MediaStorageSOPInstanceUID.UID);
                        }
                        countDownEventHandle.Signal();
                    };

                    client.AddRequestAsync(request).ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    job.Logger.LogError("Error while adding DICOM C-STORE request: {0}", exception);
                }
            }
        }

        private async Task DownloadFromPayloadsService(OutputJob job)
        {
            int failureCount = 0;
            foreach (var url in job.Uris)
            {
                PayloadFile file = null;
                try
                {
                    file = await _payloadsApi.Download(job.PayloadId, url);
                }
                catch (System.Exception ex)
                {
                    job.Logger.LogWarning("Failed to download file {0} from payload {1}: {2}", url, job.PayloadId, ex);
                    job.FailedFiles.Add(url);
                    failureCount++;
                    continue;
                }

                try
                {
                    var dicom = DicomFile.Open(new MemoryStream(file.Data));
                    job.PendingDicomFiles.Enqueue(dicom);
                }
                catch (System.Exception ex)
                {
                    job.Logger.LogWarning("Failed to load DICOM data {0} from payload {1}: {2}", url, job.PayloadId, ex);
                    job.FailedFiles.Add(url);
                    failureCount++;
                }
            }

            if (failureCount > 1)
            {
                job.Logger.LogWarning("{0}/{1} files failed to load.", failureCount, job.Uris.Count());
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
                job.Logger.LogError("Association aborted with reason {0}, exception {1}", abortEx.AbortReason, abortEx);
            }
            else if (exception is DicomAssociationRejectedException rejectEx)
            {
                job.Logger.LogError("Association rejected with reason {0}, exception {1}", rejectEx.RejectReason, rejectEx);
            }
            else if (exception is IOException && exception?.InnerException is System.Net.Sockets.SocketException socketException)
            {
                job.Logger.LogError("Association aborted with error {0}, exception {1}", socketException.Message, socketException);
            }
            else
            {
                job.Logger.LogError("Job failed with error {0}", exception);
            }
        }
    }
}
