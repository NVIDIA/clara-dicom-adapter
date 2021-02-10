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
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.API.Rest;
using Nvidia.Clara.DicomAdapter.Configuration;

namespace Nvidia.Clara.DicomAdapter.Database
{
    /// <summary>
    /// Used to EF migration.
    /// </summary>
    public class DicomAdapterContextFactory : IDesignTimeDbContextFactory<DicomAdapterContext>
    {
        public DicomAdapterContext CreateDbContext(string[] args)
        {
            IConfigurationRoot configuration = new ConfigurationBuilder()
                .SetBasePath(System.IO.Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json")
                .Build();

            var builder = new DbContextOptionsBuilder<DicomAdapterContext>();

            var connectionString = configuration.GetConnectionString(DicomConfiguration.DatabaseConnectionStringKey);
            builder.UseSqlite(connectionString);

            return new DicomAdapterContext(builder.Options);
        }
    }

    public class DicomAdapterContext : DbContext
    {
        public DicomAdapterContext(DbContextOptions<DicomAdapterContext> options) : base(options)
        {
        }

        public virtual DbSet<ClaraApplicationEntity> ClaraApplicationEntities { get; set; }
        public virtual DbSet<SourceApplicationEntity> SourceApplicationEntities { get; set; }
        public virtual DbSet<DestinationApplicationEntity> DestinationApplicationEntities { get; set; }
        public virtual DbSet<InferenceRequest> InferenceRequests { get; set; }
        public virtual DbSet<InferenceJob> InferenceJobs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.ApplyConfiguration(new ClaraApplicationEntityConfiguration());
            modelBuilder.ApplyConfiguration(new SourceApplicationEntityConfiguration());
            modelBuilder.ApplyConfiguration(new DestinationApplicationEntityConfiguration());
            modelBuilder.ApplyConfiguration(new InferenceRequestConfiguration());
            modelBuilder.ApplyConfiguration(new InferenceJobConfiguration());
        }
    }
}