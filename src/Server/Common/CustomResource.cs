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

using k8s;
using k8s.Models;
using Newtonsoft.Json;
using Nvidia.Clara.Dicom.API;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Services.Jobs;

namespace Nvidia.Clara.DicomAdapter.Server.Common
{
    /// <summary>
    /// Describes a Kubernetes Custom Resource
    /// </summary>
    public class CustomResourceDefinition
    {
        public static readonly CustomResourceDefinition ClaraAeTitleCrd = new CustomResourceDefinition
        {
            ApiVersion = "dicom.clara.nvidia.com/v1beta2",
            PluralName = "claraaetitles",
            Kind = "ClaraAeTitle",
            Namespace = "default"
        };

        public static readonly CustomResourceDefinition SourceAeTitleCrd = new CustomResourceDefinition
        {
            ApiVersion = "dicom.clara.nvidia.com/v1beta2",
            PluralName = "sources",
            Kind = "Source",
            Namespace = "default"
        };

        public static readonly CustomResourceDefinition DestinationAeTitleCrd = new CustomResourceDefinition
        {
            ApiVersion = "dicom.clara.nvidia.com/v1beta2",
            PluralName = "destinations",
            Kind = "Destination",
            Namespace = "default"
        };

        public static readonly CustomResourceDefinition JobsCrd = new CustomResourceDefinition
        {
            ApiVersion = "dicom.clara.nvidia.com/v1beta2",
            PluralName = "jobs",
            Kind = "Job",
            Namespace = "default"
        };

        public static readonly CustomResourceDefinition InferenceRequestsCrd = new CustomResourceDefinition
        {
            ApiVersion = "dicom.clara.nvidia.com/v1beta2",
            PluralName = "inferenceRequests",
            Kind = "InferenceRequest",
            Namespace = "default"
        };

        public string ApiVersion { get; set; }

        public string PluralName { get; set; }

        public string Kind { get; set; }

        public string Namespace { get; set; }
    }

    /// <summary>
    /// Base class for Kubernetes Custom Resource Definition object.
    /// </summary>
    public abstract class CustomResource : KubernetesObject
    {
        [JsonProperty(PropertyName = "metadata")]
        public V1ObjectMeta Metadata { get; set; }
    }

    /// <summary>
    /// Extended base class for Kubernetes Custom Resource Definition object.
    /// </summary>
    public abstract class CustomResource<TSpec, TStatus> : CustomResource
    {
        [JsonProperty(PropertyName = "spec")]
        public TSpec Spec { get; set; }

        [JsonProperty(PropertyName = "status")]
        public TStatus Status { get; set; }
    }

    /// <summary>
    /// Status of a DICOM AE Title
    /// </summary>
    public class AeTitleStatus
    {
        public static AeTitleStatus Default = new AeTitleStatus { Enabled = true };
        public bool Enabled { get; set; } = true;
    }

    /// <summary>
    /// Kubernetes CRD for Clara's local AE Title
    /// </summary>
    public class ClaraApplicationEntityCustomResource : CustomResource<ClaraApplicationEntity, AeTitleStatus> { }

    /// <summary>
    /// Kubernetes CRD for Clara's local AE Title
    /// </summary>
    public class SourceApplicationEntityCustomResource : CustomResource<SourceApplicationEntity, AeTitleStatus> { }

    /// <summary>
    /// Kubernetes CRD for destination AE Title
    /// </summary>
    public class DestinationApplicationEntityCustomResource : CustomResource<DestinationApplicationEntity, AeTitleStatus> { }

    /// <summary>
    /// Kubernetes CRD to track each job's status for the Job Submitter Service
    /// </summary>
    public class JobCustomResource : CustomResource<InferenceJob, InferenceJobCrdStatus> { }

    /// <summary>
    /// Kubernetes CRD to track each inference request
    /// </summary>
    public class InferenceRequestCustomResource : CustomResource<InferenceRequest, InferenceRequestStatus> { }
}