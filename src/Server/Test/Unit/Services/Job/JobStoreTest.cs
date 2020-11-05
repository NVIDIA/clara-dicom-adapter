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

using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Moq;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using System.Collections.Generic;
using System.IO.Abstractions.TestingHelpers;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class JobStoreTest
    {
        private Mock<ILoggerFactory> _loggerFactory;
        private Mock<ILogger<JobStore>> _logger;
        private IOptions<DicomAdapterConfiguration> _configuration;
        private Mock<IKubernetesWrapper> _kubernetesClient;
        private MockFileSystem _fileSystem;

        public JobStoreTest()
        {
            _loggerFactory = new Mock<ILoggerFactory>();
            _logger = new Mock<ILogger<JobStore>>();
            _configuration = Options.Create(new DicomAdapterConfiguration());
            _kubernetesClient = new Mock<IKubernetesWrapper>();
            _fileSystem = new MockFileSystem();

            _configuration.Value.CrdReadIntervals = 100;

            var logger = new Mock<ILogger<CustomResourceWatcher<JobCustomResourceList, JobCustomResource>>>();
            _loggerFactory.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns((string type) =>
            {
                return logger.Object;
            });
        }

        [Fact(DisplayName = "New - Shall retry on failure")]
        public async Task New_ShallRetryOnFailure()
        {
            _kubernetesClient
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()))
                .Throws(new HttpOperationException("error message")
                {
                    Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Conflict), "error content")
                });

            var job = new Job();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _logger.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            var instance = InstanceGenerator.GenerateInstance("./aet", "aet", fileSystem: _fileSystem);
            await Assert.ThrowsAsync<HttpOperationException>(async () => await jobStore.New(job, "job-name", new List<InstanceStorageInfo> { instance }));

            _logger.VerifyLoggingMessageBeginsWith($"Failed to add save new job {job.JobId} in CRD", LogLevel.Warning, Times.Exactly(3));
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Exactly(4));
        }

        [Fact(DisplayName = "New - Shall add new job to CRD")]
        public async Task New_ShallAddItemToCrd()
        {
            _kubernetesClient
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            var job = new Job();
            job.JobId = Guid.NewGuid().ToString();
            job.PayloadId = Guid.NewGuid().ToString();

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _logger.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            var instance = InstanceGenerator.GenerateInstance("./aet", "aet");
            await jobStore.New(job, "job-name", new List<InstanceStorageInfo> { instance });

            _logger.VerifyLoggingMessageBeginsWith($"Failed to add save new job {job.JobId} in CRD", LogLevel.Warning, Times.Never());
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Once());
        }

        [Fact(DisplayName = "Complete - Shall retry on failure")]
        public async Task Complete_ShallRetryOnFailure()
        {
            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Throws(new HttpOperationException("error message")
                {
                    Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Conflict), "error content")
                });

            var item = new InferenceRequest("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _logger.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            await Assert.ThrowsAsync<HttpOperationException>(async () => await jobStore.Complete(item));

            _logger.VerifyLoggingMessageBeginsWith($"Failed to delete job {item.JobId} in CRD", LogLevel.Warning, Times.Exactly(3));
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), item.JobId), Times.Exactly(4));
        }

        [Fact(DisplayName = "Complete - Shall delete job stored in CRD")]
        public async Task Complete_ShallDeleteJobCrd()
        {
            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            var item = new InferenceRequest("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _logger.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            await jobStore.Complete(item);

            _logger.VerifyLogging($"Removing job {item.JobId} from job store as completed.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Job {item.JobId} removed from job store.", LogLevel.Information, Times.Once());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), item.JobId), Times.Once());
        }

        [Fact(DisplayName = "Fail - Shall delete job if exceeds max retry")]
        public async Task Fail_ShallDeleteJobIfExceedsMaxRetry()
        {
            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));
            _kubernetesClient
                .Setup(p => p.UpdateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            var item = new InferenceRequest("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });
            item.TryCount = 3;

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _logger.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            await jobStore.Fail(item);

            _logger.VerifyLogging($"Exceeded maximum job submission retries; removing job {item.JobId} from job store.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Job {item.JobId} removed from job store.", LogLevel.Information, Times.Once());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), item.JobId), Times.Once());
            _kubernetesClient.Verify(p => p.UpdateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), It.IsAny<string>()), Times.Never());
        }

        [Fact(DisplayName = "Fail - Shall update count and update CRD")]
        public async Task Fail_ShallUpdateCountAndUpdateCrd()
        {
            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));
            _kubernetesClient
                .Setup(p => p.UpdateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            var item = new InferenceRequest("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });
            item.TryCount = 2;

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _logger.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            await jobStore.Fail(item);

            _logger.VerifyLogging($"Adding job {item.JobId} back to job store for retry.", LogLevel.Debug, Times.Once());
            _logger.VerifyLogging($"Job {item.JobId} added back to job store for retry.", LogLevel.Information, Times.Once());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), item.JobId), Times.Never());
            _kubernetesClient.Verify(p => p.UpdateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), item.JobId), Times.Once());
        }

        [RetryFact(DisplayName = "Take - Shall take job read from CRD")]
        public void Take_ShallReturnAJobReadFromCrd()
        {
            var jobId = Guid.NewGuid().ToString();
            var jobList = new JobCustomResourceList();
            jobList.Items = new List<JobCustomResource>();
            jobList.Items.Add(new JobCustomResource
            {
                Spec = new InferenceRequest("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() })
                {
                    TryCount = 2
                },
                Metadata = new V1ObjectMeta { Name = jobId }
            });

            _kubernetesClient
                .Setup(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(jobList)) }
                }));

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _logger.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            var cancellationSource = new CancellationTokenSource();
            cancellationSource.CancelAfter(3000);
            jobStore.StartAsync(cancellationSource.Token);

            var item = jobStore.Take(cancellationSource.Token);
            Assert.Equal(jobList.Items.First().Spec.JobId, item.JobId);
            _logger.VerifyLogging($"Job added to queue {item.JobId}", LogLevel.Debug, Times.AtLeastOnce());
            _logger.VerifyLogging($"Job Store Hosted Service is running.", LogLevel.Information, Times.Once());

            jobStore.StopAsync(cancellationSource.Token);
            _logger.VerifyLogging($"Job Store Hosted Service is stopping.", LogLevel.Information, Times.Once());
        }
    }
}