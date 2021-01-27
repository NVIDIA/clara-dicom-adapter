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
using Nvidia.Clara.Dicom.DicomWeb.Client;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Polly;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Jobs
{
    public class DataRetrievalService : IHostedService, IClaraService
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IInferenceRequestStore _inferenceRequestStore;
        private readonly ILogger<DataRetrievalService> _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IDicomToolkit _dicomToolkit;
        private readonly IJobStore _jobStore;

        public ServiceStatus Status { get; set; }

        public DataRetrievalService(
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
            ILogger<DataRetrievalService> logger,
            IInferenceRequestStore inferenceRequestStore,
            IFileSystem fileSystem,
            IDicomToolkit dicomToolkit,
            IJobStore jobStore)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _inferenceRequestStore = inferenceRequestStore ?? throw new ArgumentNullException(nameof(inferenceRequestStore));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _dicomToolkit = dicomToolkit ?? throw new ArgumentNullException(nameof(dicomToolkit));
            _jobStore = jobStore ?? throw new ArgumentNullException(nameof(jobStore));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            var task = Task.Run(async () =>
            {
                await BackgroundProcessing(cancellationToken);
            });

            Status = ServiceStatus.Running;
            if (task.IsCompleted)
                return task;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Data Retriever Hosted Service is stopping.");
            Status = ServiceStatus.Stopped;
            return Task.CompletedTask;
        }

        private async Task BackgroundProcessing(CancellationToken cancellationToken)
        {
            _logger.Log(LogLevel.Information, "Data Retriever Hosted Service is running.");

            while (!cancellationToken.IsCancellationRequested)
            {
                InferenceRequest request = null;
                try
                {
                    request = await _inferenceRequestStore.Take(cancellationToken);
                    using (_logger.BeginScope(new Dictionary<string, object> { { "JobId", request.JobId }, { "TransactionId", request.TransactionId } }))
                    {
                        _logger.Log(LogLevel.Information, "Processing inference request.");
                        await ProcessRequest(request, cancellationToken);
                        await _inferenceRequestStore.Update(request, InferenceRequestStatus.Success);
                        _logger.Log(LogLevel.Information, "Inference request completed and ready for job submission.");
                    }
                }
                catch (OperationCanceledException ex)
                {
                    _logger.Log(LogLevel.Warning, ex, "Data Retriever Service canceled.");
                }
                catch (InvalidOperationException ex)
                {
                    _logger.Log(LogLevel.Warning, ex, "Data Retriever Service may be disposed.");
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, $"Error processing request: JobId = {request?.JobId},  TransactionId = {request?.TransactionId}");
                    if (request != null)
                    {
                        await _inferenceRequestStore.Update(request, InferenceRequestStatus.Fail);
                    }
                }

            }
            Status = ServiceStatus.Cancelled;
            _logger.Log(LogLevel.Information, "Cancellation requested.");
        }

        private async Task ProcessRequest(InferenceRequest inferenceRequest, CancellationToken cancellationToken)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            var retrievedInstances = new Dictionary<string, InstanceStorageInfo>();
            RestoreExistingInstances(inferenceRequest, retrievedInstances);

            foreach (var source in inferenceRequest.InputResources)
            {
                switch (source.Interface)
                {
                    case InputInterfaceType.DicomWeb:
                        await RetrieveViaDicomWeb(inferenceRequest, source, retrievedInstances);
                        break;

                    case InputInterfaceType.Algorithm:
                        continue;
                    default:
                        _logger.Log(LogLevel.Warning, $"Specified input interface is not supported '{source.Interface}`");
                        break;
                }
            }

            if (retrievedInstances.Count == 0)
            {
                throw new InferenceRequestException("No DICOM instance found/retrieved with the request.");
            }

            await SubmitPipelineJob(inferenceRequest, retrievedInstances.Select(p => p.Value), cancellationToken);
        }

        private async Task SubmitPipelineJob(InferenceRequest inferenceRequest, IEnumerable<InstanceStorageInfo> instances, CancellationToken cancellationToken)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            if (instances.IsNullOrEmpty())
            {
                throw new ArgumentNullException("no instances found.", nameof(instances));
            }

            _logger.Log(LogLevel.Information, $"Queuing a new job '{inferenceRequest.JobName}' with pipeline '{inferenceRequest.Algorithm.PipelineId}', priority={inferenceRequest.ClaraJobPriority}, instance count={instances.Count()}");
            await _jobStore.Add(
                new Job
                {
                    JobId = inferenceRequest.JobId,
                    PayloadId = inferenceRequest.PayloadId
                }, inferenceRequest.JobName, instances.ToList());
        }

        #region Data Retrieval

        private void RestoreExistingInstances(InferenceRequest inferenceRequest, Dictionary<string, InstanceStorageInfo> retrievedInstances)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));
            Guard.Against.Null(retrievedInstances, nameof(retrievedInstances));

            _logger.Log(LogLevel.Debug, $"Restoring previously retrieved DICOM instances from {inferenceRequest.StoragePath}");
            foreach (var file in _fileSystem.Directory.EnumerateFiles(inferenceRequest.StoragePath, "*.dcm", System.IO.SearchOption.AllDirectories))
            {
                if (_dicomToolkit.HasValidHeader(file))
                {
                    var dicomFile = _dicomToolkit.Open(file);
                    var instance = InstanceStorageInfo.CreateInstanceStorageInfo(dicomFile, inferenceRequest.StoragePath, _fileSystem);
                    if (retrievedInstances.ContainsKey(instance.SopInstanceUid))
                    {
                        continue;
                    }
                    retrievedInstances.Add(instance.SopInstanceUid, instance);
                    _logger.Log(LogLevel.Debug, $"Restored previously retrieved instance {instance.SopInstanceUid}");
                }
                else
                {
                    _logger.Log(LogLevel.Warning, $"Unable to restore previously retrieved instance from {file}; file does not contain valid DICOM header.");
                }
            }
        }

        private async Task RetrieveViaDicomWeb(InferenceRequest inferenceRequest, RequestInputDataResource source, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));
            Guard.Against.Null(retrievedInstance, nameof(retrievedInstance));

            var authenticationHeaderValue = AuthenticationHeaderValueExtensions.ConvertFrom(source.ConnectionDetails.AuthType, source.ConnectionDetails.AuthId);

            var dicomWebClient = new DicomWebClient(_httpClientFactory.CreateClient("dicomweb"), _loggerFactory.CreateLogger<DicomWebClient>());
            dicomWebClient.ConfigureServiceUris(new Uri(source.ConnectionDetails.Uri, UriKind.Absolute));
            dicomWebClient.ConfigureAuthentication(authenticationHeaderValue);
            switch (inferenceRequest.InputMetadata.Details.Type)
            {
                case InferenceRequestType.DicomUid:
                    await RetrieveStudies(dicomWebClient, inferenceRequest.InputMetadata.Details.Studies, inferenceRequest.StoragePath, retrievedInstance);
                    break;

                case InferenceRequestType.DicomPatientId:
                    await QueryStudies(dicomWebClient, inferenceRequest, retrievedInstance, $"{DicomTag.PatientID.Group:X4}{DicomTag.PatientID.Element:X4}", inferenceRequest.InputMetadata.Details.PatientId);
                    break;

                case InferenceRequestType.AccessionNumber:
                    foreach (var accessionNumber in inferenceRequest.InputMetadata.Details.AccessionNumber)
                    {
                        await QueryStudies(dicomWebClient, inferenceRequest, retrievedInstance, $"{DicomTag.AccessionNumber.Group:X4}{DicomTag.AccessionNumber.Element:X4}", accessionNumber);
                    }
                    break;

                default:
                    throw new InferenceRequestException($"The 'inputMetadata' type '{inferenceRequest.InputMetadata.Details.Type}' specified is not supported.");
            }
        }

        private async Task QueryStudies(DicomWebClient dicomWebClient, InferenceRequest inferenceRequest, Dictionary<string, InstanceStorageInfo> retrievedInstance, string dicomTag, string queryValue)
        {
            Guard.Against.Null(dicomWebClient, nameof(dicomWebClient));
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));
            Guard.Against.Null(retrievedInstance, nameof(retrievedInstance));
            Guard.Against.NullOrWhiteSpace(dicomTag, nameof(dicomTag));
            Guard.Against.NullOrWhiteSpace(queryValue, nameof(queryValue));

            _logger.Log(LogLevel.Information, $"Performing QIDO with {dicomTag}={queryValue}.");
            var queryParams = new Dictionary<string, string>();
            queryParams.Add(dicomTag, queryValue);

            var studies = new List<RequestedStudy>();
            await foreach (var result in dicomWebClient.Qido.SearchForStudies<DicomDataset>(queryParams))
            {
                if (result.Contains(DicomTag.StudyInstanceUID))
                {
                    var studyInstanceUid = result.GetString(DicomTag.StudyInstanceUID);
                    studies.Add(new RequestedStudy
                    {
                        StudyInstanceUid = studyInstanceUid
                    });
                    _logger.Log(LogLevel.Debug, $"Study {studyInstanceUid} found with QIDO query {dicomTag}={queryValue}.");
                }
                else
                {
                    _logger.Log(LogLevel.Warning, $"Instance {result.GetSingleValueOrDefault(DicomTag.SOPInstanceUID, "UKNOWN")} does not contain StudyInstanceUid.");
                }
            }

            if (studies.Count != 0)
            {
                await RetrieveStudies(dicomWebClient, studies, inferenceRequest.StoragePath, retrievedInstance);
            }
            else
            {
                _logger.Log(LogLevel.Warning, $"No studies found with specified query parameter {dicomTag}={queryValue}.");

            }
        }

        private async Task RetrieveStudies(IDicomWebClient dicomWebClient, IList<RequestedStudy> studies, string storagePath, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
            Guard.Against.Null(studies, nameof(studies));
            Guard.Against.Null(storagePath, nameof(storagePath));
            Guard.Against.Null(retrievedInstance, nameof(retrievedInstance));

            foreach (var study in studies)
            {
                if (study.Series.IsNullOrEmpty())
                {
                    _logger.Log(LogLevel.Information, $"Retrieving study {study.StudyInstanceUid}");
                    var files = dicomWebClient.Wado.Retrieve(study.StudyInstanceUid);
                    await SaveFiles(files, storagePath, retrievedInstance);
                }
                else
                {
                    await RetrieveSeries(dicomWebClient, study, storagePath, retrievedInstance);
                }
            }
        }

        private async Task RetrieveSeries(IDicomWebClient dicomWebClient, RequestedStudy study, string storagePath, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
            Guard.Against.Null(study, nameof(study));
            Guard.Against.Null(storagePath, nameof(storagePath));
            Guard.Against.Null(retrievedInstance, nameof(retrievedInstance));

            foreach (var series in study.Series)
            {
                if (series.Instances.IsNullOrEmpty())
                {
                    _logger.Log(LogLevel.Information, $"Retrieving series {series.SeriesInstanceUid}");
                    var files = dicomWebClient.Wado.Retrieve(study.StudyInstanceUid, series.SeriesInstanceUid);
                    await SaveFiles(files, storagePath, retrievedInstance);
                }
                else
                {
                    await RetrieveInstances(dicomWebClient, study.StudyInstanceUid, series, storagePath, retrievedInstance);
                }
            }
        }

        private async Task RetrieveInstances(IDicomWebClient dicomWebClient, string studyInstanceUid, RequestedSeries series, string storagePath, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
            Guard.Against.NullOrWhiteSpace(studyInstanceUid, nameof(studyInstanceUid));
            Guard.Against.Null(series, nameof(series));
            Guard.Against.Null(storagePath, nameof(storagePath));
            Guard.Against.Null(retrievedInstance, nameof(retrievedInstance));

            foreach (var instance in series.Instances)
            {
                foreach (var sopInstanceUid in instance.SopInstanceUid)
                {
                    _logger.Log(LogLevel.Information, $"Retrieving instance {sopInstanceUid}");
                    var file = await dicomWebClient.Wado.Retrieve(studyInstanceUid, series.SeriesInstanceUid, sopInstanceUid);
                    var instanceStorageInfo = InstanceStorageInfo.CreateInstanceStorageInfo(file, storagePath, _fileSystem);
                    if (retrievedInstance.ContainsKey(instanceStorageInfo.SopInstanceUid))
                    {
                        _logger.Log(LogLevel.Warning, $"Instance '{instanceStorageInfo.SopInstanceUid}' already retrieved/stored.");
                        continue;
                    }

                    SaveFile(file, instanceStorageInfo);
                    retrievedInstance.Add(instanceStorageInfo.SopInstanceUid, instanceStorageInfo);
                }
            }
        }

        private async Task SaveFiles(IAsyncEnumerable<DicomFile> files, string storagePath, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
            Guard.Against.Null(files, nameof(files));
            Guard.Against.Null(storagePath, nameof(storagePath));
            Guard.Against.Null(retrievedInstance, nameof(retrievedInstance));

            var total = 0;
            var saved = 0;
            await foreach (var file in files)
            {
                total++;
                var instance = InstanceStorageInfo.CreateInstanceStorageInfo(file, storagePath, _fileSystem);
                if (retrievedInstance.ContainsKey(instance.SopInstanceUid))
                {
                    _logger.Log(LogLevel.Warning, $"Instance '{instance.SopInstanceUid}' already retrieved/stored.");
                    continue;
                }

                SaveFile(file, instance);
                retrievedInstance.Add(instance.SopInstanceUid, instance);
                saved++;
            }

            _logger.Log(LogLevel.Information, $"Saved {saved} out of {total} instances retrieved");
        }

        private void SaveFile(DicomFile file, InstanceStorageInfo instanceStorageInfo)
        {
            Guard.Against.Null(file, nameof(file));
            Guard.Against.Null(instanceStorageInfo, nameof(instanceStorageInfo));

            Policy.Handle<Exception>()
                .WaitAndRetry(3,
                (retryAttempt) =>
                {
                    return retryAttempt == 1 ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromMilliseconds(500);
                },
                (exception, retryCount, context) =>
                {
                    _logger.Log(LogLevel.Error, "Failed to save instance, retry count={retryCount}: {exception}", retryCount, exception);
                })
                .Execute(() =>
                {
                    _logger.Log(LogLevel.Information, "Saving DICOM instance {path}.", instanceStorageInfo.InstanceStorageFullPath);
                    _dicomToolkit.Save(file, instanceStorageInfo.InstanceStorageFullPath);
                    _logger.Log(LogLevel.Debug, "Instance saved successfully.");
                });
        }

        #endregion Data Retrieval
    }
}