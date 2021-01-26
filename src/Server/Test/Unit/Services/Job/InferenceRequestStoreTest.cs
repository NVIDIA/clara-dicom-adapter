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

using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Rest;
using Moq;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Common;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class InferenceRequestStoreTest
    {
        private Mock<ILoggerFactory> _loggerFactory;
        private Mock<ILogger<InferenceRequestStore>> _logger;
        private IOptions<DicomAdapterConfiguration> _configuration;
        private Mock<IKubernetesWrapper> _kubernetesClient;
        private Mock<IJobs> _jobsApi;

        public InferenceRequestStoreTest()
        {
            _loggerFactory = new Mock<ILoggerFactory>();
            _logger = new Mock<ILogger<InferenceRequestStore>>();
            _configuration = Options.Create(new DicomAdapterConfiguration());
            _kubernetesClient = new Mock<IKubernetesWrapper>();
            _jobsApi = new Mock<IJobs>();

            _configuration.Value.CrdReadIntervals = 100;

            var logger = new Mock<ILogger<CustomResourceWatcher<InferenceRequestCustomResourceList, InferenceRequestCustomResource>>>();

            _loggerFactory.Setup(p => p.CreateLogger(It.IsAny<string>())).Returns((string type) =>
            {
                if (type.Equals("Nvidia.Clara.DicomAdapter.Server.Services.Jobs.InferenceRequestStore"))
                {
                    return _logger.Object;
                }
                return logger.Object;
            });
        }

        [RetryFact(DisplayName = "Constructor")]
        public void ConstructorTest()
        {
            Assert.Throws<ArgumentNullException>(() => new InferenceRequestStore(null, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new InferenceRequestStore(_loggerFactory.Object, null, null, null));
            Assert.Throws<ArgumentNullException>(() => new InferenceRequestStore(_loggerFactory.Object, _configuration, null, null));
            Assert.Throws<ArgumentNullException>(() => new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, null));

            new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);
        }

        [RetryFact(DisplayName = "Cancellation token shall stop the service")]
        public void CancellationTokenShallCancelTheService()
        {
            var cancellationTokenSource = new CancellationTokenSource();
            cancellationTokenSource.Cancel();

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);
            store.StartAsync(cancellationTokenSource.Token);
            store.StopAsync(cancellationTokenSource.Token);
            Thread.Sleep(100);
            _logger.VerifyLogging($"Inference Request Store Hosted Service is running.", LogLevel.Information, Times.Once());
            _logger.VerifyLogging($"Inference Request Store Hosted Service is stopping.", LogLevel.Information, Times.Once());
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

            var inferenceRequest = new InferenceRequest();
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);

            await Assert.ThrowsAsync<HttpOperationException>(async () => await store.Add(inferenceRequest));

            _logger.VerifyLoggingMessageBeginsWith($"Failed to add new inference request with JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD", LogLevel.Warning, Times.Exactly(3));
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

            var inferenceRequest = new InferenceRequest();
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);
            await store.Add(inferenceRequest);

            _logger.VerifyLoggingMessageBeginsWith($"Failed to add new inference request with JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD", LogLevel.Warning, Times.Never());
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Once());
            _logger.VerifyLoggingMessageBeginsWith($"Inference request saved. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD", LogLevel.Warning, Times.Never());
        }

        [RetryFact(DisplayName = "Update (Success) - Shall retry Move on failure")]
        public async Task UpdateSuccess_ShallRetryMoveOnFailure()
        {
            _kubernetesClient
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()))
                .Throws(new HttpOperationException("error message")
                {
                    Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Conflict), "error content")
                });

            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Throws(new HttpOperationException("error message")
                {
                    Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Conflict), "error content")
                });

            var inferenceRequest = new InferenceRequest();
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);

            await Assert.ThrowsAsync<HttpOperationException>(async () => await store.Update(inferenceRequest, InferenceRequestStatus.Success));

            _logger.VerifyLoggingMessageBeginsWith($"Failed to archive inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD.", LogLevel.Warning, Times.Exactly(3));
            _logger.VerifyLoggingMessageBeginsWith($"Inference request archived. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD.", LogLevel.Information, Times.Never());
            _logger.VerifyLoggingMessageBeginsWith($"Inference request deleted. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD.", LogLevel.Information, Times.Never());
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Exactly(4));
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), inferenceRequest.JobId), Times.Never());
        }

        [RetryFact(DisplayName = "Update (Success) - Shall retry Delete on failure")]
        public async Task UpdateSuccess_ShallRetryDeleteOnFailure()
        {
            _kubernetesClient
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Throws(new HttpOperationException("error message")
                {
                    Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Conflict), "error content")
                });

            var inferenceRequest = new InferenceRequest();
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);

            await Assert.ThrowsAsync<HttpOperationException>(async () => await store.Update(inferenceRequest, InferenceRequestStatus.Success));

            _logger.VerifyLoggingMessageBeginsWith($"Failed to delete inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD.", LogLevel.Warning, Times.Exactly(3));
            _logger.VerifyLoggingMessageBeginsWith($"Inference request archived. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith($"Inference request deleted. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}", LogLevel.Information, Times.Never());
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Once());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), inferenceRequest.JobId), Times.Exactly(4));
        }

        [RetryFact(DisplayName = "Update (Success) - Shall archive and delete")]
        public async Task UpdateSuccess_ShallArchiveAndDelete()
        {
            _kubernetesClient
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            var inferenceRequest = new InferenceRequest();
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);

            await store.Update(inferenceRequest, InferenceRequestStatus.Success);

            _logger.VerifyLoggingMessageBeginsWith($"Inference request archived. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith($"Inference request deleted. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}", LogLevel.Information, Times.Once());
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Once());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), inferenceRequest.JobId), Times.Once());
        }

        [RetryFact(DisplayName = "Update (Fail) - Shall delete if exceeds max retry")]
        public async Task UpdateFail_ShallDeleteJobIfExceedsMaxRetry()
        {
            _kubernetesClient
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

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

            var inferenceRequest = new InferenceRequest();
            inferenceRequest.TryCount = 3;
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);

            await store.Update(inferenceRequest, InferenceRequestStatus.Fail);

            _logger.VerifyLogging($"Exceeded maximum retries; removing inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} from Inference Request store.", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith($"Inference request archived. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith($"Inference request deleted. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}", LogLevel.Information, Times.Once());
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Once());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), inferenceRequest.JobId), Times.Once());
            _kubernetesClient.Verify(p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), It.IsAny<string>()), Times.Never());
        }

        [RetryFact(DisplayName = "Update (Fail) - Shall retry Update on failure")]
        public async Task UpdateFail_ShallRetryUpdateOnFailure()
        {
            _kubernetesClient
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            _kubernetesClient
                .Setup(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

            _kubernetesClient
                .Setup(p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), It.IsAny<string>()))
                .Throws(new HttpOperationException("error message")
                {
                    Response = new HttpResponseMessageWrapper(new HttpResponseMessage(HttpStatusCode.Conflict), "error content")
                });

            var inferenceRequest = new InferenceRequest();
            inferenceRequest.TryCount = 2;
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);

            await Assert.ThrowsAsync<HttpOperationException>(async () => await store.Update(inferenceRequest, InferenceRequestStatus.Fail));

            _logger.VerifyLoggingMessageBeginsWith($"Failed to update inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} in CRD.", LogLevel.Warning, Times.Exactly(3));
            _logger.VerifyLoggingMessageBeginsWith($"Updating inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith($"Inference request updated. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}", LogLevel.Information, Times.Never());
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Never());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), inferenceRequest.JobId), Times.Never());
            _kubernetesClient.Verify(p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), It.IsAny<string>()), Times.Exactly(4));
        }

        [RetryFact(DisplayName = "Update (Fail) - Shall update count and update CRD")]
        public async Task UpdateFail_ShallUpdateCountAndUpdateCrd()
        {
            _kubernetesClient
                .Setup(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Response = new HttpResponseMessage()
                }));

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

            var inferenceRequest = new InferenceRequest();
            inferenceRequest.TryCount = 2;
            inferenceRequest.JobId = Guid.NewGuid().ToString();
            inferenceRequest.PayloadId = Guid.NewGuid().ToString();
            inferenceRequest.TransactionId = Guid.NewGuid().ToString();

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);

            await store.Update(inferenceRequest, InferenceRequestStatus.Fail);

            _logger.VerifyLogging($"Inference request JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId} added back to Inference Request store for retry.", LogLevel.Information, Times.Once());
            _logger.VerifyLoggingMessageBeginsWith($"Inference request archived. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}", LogLevel.Information, Times.Never());
            _logger.VerifyLoggingMessageBeginsWith($"Inference request deleted. JobId={inferenceRequest.JobId}, TransactionId={inferenceRequest.TransactionId}", LogLevel.Information, Times.Never());
            _kubernetesClient.Verify(p => p.CreateNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>()), Times.Never());
            _kubernetesClient.Verify(p => p.DeleteNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), inferenceRequest.JobId), Times.Never());
            _kubernetesClient.Verify(p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<object>(), It.IsAny<string>()), Times.Once());
        }

        [RetryFact(DisplayName = "Take - Shall take job read from CRD")]
        public async Task Take_ShallReturnAJobReadFromCrd()
        {
            var cancellationSource = new CancellationTokenSource();
            var list = GenerateList();

            _kubernetesClient
                .SetupSequence(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>()))
                .Returns(
                    Task.FromResult(new HttpOperationResponse<object>
                    {
                        Body = new object(),
                        Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(list)) }
                    }))
                .Returns(() =>
                {
                    cancellationSource.Cancel();
                    Thread.Sleep(100);
                    throw new HttpOperationException("exception");
                });

            _kubernetesClient
                .Setup(p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>(), It.IsAny<CustomResource>(), It.IsAny<string>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage(HttpStatusCode.OK)
                }));

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);

            await store.StartAsync(cancellationSource.Token);

            var expectedItem = list.Items.Last();

            var item = await store.Take(cancellationSource.Token);
            Assert.Equal(expectedItem.Spec.JobId, item.JobId);
            _logger.VerifyLogging($"Inference request added to queue {item.JobId}", LogLevel.Debug, Times.AtLeastOnce());
            _logger.VerifyLogging($"Inference Request Store Hosted Service is running.", LogLevel.Information, Times.Once());

            await store.StopAsync(cancellationSource.Token);
            _logger.VerifyLogging($"Inference Request Store Hosted Service is stopping.", LogLevel.Information, Times.Once());

            _kubernetesClient.Verify(
                p => p.PatchNamespacedCustomObjectWithHttpMessagesAsync(
                    It.IsAny<CustomResourceDefinition>(),
                    It.IsAny<InferenceRequestCustomResource>(),
                    expectedItem.Spec.JobId), Times.Once());
        }

        [Fact(DisplayName = "Get - throws if no arguments provided")]
        public async Task Get_ThrowsIfNoArgumentsProvided()
        {
            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);
            await Assert.ThrowsAsync<ArgumentException>(async () => await store.Get(string.Empty, " "));
        }

        [Fact(DisplayName = "Get - retrieves from inference request archive")]
        public async Task Get_RetrievesFromInferenceRequestArchive()
        {
            var list = GenerateList();
            _kubernetesClient
                .Setup(
                    p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(
                        It.IsAny<CustomResourceDefinition>(),
                        It.IsAny<IDictionary<string, string>>()))
                .Returns(
                    Task.FromResult(new HttpOperationResponse<object>
                    {
                        Body = new object(),
                        Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(list)) }
                    }));

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);
            var jobId = Guid.NewGuid().ToString();
            var inferenceRequest = await store.Get(jobId, "");

            _kubernetesClient.Verify(
                p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    CustomResourceDefinition.InferenceRequestArchivesCrd,
                    It.Is<IDictionary<string, string>>(p => p.ContainsKey("JobId") && p["JobId"] == jobId)),
                Times.Once());
        }

        [Fact(DisplayName = "Get - retrieves from active inference request store if not found in archive")]
        public async Task Get_RetrievesFromActiveInferenceRequestStoreIfNotFoundInArchive()
        {
            var list = GenerateList();
            _kubernetesClient
                .Setup(
                    p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(
                        It.Is<CustomResourceDefinition>(p => p == CustomResourceDefinition.InferenceRequestArchivesCrd),
                        It.IsAny<IDictionary<string, string>>()))
                .Returns(
                    Task.FromResult(new HttpOperationResponse<object>
                    {
                        Body = new object(),
                        Response = new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest }
                    }));

            _kubernetesClient
                .Setup(
                    p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(
                        It.Is<CustomResourceDefinition>(p => p == CustomResourceDefinition.InferenceRequestsCrd),
                        It.IsAny<IDictionary<string, string>>()))
                .Returns(
                    Task.FromResult(new HttpOperationResponse<object>
                    {
                        Body = new object(),
                        Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(list)) }
                    }));

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);
            var jobId = Guid.NewGuid().ToString();
            var payloadId = Guid.NewGuid().ToString();
            var inferenceRequest = await store.Get(jobId, payloadId);

            _kubernetesClient.Verify(
                p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    It.IsAny<CustomResourceDefinition>(),
                    It.IsAny<IDictionary<string, string>>()),
                Times.Exactly(2));

            _logger.VerifyLogging($"Failed to query CRD {CustomResourceDefinition.InferenceRequestArchivesCrd.Kind}.", LogLevel.Error, Times.Once());
        }

        [Fact(DisplayName = "Status - retrieves by transaction id")]
        public async Task Status_RetrievesByTransactionId()
        {
            var list = GenerateList();

            _kubernetesClient
                .Setup(
                    p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(
                        It.IsAny<CustomResourceDefinition>(),
                        It.IsAny<IDictionary<string, string>>()))
                .Returns(
                    Task.FromResult(new HttpOperationResponse<object>
                    {
                        Body = new object(),
                        Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(list)) }
                    }));
            var jobTime = DateTime.Now;
            Platform.JobId.TryParse("job id", out Platform.JobId jobId);
            _jobsApi.Setup(p => p.Status(It.IsAny<string>()))
                .Returns(Task.FromResult(new Platform.JobDetails
                {
                    DateCreated = jobTime,
                    DateStarted = jobTime,
                    DateStopped = jobTime,
                    JobId = jobId,
                    JobState = Platform.JobState.Running,
                    JobPriority = Platform.JobPriority.Higher,
                    JobStatus = Platform.JobStatus.Healthy,
                }));

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);
            var id = Guid.NewGuid().ToString();
            var status = await store.Status(id);

            Assert.Equal("My Transaction ID", status.TransactionId);
            Assert.Equal(jobId.ToString(), status.Platform.JobId);
            Assert.Equal(Platform.JobState.Running, status.Platform.State);
            Assert.Equal(Platform.JobPriority.Higher, status.Platform.Priority);
            Assert.Equal(Platform.JobStatus.Healthy, status.Platform.Status);
            Assert.Equal(jobTime, status.Platform.Started);
            Assert.Equal(jobTime, status.Platform.Stopped);
            Assert.Equal(jobTime, status.Platform.Created);

            _kubernetesClient.Verify(
                p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    It.IsAny<CustomResourceDefinition>(),
                    It.IsAny<IDictionary<string, string>>()),
                Times.Once());

            _jobsApi.Verify(p => p.Status(It.IsAny<string>()), Times.Once());
        }

        [Fact(DisplayName = "Status - retrieves by job id")]
        public async Task Status_RetrievesByJobIdIfUnableToLocateByTransaction()
        {
            var list = GenerateList();

            _kubernetesClient
                .Setup(
                    p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(
                        It.IsAny<CustomResourceDefinition>(),
                        It.Is<IDictionary<string, string>>(p => p.ContainsKey("TransactionId"))))
                .Returns(
                    Task.FromResult(new HttpOperationResponse<object>
                    {
                        Body = new object(),
                        Response = new HttpResponseMessage { StatusCode = HttpStatusCode.BadRequest }
                    }));

            _kubernetesClient
                .Setup(
                    p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(
                        It.IsAny<CustomResourceDefinition>(),
                        It.Is<IDictionary<string, string>>(p => p.ContainsKey("JobId"))))
                .Returns(
                    Task.FromResult(new HttpOperationResponse<object>
                    {
                        Body = new object(),
                        Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(list)) }
                    }));

            Platform.JobId.TryParse("job id", out Platform.JobId jobId);
            var jobTime = DateTime.Now;
            _jobsApi.Setup(p => p.Status(It.IsAny<string>()))
                .Returns(Task.FromResult(new Platform.JobDetails
                {
                    DateCreated = jobTime,
                    DateStarted = jobTime,
                    DateStopped = jobTime,
                    JobId = jobId,
                    JobState = Platform.JobState.Running,
                    JobPriority = Platform.JobPriority.Higher,
                    JobStatus = Platform.JobStatus.Healthy,
                }));

            var store = new InferenceRequestStore(_loggerFactory.Object, _configuration, _kubernetesClient.Object, _jobsApi.Object);
            var id = Guid.NewGuid().ToString();
            var status = await store.Status(id);

            Assert.Equal("My Transaction ID", status.TransactionId);
            Assert.Equal(jobId.ToString(), status.Platform.JobId);
            Assert.Equal(Platform.JobState.Running, status.Platform.State);
            Assert.Equal(Platform.JobPriority.Higher, status.Platform.Priority);
            Assert.Equal(Platform.JobStatus.Healthy, status.Platform.Status);
            Assert.Equal(jobTime, status.Platform.Started);
            Assert.Equal(jobTime, status.Platform.Stopped);
            Assert.Equal(jobTime, status.Platform.Created);

            _kubernetesClient.Verify(
                p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    It.IsAny<CustomResourceDefinition>(),
                    It.IsAny<IDictionary<string, string>>()),
                Times.Exactly(3));

            _jobsApi.Verify(p => p.Status(It.IsAny<string>()), Times.Once());
        }

        private static InferenceRequestCustomResourceList GenerateList()
        {
            var list = new InferenceRequestCustomResourceList();
            list.Items = new List<InferenceRequestCustomResource>();
            list.Items.Add(new InferenceRequestCustomResource
            {
                Spec = new InferenceRequest
                {
                    TransactionId = "My Transaction ID",
                    State = InferenceRequestState.InProcess,
                    Status = InferenceRequestStatus.Unknown
                },
                Metadata = new V1ObjectMeta { Name = Guid.NewGuid().ToString() }
            });

            list.Items.Add(new InferenceRequestCustomResource
            {
                Spec = new InferenceRequest
                {
                    State = InferenceRequestState.InProcess,
                    Status = InferenceRequestStatus.Unknown
                },
                Metadata = new V1ObjectMeta { Name = Guid.NewGuid().ToString() }
            });

            list.Items.Add(new InferenceRequestCustomResource
            {
                Spec = new InferenceRequest
                {
                    State = InferenceRequestState.Queued,
                    Status = InferenceRequestStatus.Unknown
                },
                Metadata = new V1ObjectMeta { Name = Guid.NewGuid().ToString() }
            });
            return list;
        }
    }
}