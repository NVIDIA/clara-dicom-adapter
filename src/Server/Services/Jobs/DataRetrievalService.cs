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
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.Dicom.DicomWeb.Client;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Common;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Disk;
using Polly;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Services.Jobs
{
    public class DataRetrievalService : IHostedService, IClaraService
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<DataRetrievalService> _logger;
        private readonly IStorageInfoProvider _storageInfoProvider;
        private readonly IFileSystem _fileSystem;
        private readonly IDicomToolkit _dicomToolkit;
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly IInstanceCleanupQueue _cleanupQueue;

        public ServiceStatus Status { get; set; }

        public DataRetrievalService(
            ILoggerFactory loggerFactory,
            IHttpClientFactory httpClientFactory,
            ILogger<DataRetrievalService> logger,
            IFileSystem fileSystem,
            IDicomToolkit dicomToolkit,
            IServiceScopeFactory serviceScopeFactory,
            IInstanceCleanupQueue cleanupQueue,
            IStorageInfoProvider storageInfoProvider)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _dicomToolkit = dicomToolkit ?? throw new ArgumentNullException(nameof(dicomToolkit));
            _serviceScopeFactory = serviceScopeFactory ?? throw new ArgumentNullException(nameof(serviceScopeFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cleanupQueue = cleanupQueue ?? throw new ArgumentNullException(nameof(cleanupQueue));
            _storageInfoProvider = storageInfoProvider ?? throw new ArgumentNullException(nameof(storageInfoProvider));
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
                using var scope = _serviceScopeFactory.CreateScope();
                var repository = scope.ServiceProvider.GetRequiredService<IInferenceRequestRepository>();
                if (!_storageInfoProvider.HasSpaceAvailableToRetrieve)
                {
                    _logger.Log(LogLevel.Warning, $"Data retrieval paused due to insufficient storage space.  Available storage space: {_storageInfoProvider.AvailableFreeSpace:D}.");
                    continue;
                }
                InferenceRequest request = null;
                try
                {
                    request = await repository.Take(cancellationToken);
                    using (_logger.BeginScope(new LogginDataDictionary<string, object> { { "JobId", request.JobId }, { "TransactionId", request.TransactionId } }))
                    {
                        _logger.Log(LogLevel.Information, "Processing inference request.");
                        await ProcessRequest(request, cancellationToken);
                        await repository.Update(request, InferenceRequestStatus.Success);
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
                        await repository.Update(request, InferenceRequestStatus.Fail);
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

            var retrievedResources = new Dictionary<string, string>();
            RestoreExistingResources(inferenceRequest, retrievedResources);

            foreach (var source in inferenceRequest.InputResources)
            {
                switch (source.Interface)
                {
                    case InputInterfaceType.DicomWeb:
                        _logger.Log(LogLevel.Information, $"Processing input source '{source.Interface}' from {source.ConnectionDetails.Uri}");
                        await RetrieveViaDicomWeb(inferenceRequest, source, retrievedInstances);
                        break;

                    case InputInterfaceType.Fhir:
                        _logger.Log(LogLevel.Information, $"Processing input source '{source.Interface}' from {source.ConnectionDetails.Uri}");
                        await RetrieveViaFhir(inferenceRequest, source, retrievedResources);
                        break;

                    case InputInterfaceType.Algorithm:
                        continue;

                    default:
                        _logger.Log(LogLevel.Warning, $"Specified input interface is not supported '{source.Interface}`");
                        break;
                }
            }

            if (inferenceRequest.TryCount < InferenceRequestRepository.MaxRetryLimit && ShouldRetry(inferenceRequest))
            {
                throw new InferenceRequestException("One or more failures occurred while retrieving specified resources. Will retry later.");
            }

            if (retrievedInstances.Count == 0 && retrievedResources.Count == 0)
            {
                throw new InferenceRequestException("No DICOM instances/resources retrieved with the request.");
            }

            await SubmitPipelineJob(inferenceRequest, retrievedInstances.Select(p => p.Value), retrievedResources.Select(p => p.Value), cancellationToken);
            RemoveInstances(retrievedInstances.Select(p => p.Value.InstanceStorageFullPath));
            RemoveInstances(retrievedResources.Select(p => p.Value));
        }

        private bool ShouldRetry(InferenceRequest inferenceRequest)
        {
            foreach (var input in inferenceRequest.InputMetadata.Inputs)
            {
                if (input.Resources?.Any(p => !p.IsRetrieved) ?? false)
                {
                    return true;
                }

                if (input.Studies?.Any(p => !p.IsRetrieved) ?? false)
                {
                    return true;
                }

                if (input.Studies?.Any(s => s.Series?.Any(r => !r.IsRetrieved) ?? false) ?? false)
                {
                    return true;
                }

                if (input.Studies?.Any(s => s.Series?.Any(r => r.Instances?.Any(i => !i.IsRetrieved) ?? false) ?? false) ?? false)
                {
                    return true;
                }

            }
            return false;
        }

        private void RemoveInstances(IEnumerable<string> files)
        {
            _logger.Log(LogLevel.Debug, $"Notifying Disk Reclaimer to delete {files.Count()} instances.");
            foreach (var file in files)
            {
                _cleanupQueue.QueueInstance(file);
            }
            _logger.Log(LogLevel.Information, $"Notified Disk Reclaimer to delete {files.Count()} instances.");
        }

        private async Task SubmitPipelineJob(InferenceRequest inferenceRequest, IEnumerable<InstanceStorageInfo> instances, IEnumerable<string> resources, CancellationToken cancellationToken)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));

            if (instances.IsNullOrEmpty() && resources.IsNullOrEmpty())
            {
                throw new ArgumentNullException("no instances/resources found.", nameof(instances));
            }

            _logger.Log(LogLevel.Information, $"Queuing a new job '{inferenceRequest.JobName}' with pipeline '{inferenceRequest.Algorithm.PipelineId}', priority={inferenceRequest.ClaraJobPriority}, instance count={instances.Count() + resources.Count()}");

            using var scope = _serviceScopeFactory.CreateScope();
            var repository = scope.ServiceProvider.GetRequiredService<IJobRepository>();
            await repository.Add(
                new InferenceJob
                {
                    JobId = inferenceRequest.JobId,
                    PayloadId = inferenceRequest.PayloadId,
                    PipelineId = inferenceRequest.Algorithm.PipelineId,
                    JobName = inferenceRequest.JobName,
                    Instances = instances.ToList(),
                    State = InferenceJobState.Created,
                    Resources = resources.ToList(),
                    Source = inferenceRequest.TransactionId,
                }, false);
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

        private void RestoreExistingResources(InferenceRequest inferenceRequest, Dictionary<string, string> retrievedResources)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));
            Guard.Against.Null(retrievedResources, nameof(retrievedResources));

            _logger.Log(LogLevel.Debug, $"Restoring previously retrieved resources from {inferenceRequest.StoragePath}");
            foreach (var file in _fileSystem.Directory.EnumerateFiles(inferenceRequest.StoragePath, "*", System.IO.SearchOption.AllDirectories))
            {
                if (file.EndsWith(".dcm") || retrievedResources.ContainsKey(file))
                {
                    continue;
                }
                var key = _fileSystem.Path.GetFileName(file);
                retrievedResources.Add(key, file);
                _logger.Log(LogLevel.Debug, $"Restored previously retrieved resource {key}");
            }
        }

        private async Task RetrieveViaFhir(InferenceRequest inferenceRequest, RequestInputDataResource source, Dictionary<string, string> retrievedResources)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));
            Guard.Against.Null(retrievedResources, nameof(retrievedResources));

            foreach (var input in inferenceRequest.InputMetadata.Inputs)
            {
                if (input.Resources.IsNullOrEmpty())
                {
                    continue;
                }
                await RetrieveFhirResources(input, source, retrievedResources, inferenceRequest.StoragePath.GetFhirStoragePath());
            }
        }

        private async Task RetrieveFhirResources(InferenceRequestDetails requestDetails, RequestInputDataResource source, Dictionary<string, string> retrievedResources, string storagePath)
        {
            Guard.Against.Null(requestDetails, nameof(requestDetails));
            Guard.Against.Null(source, nameof(source));
            Guard.Against.Null(retrievedResources, nameof(retrievedResources));
            Guard.Against.NullOrWhiteSpace(storagePath, nameof(storagePath));

            var pendingResources = new Queue<FhirResource>(requestDetails.Resources.Where(p => !p.IsRetrieved));

            if (pendingResources.Count == 0)
            {
                return;
            }

            var authenticationHeaderValue = AuthenticationHeaderValueExtensions.ConvertFrom(source.ConnectionDetails.AuthType, source.ConnectionDetails.AuthId);

            var httpClient = _httpClientFactory.CreateClient("fhir");
            httpClient.DefaultRequestHeaders.Clear();
            httpClient.DefaultRequestHeaders.Authorization = authenticationHeaderValue;
            _fileSystem.Directory.CreateDirectory(storagePath);

            FhirResource resource = null;
            try
            {
                while (pendingResources.Count > 0)
                {
                    resource = pendingResources.Dequeue();
                    resource.IsRetrieved = await RetrieveFhirResource(
                        httpClient,
                        resource,
                        source,
                        retrievedResources,
                        storagePath,
                        requestDetails.FhirFormat,
                        requestDetails.FhirAcceptHeader);
                }
            }
            catch (System.Exception ex)
            {
                _logger.Log(LogLevel.Error, ex, $"Error retrieving FHIR resource {resource?.Type}/{resource?.Id}");
            }
        }

        private async Task<bool> RetrieveFhirResource(HttpClient httpClient, FhirResource resource, RequestInputDataResource source, Dictionary<string, string> retrievedResources, string storagePath, FhirStorageFormat fhirFormat, string acceptHeader)
        {
            Guard.Against.Null(httpClient, nameof(httpClient));
            Guard.Against.Null(resource, nameof(resource));
            Guard.Against.Null(source, nameof(source));
            Guard.Against.Null(retrievedResources, nameof(retrievedResources));
            Guard.Against.NullOrWhiteSpace(storagePath, nameof(storagePath));
            Guard.Against.NullOrWhiteSpace(acceptHeader, nameof(acceptHeader));

            _logger.Log(LogLevel.Debug, $"Retriving FHIR resource {resource.Type}/{resource.Id} with media format {acceptHeader} and file format {fhirFormat}.");
            var request = new HttpRequestMessage(HttpMethod.Get, $"{source.ConnectionDetails.Uri}{resource.Type}/{resource.Id}");
            request.Headers.Accept.Add(MediaTypeWithQualityHeaderValue.Parse(acceptHeader));
            var response = await Policy
                .HandleResult<HttpResponseMessage>(p => !p.IsSuccessStatusCode)
                .WaitAndRetryAsync(3,
                    (retryAttempt) =>
                    {
                        return retryAttempt == 1 ? TimeSpan.FromMilliseconds(250) : TimeSpan.FromMilliseconds(500);
                    },
                    (result, timeSpan, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, result.Exception, $"Failed to retrieve resource {resource.Type}/{resource.Id} with status code {result.Result.StatusCode}, retry count={retryCount}.");
                    })
                .ExecuteAsync(async () => await httpClient.SendAsync(request));

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                var filename = _fileSystem.Path.Combine(storagePath, $"{resource.Type}-{resource.Id}.{fhirFormat}".ToLowerInvariant());
                await _fileSystem.File.WriteAllTextAsync(filename, json);
                retrievedResources.Add(_fileSystem.Path.GetFileName(filename), filename);
                return true;
            }
            else
            {
                _logger.Log(LogLevel.Error, $"Error retriving FHIR resource {resource.Type}/{resource.Id}. Recevied HTTP status code {response.StatusCode}.");
                return false;
            }
        }

        private async Task RetrieveViaDicomWeb(InferenceRequest inferenceRequest, RequestInputDataResource source, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
            Guard.Against.Null(inferenceRequest, nameof(inferenceRequest));
            Guard.Against.Null(retrievedInstance, nameof(retrievedInstance));

            var authenticationHeaderValue = AuthenticationHeaderValueExtensions.ConvertFrom(source.ConnectionDetails.AuthType, source.ConnectionDetails.AuthId);

            var dicomWebClient = new DicomWebClient(_httpClientFactory.CreateClient("dicomweb"), _loggerFactory.CreateLogger<DicomWebClient>());
            dicomWebClient.ConfigureServiceUris(new Uri(source.ConnectionDetails.Uri, UriKind.Absolute));

            if (!(authenticationHeaderValue is null))
            {
                dicomWebClient.ConfigureAuthentication(authenticationHeaderValue);
            }

            foreach (var input in inferenceRequest.InputMetadata.Inputs)
            {
                switch (input.Type)
                {
                    case InferenceRequestType.DicomUid:
                        await RetrieveStudies(dicomWebClient, input.Studies, inferenceRequest.StoragePath.GetDicomStoragePath(), retrievedInstance);
                        break;

                    case InferenceRequestType.DicomPatientId:
                        await QueryStudies(dicomWebClient, inferenceRequest, retrievedInstance, $"{DicomTag.PatientID.Group:X4}{DicomTag.PatientID.Element:X4}", input.PatientId);
                        break;

                    case InferenceRequestType.AccessionNumber:
                        foreach (var accessionNumber in input.AccessionNumber)
                        {
                            await QueryStudies(dicomWebClient, inferenceRequest, retrievedInstance, $"{DicomTag.AccessionNumber.Group:X4}{DicomTag.AccessionNumber.Element:X4}", accessionNumber);
                        }
                        break;

                    case InferenceRequestType.FhireResource:
                        continue;
                    default:
                        throw new InferenceRequestException($"The 'inputMetadata' type '{input.Type}' specified is not supported.");
                }
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
                await RetrieveStudies(dicomWebClient, studies, inferenceRequest.StoragePath.GetDicomStoragePath(), retrievedInstance);
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
                if (study.IsRetrieved)
                {
                    continue;
                }
                try
                {
                    await RetrieveStudy(dicomWebClient, study, storagePath, retrievedInstance);
                }
                catch (System.Exception ex)
                {
                    _logger.Log(LogLevel.Error, ex, "Error retrieving studies.");
                }
            }
        }

        private async Task RetrieveStudy(IDicomWebClient dicomWebClient, RequestedStudy study, string storagePath, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
            Guard.Against.Null(study, nameof(study));
            Guard.Against.Null(storagePath, nameof(storagePath));
            Guard.Against.Null(retrievedInstance, nameof(retrievedInstance));
            
            if (study.Series.IsNullOrEmpty())
            {
                _logger.Log(LogLevel.Information, $"Retrieving study {study.StudyInstanceUid}");
                var files = dicomWebClient.Wado.Retrieve(study.StudyInstanceUid);
                await SaveFiles(files, storagePath, retrievedInstance);
                study.IsRetrieved = true;
            }
            else
            {
                await RetrieveSeries(dicomWebClient, study, storagePath, retrievedInstance);
                study.IsRetrieved = true;
            }

        }

        private async Task RetrieveSeries(IDicomWebClient dicomWebClient, RequestedStudy study, string storagePath, Dictionary<string, InstanceStorageInfo> retrievedInstance)
        {
            Guard.Against.Null(study, nameof(study));
            Guard.Against.Null(storagePath, nameof(storagePath));
            Guard.Against.Null(retrievedInstance, nameof(retrievedInstance));

            foreach (var series in study.Series)
            {
                if (series.IsRetrieved)
                {
                    continue;
                }
                if (series.Instances.IsNullOrEmpty())
                {
                    _logger.Log(LogLevel.Information, $"Retrieving series {series.SeriesInstanceUid}");
                    var files = dicomWebClient.Wado.Retrieve(study.StudyInstanceUid, series.SeriesInstanceUid);
                    await SaveFiles(files, storagePath, retrievedInstance);
                    series.IsRetrieved = true;
                }
                else
                {
                    await RetrieveInstances(dicomWebClient, study.StudyInstanceUid, series, storagePath, retrievedInstance);
                    series.IsRetrieved = true;
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
                if (instance.IsRetrieved)
                {
                    continue;
                }
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
                instance.IsRetrieved = true;
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