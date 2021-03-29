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
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Newtonsoft.Json;
using Nvidia.Clara.DicomAdapter.API;
using System.Collections.Generic;

namespace Nvidia.Clara.DicomAdapter.Database
{
    internal class ClaraApplicationEntityConfiguration : IEntityTypeConfiguration<ClaraApplicationEntity>
    {
        public void Configure(EntityTypeBuilder<ClaraApplicationEntity> builder)
        {
            var jsonSeriealizerSettings = new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore };
            
            builder.HasKey(j => j.Name);
            
            builder.Property(j => j.AeTitle).IsRequired();

            builder.Property(j => j.OverwriteSameInstance).IsRequired().HasDefaultValue(false);

            builder.Property(j => j.Processor).IsRequired();

            builder.Property(j => j.IgnoredSopClasses).HasConversion(
                        v => JsonConvert.SerializeObject(v, jsonSeriealizerSettings),
                        v => JsonConvert.DeserializeObject<List<string>>(v, jsonSeriealizerSettings));

            builder.Property(j => j.ProcessorSettings).HasConversion(
                        v => JsonConvert.SerializeObject(v, jsonSeriealizerSettings),
                        v => JsonConvert.DeserializeObject<Dictionary<string, string>>(v, jsonSeriealizerSettings));
        }
    }
}