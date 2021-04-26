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

using Grpc.Core;
using Microsoft.Extensions.Logging;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Configuration;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using Nvidia.Clara.DicomAdapter.Test.Shared;
using Nvidia.Clara.Platform;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using xRetry;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class ClaraJobsApiTest
    {
        public ClaraJobsApiTest()
        {
        }

        #region Create Job

        [RetryFact(DisplayName = "Create shall throw on bad pipelineId")]
        public void Create_ShallThrowOnBadPipelineId()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            mockClient.Setup(p => p.CreateJob(It.IsAny<PipelineId>(), It.IsAny<string>(), It.IsAny<JobPriority>(), It.IsAny<Dictionary<string, string>>()));

            var service = new ClaraJobsApi(mockClient.Object, mockLogger.Object);

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.Create("bad pipeline id", "bla bla", JobPriority.Higher, new JobMetadataBuilder()).Wait();
            });

            Assert.IsType<ConfigurationException>(exception.InnerException);
            mockClient.Verify(
                p => p.CreateJob(It.IsAny<PipelineId>(), It.IsAny<string>(), JobPriority.Higher, It.IsAny<Dictionary<string, string>>()),
                Times.Never());

            mockLogger.VerifyLogging(LogLevel.Error, Times.Exactly(1));
        }

        [RetryFact(DisplayName = "Create shall respect retry policy on failures")]
        public void Create_ShallRespectRetryPolicyOnFailure()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            mockClient.Setup(p => p.CreateJob(It.IsAny<PipelineId>(), It.IsAny<string>(), It.IsAny<JobPriority>(), It.IsAny<Dictionary<string, string>>()))
                .Throws(new RpcException(Status.DefaultCancelled));

            var service = new ClaraJobsApi(mockClient.Object, mockLogger.Object);

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.Create(Guid.NewGuid().ToString("N"), "bla bla", JobPriority.Lower, new JobMetadataBuilder()).Wait();
            });

            Assert.IsType<RpcException>(exception.InnerException);
            mockClient.Verify(
                p => p.CreateJob(It.IsAny<PipelineId>(), It.IsAny<string>(), JobPriority.Lower, It.IsAny<Dictionary<string, string>>()),
                Times.Exactly(2));

            mockLogger.VerifyLogging(LogLevel.Error, Times.Exactly(1));
        }

        [RetryFact(DisplayName = "Create shall return a job")]
        public async Task Create_ShallReturnAJob()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            JobId.TryParse(Guid.NewGuid().ToString("N"), out JobId jobId);
            PayloadId.TryParse(Guid.NewGuid().ToString("N"), out PayloadId payloadId);
            PipelineId.TryParse(Guid.NewGuid().ToString("N"), out PipelineId pipelineId);

            mockClient.Setup(p => p.CreateJob(It.IsAny<PipelineId>(), It.IsAny<string>(), It.IsAny<JobPriority>(), It.IsAny<Dictionary<string, string>>()))
                .ReturnsAsync(new JobInfo
                {
                    Name = "bla bla job",
                    JobId = jobId,
                    PayloadId = payloadId,
                    PipelineId = pipelineId
                });

            var service = new ClaraJobsApi(mockClient.Object, mockLogger.Object);

            var metadata = new JobMetadataBuilder();
            metadata.AddSourceName("TestSource");
            var job = await service.Create(pipelineId.ToString(), "bla bla", JobPriority.Higher, metadata);

            Assert.Equal(jobId.ToString(), job.JobId);
            Assert.Equal(payloadId.ToString(), job.PayloadId);

            mockClient.Verify(
                p => p.CreateJob(It.IsAny<PipelineId>(), It.IsAny<string>(), JobPriority.Higher, metadata),
                Times.Exactly(1));

            mockLogger.VerifyLogging(LogLevel.Information, Times.Once());
            mockLogger.VerifyLogging(LogLevel.Error, Times.Never());
        }

        #endregion Create Job

        #region Start Job

        [RetryFact(DisplayName = "Start shall throw on bad jobId")]
        public void Start_ShallThrowOnBadPipelineId()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            mockClient.Setup(p => p.StartJob(It.IsAny<JobId>(), It.IsAny<List<KeyValuePair<string, string>>>()));

            var service = new ClaraJobsApi(mockClient.Object, mockLogger.Object);

            var job = new Job
            {
                JobId = "bad-job-id-has-dashes",
                PayloadId = Guid.NewGuid().ToString("N")
            };

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.Start(job).Wait();
            });

            Assert.IsType<ArgumentException>(exception.InnerException);
            mockClient.Verify(
                p => p.StartJob(It.IsAny<JobId>(), It.IsAny<List<KeyValuePair<string, string>>>()),
                Times.Never());

            mockLogger.VerifyLogging(LogLevel.Error, Times.Exactly(1));
        }

        [RetryFact(DisplayName = "Start shall respect retry policy on failures")]
        public void Start_ShallRespectRetryPolicyOnFailure()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            mockClient.Setup(p => p.StartJob(It.IsAny<JobId>(), It.IsAny<List<KeyValuePair<string, string>>>()))
                .Throws(new RpcException(Status.DefaultCancelled));

            var service = new ClaraJobsApi(mockClient.Object, mockLogger.Object);

            var job = new Job
            {
                JobId = Guid.NewGuid().ToString("N"),
                PayloadId = Guid.NewGuid().ToString("N")
            };

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.Start(job).Wait();
            });

            Assert.IsType<RpcException>(exception.InnerException);
            mockClient.Verify(
                p => p.StartJob(It.IsAny<JobId>(), It.IsAny<List<KeyValuePair<string, string>>>()),
                Times.Exactly(2));

            mockLogger.VerifyLogging(LogLevel.Error, Times.Exactly(1));
        }

        [RetryFact(DisplayName = "Start shall be able to start a job successfully")]
        public void StartShallRunThrough()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();
            var job = new Job
            {
                JobId = Guid.NewGuid().ToString("N"),
                PayloadId = Guid.NewGuid().ToString("N")
            };
            JobId.TryParse(job.JobId, out JobId jobId);

            mockClient.Setup(p => p.StartJob(It.IsAny<JobId>(), It.IsAny<List<KeyValuePair<string, string>>>()))
                .ReturnsAsync(new JobToken
                {
                    JobId = jobId,
                    JobState = JobState.Pending,
                    JobStatus = Nvidia.Clara.Platform.JobStatus.Healthy
                });

            var service = new ClaraJobsApi(
                mockClient.Object, mockLogger.Object);

            service.Start(job).Wait();

            mockClient.Verify(
                p => p.StartJob(It.IsAny<JobId>(), It.IsAny<List<KeyValuePair<string, string>>>()),
                Times.Exactly(1));

            mockLogger.VerifyLogging(LogLevel.Error, Times.Never());
            mockLogger.VerifyLogging(LogLevel.Information, Times.Once());
        }

        #endregion Start Job

        #region Status

        [RetryFact(DisplayName = "Status shall throw on bad jobId")]
        public void Status_ShallThrowOnBadPipelineId()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            mockClient.Setup(p => p.GetStatus(It.IsAny<JobId>()));

            var service = new ClaraJobsApi(mockClient.Object, mockLogger.Object);

            var jobId = "bad job id has spaces";

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.Status(jobId).Wait();
            });

            Assert.IsType<ArgumentException>(exception.InnerException);
            mockClient.Verify(
                p => p.GetStatus(It.IsAny<JobId>()),
                Times.Never());

            mockLogger.VerifyLogging(LogLevel.Error, Times.Exactly(1));
        }

        [RetryFact(DisplayName = "Status shall respect retry policy on failures")]
        public void Status_ShallRespectRetryPolicyOnFailure()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            mockClient.Setup(p => p.GetStatus(It.IsAny<JobId>()))
                .Throws(new RpcException(Status.DefaultCancelled));

            var service = new ClaraJobsApi(mockClient.Object, mockLogger.Object);

            var jobId = Guid.NewGuid().ToString("N");

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.Status(jobId).Wait();
            });

            Assert.IsType<RpcException>(exception.InnerException);
            mockClient.Verify(
                p => p.GetStatus(It.IsAny<JobId>()),
                Times.Exactly(2));

            mockLogger.VerifyLogging(LogLevel.Error, Times.Exactly(1));
        }

        [RetryFact(DisplayName = "Status shall be able to retrieve job status successfully")]
        public void StatusShallRunThrough()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            var jobId = Guid.NewGuid().ToString("N");
            var jobDate = DateTime.UtcNow;
            JobId.TryParse(jobId, out JobId jobIdObj);

            mockClient.Setup(p => p.GetStatus(It.IsAny<JobId>()))
                .ReturnsAsync(new JobDetails
                {
                    JobId = jobIdObj,
                    JobState = JobState.Pending,
                    JobStatus = Nvidia.Clara.Platform.JobStatus.Healthy,
                    DateCreated = jobDate,
                    DateStarted = jobDate,
                    DateStopped = jobDate,
                    JobPriority = JobPriority.Higher,
                    Name = "name"
                });

            var service = new ClaraJobsApi(
                mockClient.Object, mockLogger.Object);

            service.Status(jobId).Wait();

            mockClient.Verify(
                p => p.GetStatus(It.IsAny<JobId>()),
                Times.Exactly(1));

            mockLogger.VerifyLogging(LogLevel.Error, Times.Never());
            mockLogger.VerifyLogging(LogLevel.Information, Times.Once());
        }

        #endregion Status

        #region AddMetadata

        [RetryFact(DisplayName = "AddMetadata shall throw on bad jobId")]
        public void AddMetadata_ShallThrowOnBadPipelineId()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            mockClient.Setup(p => p.AddMetadata(It.IsAny<JobId>(), It.IsAny<Dictionary<string, string>>()));

            var service = new ClaraJobsApi(mockClient.Object, mockLogger.Object);

            var job = new Job
            {
                JobId = "bad job id has spaces",
                PayloadId = "12345"
            };

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.AddMetadata(job, new Dictionary<string, string>()).Wait();
            });

            Assert.IsType<ArgumentException>(exception.InnerException);
            mockClient.Verify(
                p => p.AddMetadata(It.IsAny<JobId>(), It.IsAny<Dictionary<string, string>>()),
                Times.Never());

            mockLogger.VerifyLogging(LogLevel.Error, Times.Exactly(1));
        }

        [RetryFact(DisplayName = "AddMetadata shall respect retry policy on failures")]
        public void AddMetadata_ShallRespectRetryPolicyOnFailure()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            mockClient.Setup(p => p.AddMetadata(It.IsAny<JobId>(), It.IsAny<Dictionary<string, string>>()))
                .Throws(new RpcException(Status.DefaultCancelled));

            var service = new ClaraJobsApi(mockClient.Object, mockLogger.Object);

            var job = new Job
            {
                JobId = Guid.NewGuid().ToString("N"),
                PayloadId = Guid.NewGuid().ToString("N")
            };

            var exception = Assert.Throws<AggregateException>(() =>
            {
                service.AddMetadata(job, new Dictionary<string, string>()).Wait();
            });

            Assert.IsType<RpcException>(exception.InnerException);
            mockClient.Verify(
                p => p.AddMetadata(It.IsAny<JobId>(), It.IsAny<Dictionary<string, string>>()),
                Times.Exactly(2));

            mockLogger.VerifyLogging(LogLevel.Error, Times.Exactly(1));
        }

        [RetryFact(DisplayName = "AddMetadata shall be able to add metadata successfully")]
        public void AddMetadata_ShallRunThrough()
        {
            var mockClient = new Mock<IJobsClient>();
            var mockLogger = new Mock<ILogger<ClaraJobsApi>>();

            var jobId = Guid.NewGuid().ToString("N");
            var jobDate = DateTime.UtcNow;
            JobId.TryParse(jobId, out JobId jobIdObj);

            mockClient.Setup(p => p.AddMetadata(It.IsAny<JobId>(), It.IsAny<Dictionary<string, string>>()));

            var service = new ClaraJobsApi(
                mockClient.Object, mockLogger.Object);

            var job = new Job
            {
                JobId = Guid.NewGuid().ToString("N"),
                PayloadId = Guid.NewGuid().ToString("N")
            };

            service.AddMetadata(job, new Dictionary<string, string>()).Wait();

            mockClient.Verify(
                p => p.AddMetadata(It.IsAny<JobId>(), It.IsAny<Dictionary<string, string>>()),
                Times.Exactly(1));

            mockLogger.VerifyLogging(LogLevel.Error, Times.Never());
            mockLogger.VerifyLogging(LogLevel.Information, Times.Once());
        }

        #endregion AddMetadata
    }
}