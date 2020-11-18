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

using Ardalis.GuardClauses;
using Dicom;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.DicomWeb.Client.API;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Polly;
using System;
using System.Collections.Generic;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Jobs
{
    public class DataRetrievalService : IHostedService
    {
        private readonly IDicomWebClientFactory _dicomWebClientFactory;
        private readonly IInferenceRequestStore _inferenceRequestStore;
        private readonly ILogger<DataRetrievalService> _logger;
        private readonly IFileSystem _fileSystem;
        private readonly IDicomToolkit _dicomToolkit;
        private readonly IJobStore _jobStore;

        public DataRetrievalService(
            IDicomWebClientFactory dicomWebClientFactory,
            ILogger<DataRetrievalService> logger,
            IInferenceRequestStore inferenceRequestStore,
            IFileSystem fileSystem,
            IDicomToolkit dicomToolkit,
            IJobStore jobStore)
        {
            _dicomWebClientFactory = dicomWebClientFactory ?? throw new ArgumentNullException(nameof(dicomWebClientFactory));
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

            if (task.IsCompleted)
                return task;
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _logger.LogInformation("Data Retriever Hosted Service is stopping.");
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
                    _logger.Log(LogLevel.Warning, "Data Retriever Service canceled: {0}", ex.Message);
                }
                catch (InvalidOperationException ex)
                {
                    _logger.Log(LogLevel.Warning, "Data Retriever Service may be disposed: {0}", ex.Message);
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
            _logger.Log(LogLevel.Information, "Cancellation requested.");
        }

        private async Task ProcessRequest(InferenceRequest inferenceRequest, CancellationToken cancellationToken)
        {
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
            _logger.Log(LogLevel.Information, $"Queuing a new job '{inferenceRequest.JobName}' with pipeline '{inferenceRequest.Algorithm.PipelineId}', priority={inferenceRequest.ClaraJobPriority}, instance count={instances.Count()}");
            await _jobStore.Add(
                new Job
                {
                    JobId = inferenceRequest.JobId,
                    PayloadId = inferenceRequest.PayloadId
                }, inferenceRequest.JobName, instances.ToList());
        }

        private AuthenticationHeaderValue GenerateAuthenticationHeader(ConnectionAuthType authType, string authId)
        {
            Guard.Against.NullOrWhiteSpace(authId, nameof(authId));
            switch (authType)
            {
                case ConnectionAuthType.Basic:
                    return new AuthenticationHeaderValue("Basic", authId);

                default:
                    throw new InferenceRequestException($"Unsupported ConnectionAuthType: {authType}");
            }
        }

        #region Data Retrieval

        private void RestoreExistingInstances(InferenceRequest inferenceRequest, Dictionary<string, InstanceStorageInfo> retrievedInstances)
        {
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
            var authenticationHeaderValue = GenerateAuthenticationHeader(source.ConnectionDetails.AuthType, source.ConnectionDetails.AuthId);
            var dicomWebClient = _dicomWebClientFactory.CreateDicomWebClient(
                new Uri(source.ConnectionDetails.Uri),
                authenticationHeaderValue,
                null,
                null,
                null,
                null);

            switch (inferenceRequest.InputMetadata.Details.Type)
            {
                case InferenceRequestType.DicomUid:
                    await RetrieveStudies(inferenceRequest.InputMetadata.Details.Studies, inferenceRequest.StoragePath, dicomWebClient, retrievedInstance);
                    break;

                default:
                    throw new InferenceRequestException($"The 'inputMetadata' type '{inferenceRequest.InputMetadata.Details.Type}' specified is not supported.");
            }
        }

        private async Task RetrieveStudies(IList<RequestedStudy> studies, string storagePath, IDicomWebClient dicomWebClient, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
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
                    await RetrieveSeries(study, storagePath, dicomWebClient, retrievedInstance);
                }
            }
        }

        private async Task RetrieveSeries(RequestedStudy study, string storagePath, IDicomWebClient dicomWebClient, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
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
                    await RetrieveInstances(study.StudyInstanceUid, series, storagePath, dicomWebClient, retrievedInstance);
                }
            }
        }

        private async Task RetrieveInstances(string studyInstanceUid, RequestedSeries series, string storagePath, IDicomWebClient dicomWebClient, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
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