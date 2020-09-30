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

using System.Threading.Tasks;
using Ardalis.GuardClauses;
using k8s;
using Microsoft.Rest;

namespace Nvidia.Clara.DicomAdapter.Server.Services.K8s
{
    public interface IKubernetesWrapper
    {
        Task<Microsoft.Rest.HttpOperationResponse<object>> ListNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition crd);

        Task<Microsoft.Rest.HttpOperationResponse<object>> CreateNamespacedCustomObjectWithHttpMessagesAsync<T>(CustomResourceDefinition crd, T item);

        Task<Microsoft.Rest.HttpOperationResponse<object>> DeleteNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition crd, string name);
    }

    public class KubernetesClientWrapper : IKubernetesWrapper
    {
        private readonly Kubernetes _client;

        public KubernetesClientWrapper() : this(KubernetesClientConfiguration.BuildDefaultConfig())
        {
        }

        public KubernetesClientWrapper(KubernetesClientConfiguration config)
        {
            Guard.Against.Null(config, "config");
            _client = new Kubernetes(config);
        }

        public async Task<HttpOperationResponse<object>> ListNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition crd)
        {
            Guard.Against.Null(crd, "crd");
            Guard.Against.NullOrWhiteSpace(crd.ApiVersion, "crd.ApiVersion");
            Guard.Against.NullOrWhiteSpace(crd.Namespace, "crd.Namespace");
            Guard.Against.NullOrWhiteSpace(crd.PluralName, "crd.PluralName");

            return await _client.ListNamespacedCustomObjectWithHttpMessagesAsync(
                    group: crd.ApiVersion.Split('/')[0],
                    version: crd.ApiVersion.Split('/')[1],
                    namespaceParameter: crd.Namespace,
                    plural: crd.PluralName)
                .ConfigureAwait(false);
        }

        public async Task<HttpOperationResponse<object>> CreateNamespacedCustomObjectWithHttpMessagesAsync<T>(CustomResourceDefinition crd, T item)
        {
            Guard.Against.Null(crd, "crd");
            Guard.Against.NullOrWhiteSpace(crd.ApiVersion, "crd.ApiVersion");
            Guard.Against.NullOrWhiteSpace(crd.Namespace, "crd.Namespace");
            Guard.Against.NullOrWhiteSpace(crd.PluralName, "crd.PluralName");
            Guard.Against.Null(item, "item");

            return await _client.CreateNamespacedCustomObjectWithHttpMessagesAsync(
                    body: item,
                    group: crd.ApiVersion.Split('/')[0],
                    version: crd.ApiVersion.Split('/')[1],
                    namespaceParameter: crd.Namespace,
                    plural: crd.PluralName)
                .ConfigureAwait(false);
        }

        public async Task<HttpOperationResponse<object>> DeleteNamespacedCustomObjectWithHttpMessagesAsync(CustomResourceDefinition crd, string name)
        {
            Guard.Against.Null(crd, "crd");
            Guard.Against.NullOrWhiteSpace(crd.ApiVersion, "crd.ApiVersion");
            Guard.Against.NullOrWhiteSpace(crd.Namespace, "crd.Namespace");
            Guard.Against.NullOrWhiteSpace(crd.PluralName, "crd.PluralName");
            Guard.Against.NullOrWhiteSpace(name, "name");

            return await _client.DeleteNamespacedCustomObjectWithHttpMessagesAsync(
                    group: crd.ApiVersion.Split('/')[0],
                    version: crd.ApiVersion.Split('/')[1],
                    namespaceParameter: crd.Namespace,
                    plural: crd.PluralName,
                    name: name)
                .ConfigureAwait(false);
        }
    }
}
