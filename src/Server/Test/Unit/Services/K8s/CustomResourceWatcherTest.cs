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
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Rest;
using Moq;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.Server.Services.K8s;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class TestSpec
    {
        public string Name { get; set; }
    }

    public class TestStatus
    {
        public int Count { get; set; }
    }

    public class TestCustomResource : CustomResource<TestSpec, TestStatus> { }

    public class TestCustomResourceList : CustomResourceList<TestCustomResource> { }

    public class CustomResourceWatcherTest
    {
        private Mock<ILogger> _logger;
        private Mock<IKubernetesWrapper> _k8sClient;
        private CustomResourceDefinition _customResourceDefinition;
        private CancellationTokenSource _cancellationTokenSource;

        public CustomResourceWatcherTest()
        {
            _logger = new Mock<ILogger>();
            _k8sClient = new Mock<IKubernetesWrapper>();
            _cancellationTokenSource = new CancellationTokenSource();
            _customResourceDefinition = new CustomResourceDefinition
            {
                ApiVersion = "test.nvidia.com/v1",
                PluralName = "Tests",
                Kind = "Test",
                Namespace = "test.nvidia.com"
            };
        }

        [RetryFact(DisplayName = "Start - Shall not start if already cancelled")]
        public void Start_ShallNotStartIfAlreadyCancelled()
        {
            var crdList = new TestCustomResourceList();
            _k8sClient.Setup(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(crdList)) }
                }));

            var watcher = new CustomResourceWatcher<TestCustomResourceList, TestCustomResource>(
                    _logger.Object, _k8sClient.Object, _customResourceDefinition, _cancellationTokenSource.Token, (eventType, item) =>
                    {
                    });
            _cancellationTokenSource.Cancel();
            watcher.Start(100);
            _k8sClient.Verify(v => v.ListNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>()), Times.Never());
            _logger.VerifyLogging($"No CRD found in type: {_customResourceDefinition.ApiVersion}/{_customResourceDefinition.Kind}", LogLevel.Warning, Times.Never());
            _logger.VerifyLogging($"Cancallation requested, CRD watcher will not be set.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "Stops polling when cancellation is requested")]
        public void Start_RespondsToCancellationRequest()
        {
            var crdList = new TestCustomResourceList();
            _k8sClient.Setup(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(crdList)) }
                }));

            var watcher = new CustomResourceWatcher<TestCustomResourceList, TestCustomResource>(
                    _logger.Object, _k8sClient.Object, _customResourceDefinition, _cancellationTokenSource.Token, (eventType, item) =>
                    {
                    });

            watcher.Start(100);
            Thread.Sleep(150);
            _cancellationTokenSource.Cancel();
            Thread.Sleep(100);
            _k8sClient.Verify(v => v.ListNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>()), Times.AtLeastOnce());
            _logger.VerifyLogging($"No CRD found in type: {_customResourceDefinition.ApiVersion}/{_customResourceDefinition.Kind}", LogLevel.Warning, Times.AtLeastOnce());
            _logger.VerifyLogging($"Cancallation requested, CRD watcher stopped.", LogLevel.Information, Times.Once());
        }

        [RetryFact(DisplayName = "Handled added/modified/deleted CRDs")]
        public void Start_HandledCrdEvents()
        {
            var crdList1 = TestData1();
            var crdList2 = TestData2();
            var crdList3 = TestData3();
            var crdList4 = TestData4();
            var crdList5 = TestData5();

            _k8sClient.SetupSequence(p => p.ListNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>()))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(crdList1)) }
                }))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(crdList2)) }
                }))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(crdList3)) }
                }))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(crdList4)) }
                }))
                .Returns(Task.FromResult(new HttpOperationResponse<object>
                {
                    Body = new object(),
                    Response = new HttpResponseMessage { Content = new StringContent(JsonConvert.SerializeObject(crdList5)) }
                }));

            var countdownEvent = new CountdownEvent(9);
            var callCount = 0;
            var addedCount = 0;
            var deletedCount = 0;
            var watcher = new CustomResourceWatcher<TestCustomResourceList, TestCustomResource>(
                    _logger.Object, _k8sClient.Object, _customResourceDefinition, _cancellationTokenSource.Token, (eventType, item) =>
                    {
                        Console.WriteLine($"Call #{callCount}");
                        switch (eventType)
                        {
                            case WatchEventType.Added:
                                Console.WriteLine($"Added {item.Metadata.Name}");
                                addedCount++;
                                break;

                            case WatchEventType.Deleted:
                                Console.WriteLine($"Deleted {item.Metadata.Name}");
                                deletedCount++;
                                break;
                        }

                        countdownEvent.Signal();
                    });

            watcher.Start(100);
            countdownEvent.Wait();
            _k8sClient.Verify(v => v.ListNamespacedCustomObjectWithHttpMessagesAsync(It.IsAny<CustomResourceDefinition>()), Times.AtLeastOnce());
            _logger.VerifyLogging($"No CRD found in type: {_customResourceDefinition.Namespace}/{_customResourceDefinition.PluralName}", LogLevel.Warning, Times.Never());
            Assert.Equal(6, addedCount);
            Assert.Equal(3, deletedCount);
        }

        /// <summary>
        /// Adds "first"
        /// </summary>
        private TestCustomResourceList TestData1()
        {
            return new TestCustomResourceList
            {
                Items = new List<TestCustomResource>
                {
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "first" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "1", Name = "first" }
                    }
                    }
            };
        }

        /// <summary>
        /// Adds "second" and "third"
        /// </summary>
        private TestCustomResourceList TestData2()
        {
            return new TestCustomResourceList
            {
                Items = new List<TestCustomResource>
                {
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "first" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "1", Name = "first" }
                    },
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "second" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "1", Name = "second" }
                    },
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "third" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "1", Name = "third" }
                    }
                    }
            };
        }

        /// <summary>
        /// Modifies "first"
        /// Deletes "third"
        /// </summary>
        private TestCustomResourceList TestData3()
        {
            return new TestCustomResourceList
            {
                Items = new List<TestCustomResource>
                {
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "first" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "2", Name = "first" }
                    },
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "second" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "1", Name = "second" }
                    }
                    }
            };
        }

        /// <summary>
        /// Modifies "first"
        /// Adds "fourth"
        /// </summary>
        private TestCustomResourceList TestData4()
        {
            return new TestCustomResourceList
            {
                Items = new List<TestCustomResource>
                {
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "first" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "3", Name = "first" }
                    },
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "second" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "1", Name = "second" }
                    },
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "fourth" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "1", Name = "fourth" }
                    }
                    }
            };
        }

        /// <summary>
        /// Deletes "second"
        /// Adds "fifth"
        /// Adds "third" back
        /// Deletes "fourth"
        /// </summary>
        private TestCustomResourceList TestData5()
        {
            return new TestCustomResourceList
            {
                Items = new List<TestCustomResource>
                {
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "first" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "3", Name = "first" }
                    },
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "fifth" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "1", Name = "fifth" }
                    },
                    new TestCustomResource
                    {
                    Spec = new TestSpec { Name = "third" },
                    Status = new TestStatus { Count = 1 }, Metadata = new V1ObjectMeta { ResourceVersion = "2", Name = "third" }
                    }
                    }
            };
        }
    }
}
