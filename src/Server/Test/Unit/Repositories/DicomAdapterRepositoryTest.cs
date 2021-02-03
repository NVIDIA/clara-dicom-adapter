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
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Nvidia.Clara.DicomAdapter.API;
using Nvidia.Clara.DicomAdapter.Database;
using Nvidia.Clara.DicomAdapter.Server.Repositories;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Nvidia.Clara.DicomAdapter.Test.Unit
{
    public class DicomAdapterRepositoryTest : IClassFixture<DatabaseFixture>
    {
        private readonly Mock<IServiceScopeFactory> _serviceScopeFactory;
        public DatabaseFixture Fixture { get; }

        public DicomAdapterRepositoryTest(DatabaseFixture fixture)
        {
            Fixture = fixture;

            var serviceProvider = new Mock<IServiceProvider>();
            serviceProvider
                .Setup(x => x.GetService(typeof(DicomAdapterContext)))
                .Returns(Fixture.DbContext);

            var scope = new Mock<IServiceScope>();
            scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

            _serviceScopeFactory = new Mock<IServiceScopeFactory>();
            _serviceScopeFactory.Setup(p => p.CreateScope()).Returns(scope.Object);

        }

        [Fact(DisplayName = "AsQueryable - returns IQueryable")]
        public void AsQueryable()
        {
            var repo = new DicomAdapterRepository<SourceApplicationEntity>(_serviceScopeFactory.Object);

            var result = repo.AsQueryable();

            Assert.True(result is IQueryable<SourceApplicationEntity>);
        }

        [Fact(DisplayName = "AsQueryable - returns List")]
        public async Task ToListAsync()
        {
            var repo = new DicomAdapterRepository<SourceApplicationEntity>(_serviceScopeFactory.Object);

            var result = await repo.ToListAsync();

            Assert.True(result is List<SourceApplicationEntity>);
        }

        [Fact(DisplayName = "FindAsync - lookup by key")]
        public async Task FindAsync()
        {
            var repo = new DicomAdapterRepository<SourceApplicationEntity>(_serviceScopeFactory.Object);

            var result = await repo.FindAsync("AET5");

            Assert.NotNull(result);
            Assert.Equal("AET5", result.AeTitle);
            Assert.Equal("5.5.5.5", result.HostIp);
        }

        [Fact(DisplayName = "Update")]
        public async Task Update()
        {
            var repo = new DicomAdapterRepository<SourceApplicationEntity>(_serviceScopeFactory.Object);

            var key = "AET2";
            var result = await repo.FindAsync(key);
            Assert.NotNull(result);

            result.HostIp = "20.20.20.20";
            repo.Update(result);
            await repo.SaveChangesAsync();
            var updated = await repo.FindAsync(key);

            Assert.Equal(result, updated);
        }

        [Fact(DisplayName = "Remove")]
        public async Task Remove()
        {
            var repo = new DicomAdapterRepository<SourceApplicationEntity>(_serviceScopeFactory.Object);

            for (int i = 8; i <= 10; i++)
            {
                var key = $"AET{i}";
                var result = await repo.FindAsync(key);
                repo.Remove(result);
                await repo.SaveChangesAsync();
            }
        }

        [Fact(DisplayName = "AddAsync")]
        public async Task AddAsync()
        {
            var repo = new DicomAdapterRepository<SourceApplicationEntity>(_serviceScopeFactory.Object);

            for (int i = 11; i <= 20; i++)
            {
                await repo.AddAsync(new SourceApplicationEntity
                {
                    AeTitle = $"AET{i}",
                    HostIp = $"Server{i}",
                });
            }
            await repo.SaveChangesAsync();

            for (int i = 11; i <= 20; i++)
            {
                var notNull = await repo.FindAsync($"AET{i}");
                Assert.NotNull(notNull);
            }
        }

        [Fact(DisplayName = "FirstOrDefault")]
        public void FirstOrDefault()
        {
            var repo = new DicomAdapterRepository<SourceApplicationEntity>(_serviceScopeFactory.Object);

            var exists = repo.FirstOrDefault(p => p.HostIp == "1.1.1.1");
            Assert.NotNull(exists);

            var doesNotexist = repo.FirstOrDefault(p => p.AeTitle == "ABC");
            Assert.Null(doesNotexist);
        }
    }

    public class DatabaseFixture : IDisposable
    {
        public DicomAdapterContext DbContext { get; }

        public DatabaseFixture()
        {
            DbContext = GetDatabaseContext();
        }

        public void Dispose()
        {
        }

        public DicomAdapterContext GetDatabaseContext()
        {
            var options = new DbContextOptionsBuilder<DicomAdapterContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            var databaseContext = new DicomAdapterContext(options);
            databaseContext.Database.EnsureDeleted();
            databaseContext.Database.EnsureCreated();
            if (databaseContext.SourceApplicationEntities.Count() <= 0)
            {
                for (int i = 1; i <= 10; i++)
                {
                    databaseContext.SourceApplicationEntities.Add(
                        new SourceApplicationEntity
                        {
                            AeTitle = $"AET{i}",
                            HostIp = $"{i}.{i}.{i}.{i}"
                        });
                }
            }
            databaseContext.SaveChanges();
            return databaseContext;
        }
    }
}