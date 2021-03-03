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
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Nvidia.Clara.ResultsService.Api;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Threading.Tasks.Dataflow;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Export
{
    internal abstract class ExportServiceBase : IHostedService, IClaraService
    {
        private readonly ILogger _logger;
        private readonly IPayloads _payloadsApi;
        private readonly IResultsService _resultsService;
        private readonly IStorageInfoProvider _storageInfoProvider;
        private readonly DataExportConfiguration _dataExportConfiguration;
        private System.Timers.Timer _workerTimer;

        internal event EventHandler ReportActionStarted;

        protected abstract string Agent { get; }
        protected abstract int Concurrentcy { get; }
        public ServiceStatus Status { get; set; } = ServiceStatus.Unknown;

        public ExportServiceBase(
            ILogger logger,
            IPayloads payloadsApi,
            IResultsService resultsService,
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration,
            IStorageInfoProvider storageInfoProvider)
        {
            if (dicomAdapterConfiguration is null)
            {
                throw new ArgumentNullException(nameof(dicomAdapterConfiguration));
            }

            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _payloadsApi = payloadsApi ?? throw new ArgumentNullException(nameof(payloadsApi));
            _resultsService = resultsService ?? throw new ArgumentNullException(nameof(resultsService));
            _storageInfoProvider = storageInfoProvider ?? throw new ArgumentNullException(nameof(storageInfoProvider));
            _dataExportConfiguration = dicomAdapterConfiguration.Value.Dicom.Scu.ExportSettings;
        }

        protected abstract Task<OutputJob> ExportDataBlockCallback(OutputJob outputJob, CancellationToken cancellationToken);

        protected abstract IEnumerable<OutputJob> ConvertDataBlockCallback(IList<TaskResponse> jobs, CancellationToken cancellationToken);

        public Task StartAsync(CancellationToken cancellationToken)
        {
            SetupPolling(cancellationToken);

            Status = ServiceStatus.Running;
            _logger.LogInformation("Export Task Watcher Hosted Service started.");
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _workerTimer?.Stop();
            _workerTimer = null;
            _logger.LogInformation("Export Task Watcher Hosted Service is stopping.");
            Status = ServiceStatus.Stopped;
            return Task.CompletedTask;
        }

        private void SetupPolling(CancellationToken cancellationToken)
        {
            _workerTimer = new System.Timers.Timer(_dataExportConfiguration.PollFrequencyMs);
            _workerTimer.Elapsed += (sender, e) =>
            {
                WorkerTimerElapsed(cancellationToken);
            };
            _workerTimer.AutoReset = false;
            _workerTimer.Start();
        }

        private void WorkerTimerElapsed(CancellationToken cancellationToken)
        {
            if(!_storageInfoProvider.HasSpaceAvailableForExport)
            {
                _logger.Log(LogLevel.Warning, $"Export service paused due to insufficient storage space.  Available storage space: {_storageInfoProvider.AvailableFreeSpace:D}.");
                return;
            }

            var downloadActionBlock = new TransformBlock<string, IList<TaskResponse>>(
                async (agent) => await DownloadActionCallback(agent, cancellationToken));

            var dataConvertTransformBlock = new TransformManyBlock<IList<TaskResponse>, OutputJob>(
                (tasks) =>
                {
                    if (tasks.IsNullOrEmpty())
                    {
                        return null;
                    }

                    return ConvertDataBlockCallback(tasks, cancellationToken);
                });

            var downloadPayloadTransformBlock = new TransformBlock<OutputJob, OutputJob>(
                async (outputJob) => await DownloadPayloadBlockCallback(outputJob, cancellationToken));

            var exportActionBlock = new TransformBlock<OutputJob, OutputJob>(
                async (outputJob) => await ExportDataBlockCallback(outputJob, cancellationToken),
                new ExecutionDataflowBlockOptions
                {
                    MaxDegreeOfParallelism = Concurrentcy,
                    MaxMessagesPerTask = 1,
                    CancellationToken = cancellationToken
                });

            var reportingActionBlock = new ActionBlock<OutputJob>(
                async (outputJob) => await ReportingActionBlock(outputJob, cancellationToken));

            var linkOptions = new DataflowLinkOptions { PropagateCompletion = true };
            downloadActionBlock.LinkTo(dataConvertTransformBlock, linkOptions);
            dataConvertTransformBlock.LinkTo(downloadPayloadTransformBlock, linkOptions);
            downloadPayloadTransformBlock.LinkTo(exportActionBlock, linkOptions);
            exportActionBlock.LinkTo(reportingActionBlock, linkOptions);

            try
            {
                downloadActionBlock.Post(Agent);
                downloadActionBlock.Complete();
                reportingActionBlock.Completion.Wait();
                _logger.Log(LogLevel.Debug, "Export Service completed timer routine.");
            }
            catch (AggregateException ex)
            {
                foreach (var iex in ex.InnerExceptions)
                {
                    _logger.Log(LogLevel.Error, iex, "Error occurred while exporting.");
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Error processing export task.");
            }
            finally
            {
                _workerTimer?.Start();
            }
        }

        private async Task ReportingActionBlock(OutputJob outputJob, CancellationToken cancellationToken)
        {
            if (ReportActionStarted != null)
            {
                ReportActionStarted(this, null);
            }

            if (outputJob is null)
            {
                return;
            }

            using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", outputJob.JobId }, { "PayloadId", outputJob.PayloadId } });
            await ReportStatus(outputJob, cancellationToken);
        }

        private async Task<OutputJob> DownloadPayloadBlockCallback(OutputJob outputJob, CancellationToken cancellationToken)
        {
            Guard.Against.Null(outputJob, nameof(outputJob));
            using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", outputJob.JobId }, { "PayloadId", outputJob.PayloadId } });
            foreach (var url in outputJob.Uris)
            {
                PayloadFile file;
                try
                {
                    file = await _payloadsApi.Download(outputJob.PayloadId, url);
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, ex, "Failed to download file {0}.", url);
                    outputJob.FailedFiles.Add(url);
                    outputJob.FailureCount++;
                    continue;
                }

                try
                {
                    var dicom = DicomFile.Open(new MemoryStream(file.Data));
                    outputJob.PendingDicomFiles.Enqueue(dicom);
                    outputJob.SuccessfulDownload++;
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Warning, ex, "Ignoring file; not a valid DICOM part-10 file {0}.", url);
                }
            }

            if (outputJob.DownloadFailureRate > _dataExportConfiguration.FailureThreshold)
            {
                _logger.Log(LogLevel.Error, "Failure rate exceeded threshold and will not be exported.");
                await ReportFailure(outputJob, cancellationToken);
                return null;
            }

            return outputJob;
        }

        private async Task<IList<TaskResponse>> DownloadActionCallback(string agent, CancellationToken cancellationToken)
        {
            return await _resultsService.GetPendingJobs(agent, cancellationToken, 10);
        }

        protected async Task ReportStatus(OutputJob outputJob, CancellationToken cancellationToken)
        {
            using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", outputJob.JobId }, { "PayloadId", outputJob.PayloadId } });
            if (outputJob is null)
            {
                return;
            }

            try
            {
                if (outputJob.ExportFailureRate > _dataExportConfiguration.FailureThreshold)
                {
                    var retry = outputJob.Retries < _dataExportConfiguration.MaximumRetries;
                    await _resultsService.ReportFailure(outputJob.TaskId, retry, cancellationToken);
                    _logger.Log(LogLevel.Warning,
                        $"Task marked as failed with failure rate={outputJob.ExportFailureRate}, total={outputJob.Uris.Count()}, failed={outputJob.FailureCount + outputJob.FailedFiles.Count}, processed={outputJob.SuccessfulExport}, retry={retry}");
                }
                else
                {
                    await _resultsService.ReportSuccess(outputJob.TaskId, cancellationToken);
                    _logger.LogInformation("Task marked as successful.");
                }
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, "Failed to report status back to Results Service.");
            }
        }

        protected async Task ReportFailure(TaskResponse task, CancellationToken cancellationToken)
        {
            using var loggerScope = _logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", task.JobId }, { "PayloadId", task.PayloadId } });
            try
            {
                await _resultsService.ReportFailure(task.TaskId, false, cancellationToken);
                _logger.Log(LogLevel.Warning, $"Task {task.TaskId} marked as failure and will not be retried.");
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, ex, "Failed to mark task {0} as failure.", task.TaskId);
            }
        }
    }
}