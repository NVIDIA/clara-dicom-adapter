/*
 * Apache License, Version 2.0
 * Copyright 2021 NVIDIA Corporation
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

using Ardalis.GuardClauses;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.API.Rest;
using System;
using System.Collections.Generic;

namespace Nvidia.Clara.DicomAdapter.Database
{
    internal class InferenceRequestConfiguration : IEntityTypeConfiguration<InferenceRequest>
    {
        public void Configure(EntityTypeBuilder<InferenceRequest> builder)
        {
            var jsonSeriealizerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };

            builder.HasKey(j => j.InferenceRequestId);

            builder.Property(j => j.TransactionId).IsRequired();
            builder.Property(j => j.Priority).IsRequired();

            builder.Property(j => j.InputMetadata).HasConversion(
                        v => JsonConvert.SerializeObject(v, jsonSeriealizerSettings),
                        v => JsonConvert.DeserializeObject<InferenceRequestMetadata>(v, jsonSeriealizerSettings));

            builder.Property(j => j.InputResources).HasConversion(
                        v => JsonConvert.SerializeObject(v, jsonSeriealizerSettings),
                        v => JsonConvert.DeserializeObject<List<RequestInputDataResource>>(v, jsonSeriealizerSettings));

            builder.Property(j => j.OutputResources).HasConversion(
                        v => JsonConvert.SerializeObject(v, jsonSeriealizerSettings),
                        v => JsonConvert.DeserializeObject<List<RequestOutputDataResource>>(v, jsonSeriealizerSettings));

            builder.Property(j => j.InferenceRequestId).IsRequired().HasDefaultValue(Guid.NewGuid());
            builder.Property(j => j.JobId).IsRequired();
            builder.Property(j => j.PayloadId).IsRequired();
            builder.Property(j => j.State).IsRequired();
            builder.Property(j => j.Status).IsRequired();
            builder.Property(j => j.StoragePath).IsRequired();
            builder.Property(j => j.TryCount).IsRequired();

            builder.Ignore(p => p.Algorithm);
            builder.Ignore(p => p.ClaraJobPriority);
            builder.Ignore(p => p.JobName);
        }
    }
}