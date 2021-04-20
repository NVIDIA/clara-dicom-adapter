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

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nvidia.Clara.Common;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.Platform;
using Polly;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Repositories
{
    public class ClaraJobsApi : IJobs
    {
        private readonly ILogger<ClaraJobsApi> _logger;
        private readonly IJobsClient _jobsClient;

        public ClaraJobsApi(
            IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration,
            ILogger<ClaraJobsApi> logger) : this(
                InitializeJobsClient(dicomAdapterConfiguration),
                logger)
        {
            logger.Log(LogLevel.Information, "ClaraJobsApi initialized with {0}", dicomAdapterConfiguration.Value.Services.Platform.Endpoint);
        }

        public ClaraJobsApi(
            IJobsClient jobsClient,
            ILogger<ClaraJobsApi> iLogger)
        {
            _jobsClient = jobsClient ?? throw new ArgumentNullException(nameof(jobsClient));
            _logger = iLogger ?? throw new ArgumentNullException(nameof(iLogger));
        }

        public async Task<Job> Create(string pipeline, string jobName, JobPriority jobPriority, IDictionary<string,string> metadata)
        {
            return await Policy.Handle<Exception>()
                .WaitAndRetryAsync(
                    1,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, "Exception while creating a new job: {exception}", exception);
                    })
                .ExecuteAsync(async () =>
                {
                    if (!PipelineId.TryParse(pipeline, out PipelineId pipelineId))
                    {
                        throw new ConfigurationException($"Invalid Pipeline ID configured: {pipeline}");
                    }

                    var response = await _jobsClient.CreateJob(pipelineId, jobName, jobPriority, metadata);
                    var job = ConvertResponseToJob(response);
                    _logger.Log(LogLevel.Information, "Clara Job.Create API called successfully, Pipeline={0}, JobId={1}, JobName={2}", pipeline, job.JobId, jobName);
                    return job;
                }).ConfigureAwait(false);
        }

        public async Task Start(Job job)
        {
            await Policy.Handle<Exception>()
                .WaitAndRetryAsync(
                    1,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, "Exception while starting a new job: {exception}", exception);
                    })
                .ExecuteAsync(async () =>
                {
                    if (!JobId.TryParse(job.JobId, out JobId jobId))
                    {
                        throw new ArgumentException($"Invalid JobId provided: {job.JobId}");
                    }
                    var response = await _jobsClient.StartJob(jobId, null);
                    _logger.Log(LogLevel.Information, "Clara Job.Start API called successfully with state={0}, status={1}",
                        response.JobState,
                        response.JobStatus);
                }).ConfigureAwait(false);
        }

        public async Task AddMetadata(Job job, IDictionary<string,string> metadata)
        {
            await Policy.Handle<Exception>()
                .WaitAndRetryAsync(
                    1,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, "Exception while adding job metadata: {exception}", exception);
                    })
                .ExecuteAsync(async () =>
                {
                    if (!JobId.TryParse(job.JobId, out JobId jobId))
                    {
                        throw new ArgumentException($"Invalid JobId provided: {job.JobId}");
                    }
                    var response = await _jobsClient.AddMetadata(jobId, metadata);
                    _logger.Log(LogLevel.Information, "Clara Job.AddMetadata API called successfully.");
                }).ConfigureAwait(false);
        }

        public async Task<JobDetails> Status(string jobId)
        {
            return await Policy.Handle<Exception>()
                .WaitAndRetryAsync(
                    1,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)),
                    (exception, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, "Exception while starting a new job: {exception}", exception);
                    })
                .ExecuteAsync(async () =>
                {
                    if (!JobId.TryParse(jobId, out JobId jobIdObj))
                    {
                        throw new ArgumentException($"Invalid JobId provided: {jobId}");
                    }
                    var response = await _jobsClient.GetStatus(jobIdObj);
                    _logger.Log(LogLevel.Information, "Clara Job.GetStatus API called successfully.");
                    return response;
                }).ConfigureAwait(false);
        }

        private static IJobsClient InitializeJobsClient(IOptions<DicomAdapterConfiguration> dicomAdapterConfiguration)
        {
            var serviceContext = ServiceContext.Create();
            BaseClient.InitializeServiceContext(serviceContext);
            return new JobsClient(serviceContext, dicomAdapterConfiguration.Value.Services.Platform.Endpoint);
        }

        private Job ConvertResponseToJob(JobInfo response)
        {
            return new Job
            {
                JobId = response.JobId.ToString(),
                PayloadId = response.PayloadId.ToString()
            };
        }
    }
}