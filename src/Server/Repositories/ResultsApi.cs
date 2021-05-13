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
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.ResultsService.Api;
using Polly;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Repositories
{
    public class ResultsApi : IResultsService, IDisposable
    {
        private readonly IOptions<DicomAdapterConfiguration> _configuration;
        private IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ResultsApi> _logger;

        public ResultsApi(
            IOptions<DicomAdapterConfiguration> configuration,
            IHttpClientFactory httpClientFactory,
            ILogger<ResultsApi> iLogger)
        {
            _logger = iLogger ?? throw new ArgumentNullException(nameof(iLogger));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        public async Task<IList<TaskResponse>> GetPendingJobs(string agent, CancellationToken cancellationToken, int count = 10)
        {
            using var httpClient = _httpClientFactory.CreateClient("results");
            httpClient.BaseAddress = new Uri(_configuration.Value.Services.ResultsServiceEndpoint);

            var retryPolicy = Policy<List<TaskResponse>>
                    .Handle<Exception>()
                    .WaitAndRetryAsync(3, (r) => TimeSpan.FromSeconds(r * 2.5f),
                    (exception, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, "Failed to retrieve pending tasks from Results Service: {0}", exception.Exception);
                    });

            var fallbackPolicy = Policy<List<TaskResponse>>
                    .Handle<Exception>()
                    .FallbackAsync<List<TaskResponse>>((t) => Task.FromResult(new List<TaskResponse>()));

            return await Policy.WrapAsync(fallbackPolicy, retryPolicy).ExecuteAsync(async () =>
            {
                var response = await httpClient.GetAsync(GenerateGetPendingJobsUri(agent, count), cancellationToken);
                response.EnsureSuccessStatusCode();

                var result = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<List<TaskResponse>>(result);
            }).ConfigureAwait(false);
        }

        public async Task<bool> ReportFailure(Guid taskId, bool retryLater, CancellationToken cancellationToken)
        {
            using var httpClient = _httpClientFactory.CreateClient("results");
            httpClient.BaseAddress = new Uri(_configuration.Value.Services.ResultsServiceEndpoint);
            
            var retryPolicy = Policy<bool>
                    .Handle<Exception>()
                    .WaitAndRetryAsync(3, (r) => TimeSpan.FromSeconds(r * 1.5f),
                    (exception, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, "Failed to report failed status for task {0}: {1}", taskId, exception.Exception);
                        return;
                    });

            var fallbackPolicy = Policy<bool>
                    .Handle<Exception>()
                    .FallbackAsync((t) => Task.FromResult(false));

            return await Policy.WrapAsync(fallbackPolicy, retryPolicy).ExecuteAsync(async () =>
            {
                var data = new StringContent(JsonConvert.SerializeObject(new { RetryLater = retryLater }), Encoding.UTF8, "application/json");
                var response = await httpClient.PutAsync(GenerateReportFailureUri(taskId), data, cancellationToken);
                response.EnsureSuccessStatusCode();
                return true;
            }).ConfigureAwait(false);
        }

        public async Task<bool> ReportSuccess(Guid taskId, CancellationToken cancellationToken)
        {
            using var httpClient = _httpClientFactory.CreateClient("results");
            httpClient.BaseAddress = new Uri(_configuration.Value.Services.ResultsServiceEndpoint);
            
            var retryPolicy = Policy<bool>
                    .Handle<Exception>()
                    .WaitAndRetryAsync(3, (r) => TimeSpan.FromSeconds(r * 1.5f),
                    (exception, retryCount, context) =>
                    {
                        _logger.Log(LogLevel.Error, "Failed to report successful status for task {0}: {1}", taskId, exception.Exception);
                        return;
                    });

            var fallbackPolicy = Policy<bool>
                    .Handle<Exception>()
                    .FallbackAsync((t) => Task.FromResult(false));

            return await Policy.WrapAsync(fallbackPolicy, retryPolicy).ExecuteAsync(async () =>
            {
                var response = await httpClient.PutAsync(GenerateReportSuccessUri(taskId), null, cancellationToken);
                response.EnsureSuccessStatusCode();
                return true;
            }).ConfigureAwait(false);
        }

        private string GenerateReportSuccessUri(Guid taskId)
        {
            return $"/api/tasks/success/{taskId}";
        }

        private string GenerateReportFailureUri(Guid taskId)
        {
            return $"/api/tasks/failure/{taskId}";
        }

        private string GenerateGetPendingJobsUri(string agent, int count)
        {
            return $"/api/tasks/{agent}/pending?size={count}";
        }

        public void Dispose()
        {
            _httpClientFactory = null;
        }
    }
}