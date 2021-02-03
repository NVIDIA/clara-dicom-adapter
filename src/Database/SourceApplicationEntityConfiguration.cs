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

using Microsoft.EntityFrameworkCore;
using Nvidia.Clara.DicomAdapter.API;

namespace Nvidia.Clara.DicomAdapter.Database
{
    internal class SourceApplicationEntityConfiguration : IEntityTypeConfiguration<SourceApplicationEntity>
    {
        public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<SourceApplicationEntity> builder)
        {
            builder.HasKey(j => j.AeTitle);

            builder.Property(j => j.HostIp).IsRequired();
        }
    }
}