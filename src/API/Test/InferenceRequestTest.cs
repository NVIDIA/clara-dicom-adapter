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

using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Generic;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.API.Test
{
    public class InferenceRequestTest
    {
        [Fact(DisplayName = "Algorithm shall return null if no algorithm is defined")]
        public void Algorithm_ShallReturnNullOnNoMatch()
        {
            var request = new InferenceRequest();

            Assert.Null(request.Algorithm);
        }

        [Fact(DisplayName = "Algorithm shall return defined algorithm")]
        public void Algorithm_ShallReturnAlgorithm()
        {
            var request = new InferenceRequest();
            request.InputResources.Add(new RequestInputDataResource { Interface = InputInterfaceType.DicomWeb });
            request.InputResources.Add(new RequestInputDataResource { Interface = InputInterfaceType.Algorithm, ConnectionDetails = new InputConnectionDetails() });
            request.InputResources.Add(new RequestInputDataResource { Interface = InputInterfaceType.Dimse });

            Assert.NotNull(request.Algorithm);
        }

        [Fact(DisplayName = "ClaraJobPriority shall return mapped value")]
        public void ClaraJobPriority_ShallReturnMappedValue()
        {
            var request = new InferenceRequest();
            request.Priority = 0;
            Assert.Equal(JobPriority.Lower, request.ClaraJobPriority);

            request.Priority = 128;
            Assert.Equal(JobPriority.Normal, request.ClaraJobPriority);

            request.Priority = 250;
            Assert.Equal(JobPriority.Higher, request.ClaraJobPriority);

            request.Priority = 255;
            Assert.Equal(JobPriority.Immediate, request.ClaraJobPriority);
        }

        [Fact(DisplayName = "IsValidate shall return all errors")]
        public void IsValidate_ShallReturnAllErrors()
        {
            var request = new InferenceRequest();
            Assert.False(request.IsValid(out string _));

            request.InputResources.Add(new RequestInputDataResource { Interface = InputInterfaceType.Algorithm });
            Assert.False(request.IsValid(out string _));
        }

        [Fact(DisplayName = "IsValidate shall return false if no studies were defined")]
        public void IsValidate_ShallReturnFalseWithEmptyStudy()
        {
            var request = new InferenceRequest();
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.Algorithm,
                ConnectionDetails = new InputConnectionDetails()
            });
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.DicomWeb,
                ConnectionDetails = new InputConnectionDetails
                {
                    Uri = "http://this.is.not.a.valid.uri\\",
                    AuthId = "token",
                    AuthType = ConnectionAuthType.Bearer,
                }
            });
            request.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.DicomUid
                }
            };
            Assert.False(request.IsValid(out string _));
        }

        [Fact(DisplayName = "IsValidate shall return false if missing patient ID")]
        public void IsValidate_ShallReturnFalseWithoutPatientId()
        {
            var request = new InferenceRequest();
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.Algorithm,
                ConnectionDetails = new InputConnectionDetails()
            });
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.DicomWeb,
                ConnectionDetails = new InputConnectionDetails
                {
                    Uri = "http://this.is.not.a.valid.uri\\",
                    AuthId = "token",
                    AuthType = ConnectionAuthType.Bearer,
                }
            });
            request.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.DicomPatientId
                }
            };
            Assert.False(request.IsValid(out string _));
        }

        [Fact(DisplayName = "IsValidate shall return false if no accession number were defined")]
        public void IsValidate_ShallReturnFalseWithoutAccessNumbers()
        {
            var request = new InferenceRequest();
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.Algorithm,
                ConnectionDetails = new InputConnectionDetails()
            });
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.DicomWeb,
                ConnectionDetails = new InputConnectionDetails
                {
                    Uri = "http://this.is.not.a.valid.uri\\",
                    AuthId = "token",
                    AuthType = ConnectionAuthType.Bearer,
                }
            });
            request.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.AccessionNumber
                }
            };
            Assert.False(request.IsValid(out string _));
        }

        [Fact(DisplayName = "IsValidate shall return false for unknown request type")]
        public void IsValidate_ShallReturnFalseForUnknownRequestType()
        {
            var request = new InferenceRequest();
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.Algorithm,
                ConnectionDetails = new InputConnectionDetails()
            });
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.DicomWeb,
                ConnectionDetails = new InputConnectionDetails
                {
                    Uri = "http://this.is.not.a.valid.uri\\",
                    AuthId = "token",
                    AuthType = ConnectionAuthType.Bearer,
                }
            });
            request.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails()
            };
            Assert.False(request.IsValid(out string _));
        }

        [Fact(DisplayName = "IsValidate shall return false without a valid crednetial")]
        public void IsValidate_ShallReturnFalseWithoutAValidCredential()
        {
            var request = new InferenceRequest();
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.Algorithm,
                ConnectionDetails = new InputConnectionDetails()
            });
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.DicomWeb,
                ConnectionDetails = new InputConnectionDetails
                {
                    Uri = "http://this.is.not.a.valid.uri\\",
                    AuthType = ConnectionAuthType.Bearer,
                }
            });
            request.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails()
            };
            Assert.False(request.IsValid(out string _));
        }

        [Fact(DisplayName = "IsValidate shall return false with invalid uri")]
        public void IsValidate_ShallReturnFalseWithInvalidUri()
        {
            var request = new InferenceRequest();
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.Algorithm,
                ConnectionDetails = new InputConnectionDetails()
            });
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.DicomWeb,
                ConnectionDetails = new InputConnectionDetails
                {
                    Uri = "http://this.is.not.a.valid.uri\\",
                    AuthId = "token",
                    AuthType = ConnectionAuthType.Bearer,
                }
            });
            request.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.DicomUid,
                    Studies = new List<RequestedStudy>
                    {
                        new RequestedStudy
                        {
                            StudyInstanceUid = "1"
                        }
                    }
                }
            };
            Assert.False(request.IsValid(out string _));
        }

        [Fact(DisplayName = "IsValidate shall return true with valid request")]
        public void IsValidate_ShallReturnTrue()
        {
            var request = new InferenceRequest();
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.Algorithm,
                ConnectionDetails = new InputConnectionDetails()
            });
            request.InputResources.Add(new RequestInputDataResource
            {
                Interface = InputInterfaceType.DicomWeb,
                ConnectionDetails = new InputConnectionDetails
                {
                    Uri = "http://this.is.a/valid/uri",
                    AuthId = "token",
                    AuthType = ConnectionAuthType.Bearer
                }
            });
            request.OutputResources.Add(new RequestOutputDataResource
            {
                Interface = InputInterfaceType.DicomWeb,
                ConnectionDetails = new DicomWebConnectionDetails
                {
                    Uri = "http://this.is.a/valid/uri",
                    AuthId = "token",
                    AuthType = ConnectionAuthType.Bearer
                }
            });
            request.InputMetadata = new InferenceRequestMetadata
            {
                Details = new InferenceRequestDetails
                {
                    Type = InferenceRequestType.DicomUid,
                    Studies = new List<RequestedStudy>
                    {
                        new RequestedStudy
                        {
                            StudyInstanceUid = "1"
                        }
                    }
                }
            };
            Assert.True(request.IsValid(out string _));
        }

        [Fact(DisplayName = "ConfigureTemporaryStorageLocation shall throw when input is invalid")]
        public void ConfigureTemporaryStorageLocation_ShallThrowWithInvalidInput()
        {
            var request = new InferenceRequest();

            Assert.Throws<ArgumentNullException>(() =>
            {
                request.ConfigureTemporaryStorageLocation(null);
            });
            Assert.Throws<ArgumentException>(() =>
            {
                request.ConfigureTemporaryStorageLocation(" ");
            });
        }

        [Fact(DisplayName = "ConfigureTemporaryStorageLocation shall throw if already configured")]
        public void ConfigureTemporaryStorageLocation_ShallThrowIfAlreadyConfigured()
        {
            var request = new InferenceRequest();
            request.ConfigureTemporaryStorageLocation("/blabla");

            Assert.Throws<InferenceRequestException>(() =>
            {
                request.ConfigureTemporaryStorageLocation("/new-location");
            });
        }
    }
}