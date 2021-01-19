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
                if (type.Equals("Nvidia.Clara.DicomAdapter.Server.Services.Jobs.JobStore"))
                {
                    return _logger.Object;
                }
                return logger.Object;
            });
        }

        [RetryFact(DisplayName = "Add - Shall retry on failure")]
        public async Task Add_ShallRetryOnFailure()
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
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            var instance = InstanceGenerator.GenerateInstance("./aet", "aet", fileSystem: _fileSystem);
            await Assert.ThrowsAsync<HttpOperationException>(async () => await jobStore.Add(job, "job-name", new List<InstanceStorageInfo> { instance }));

            _logger.VerifyLoggingMessageBeginsWith($"Failed to add new job {job.JobId} in CRD", LogLevel.Warning, Times.Exactly(3));
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Exactly(4));
        }

        [RetryFact(DisplayName = "Add - Shall add new job to CRD")]
        public async Task Add_ShallAddItemToCrd()
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
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            var instance = InstanceGenerator.GenerateInstance("./aet", "aet", fileSystem: _fileSystem);
            await jobStore.Add(job, "job-name", new List<InstanceStorageInfo> { instance });

            _logger.VerifyLoggingMessageBeginsWith($"Failed to add save new job {job.JobId} in CRD", LogLevel.Warning, Times.Never());
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Once());
        }

        [RetryFact(DisplayName = "Update (Success) - Shall retry on failure")]
        public async Task UpdateSuccess_ShallRetryOnFailure()
        {
            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Throws(new HttpOperationException("error message")
                {
                    Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Conflict), "error content")
                });

            var item = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            await Assert.ThrowsAsync<HttpOperationException>(async () => await jobStore.Update(item, InferenceJobStatus.Success));

            _logger.VerifyLoggingMessageBeginsWith($"Failed to delete job {item.JobId} in CRD", LogLevel.Warning, Times.Exactly(3));
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), item.JobId), Times.Exactly(4));
        }

        [RetryFact(DisplayName = "Update (Success) - Shall delete job stored in CRD")]
        public async Task UpdateSuccess_ShallDeleteJobCrd()
        {
            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            var item = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            await jobStore.Update(item, InferenceJobStatus.Success);

            _logger.VerifyLogging($"Removing job {item.JobId} from job store as completed.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Job {item.JobId} removed from job store.", LogLevel.Information, Times.Once());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), item.JobId), Times.Once());
        }

        [RetryFact(DisplayName = "Update (Fail) - Shall delete job if exceeds max retry")]
        public async Task UpdateFail_ShallDeleteJobIfExceedsMaxRetry()
        {
            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));
            _kubernetesClient
                .Setup(p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            var item = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });
            item.TryCount = 3;

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            await jobStore.Update(item, InferenceJobStatus.Fail);

            _logger.VerifyLogging($"Exceeded maximum job submission retries; removing job {item.JobId} from job store.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Job {item.JobId} removed from job store.", LogLevel.Information, Times.Once());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), item.JobId), Times.Once());
            _kubernetesClient.Verify(p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), It.IsAny<string>()), Times.Never());
        }

        [RetryFact(DisplayName = "Update (Fail) - Shall update count and update CRD")]
        public async Task UpdateFail_ShallUpdateCountAndUpdateCrd()
        {
            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));
            _kubernetesClient
                .Setup(p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            var item = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() });
            item.TryCount = 2;

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            await jobStore.Update(item, InferenceJobStatus.Fail);

            _logger.VerifyLogging($"Adding job {item.JobId} back to job store for retry.", LogLevel.Debug, Times.Once());
            _logger.VerifyLogging($"Job {item.JobId} added back to job store for retry.", LogLevel.Information, Times.Once());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), item.JobId), Times.Never());
            _kubernetesClient.Verify(p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), item.JobId), Times.Once());
        }

        [RetryFact(DisplayName = "Take - Shall take job read from CRD")]
        public async Task Take_ShallReturnAJobReadFromCrd()
        {
            var cancellationSource = new CancellationTokenSource();
            var jobList = new JobCustomResourceList();
            jobList.Items = new List<JobCustomResource>();
            jobList.Items.Add(new JobCustomResource
            {
                Spec = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() })
                {
                    State = InferenceJobState.InProcess
                },
                Metadata = new V1ObjectMeta { Name = Guid.NewGuid().ToString() }
            });

            jobList.Items.Add(new JobCustomResource
            {
                Spec = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() })
                {
                    State = InferenceJobState.InProcess
                },
                Metadata = new V1ObjectMeta { Name = Guid.NewGuid().ToString() }
            });

            jobList.Items.Add(new JobCustomResource
            {
                Spec = new InferenceJob("/path/to/job", new Job { JobId = Guid.NewGuid().ToString(), PayloadId = Guid.NewGuid().ToString() })
                {
                    State = InferenceJobState.Queued
                },
                Metadata = new V1ObjectMeta { Name = Guid.NewGuid().ToString() }
            });

            _kubernetesClient
                .SetupSequence(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>()))
                .Returns(
                    Task.FromResult(new HttpOperationResponse<object>
                    {
                        Body = new object(),
                        Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(jobList)) }
                    }))
                .Returns(() =>
                {
                    cancellationSource.Cancel();
                    Thread.Sleep(100);
                    throw new HttpOperationException("exception");
                });

            _kubernetesClient
                .Setup(p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<JobCustomResource>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage(HttpStatusCode.OK)
                }));

            var jobStore = new JobStore(
                _loggerFactory.Object,
                _configuration,
                _kubernetesClient.Object,
                _fileSystem);

            await jobStore.StartAsync(cancellationSource.Token);

            var expectedItem = jobList.Items.Last();

            var item = await jobStore.Take(cancellationSource.Token);
            Assert.Equal(expectedItem.Spec.JobId, item.JobId);
            _logger.VerifyLogging($"Job added to queue {item.JobId}", LogLevel.Debug, Times.AtLeastOnce());
            _logger.VerifyLogging($"Job Store Hosted Service is running.", LogLevel.Information, Times.Once());

            await jobStore.StopAsync(cancellationSource.Token);
            _logger.VerifyLogging($"Job Store Hosted Service is stopping.", LogLevel.Information, Times.Once());

            _kubernetesClient.Verify(
                p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(
                    It.IsAny<CustomResourceDefinition>(),
                    It.IsAny<JobCustomResource>(),
                    expectedItem.Spec.JobId), Times.Once());
        }
    }
}