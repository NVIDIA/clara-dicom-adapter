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

using Microsoft.EntityFrameworkCore;
using Nvidia.Clara.DicomAdapter.API;

namespace Nvidia.Clara.DicomAdapter.Database
{
    internal class InferenceJobConfiguration : IEntityTypeConfiguration<InferenceJob>
    {
        public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<InferenceJob> builder)
        {
            builder.HasKey(f => f.InferenceJobId);

            builder.Property(f => f.JobId);
            builder.Property(f => f.PayloadId).IsRequired();
            builder.Property(f => f.JobPayloadsStoragePath).IsRequired();
            builder.Property(f => f.TryCount).IsRequired();
            builder.Property(f => f.State).IsRequired();
            builder.Property(f => f.LastUpdate).IsRequired();
            builder.Property(f => f.JobName).IsRequired();
            builder.Property(f => f.PipelineId).IsRequired();
            builder.Property(f => f.Priority).IsRequired();
            builder.Property(f => f.Source).IsRequired();

            builder.Ignore(f => f.Instances);
            builder.Ignore(f => f.Resources);
        }
    }
}