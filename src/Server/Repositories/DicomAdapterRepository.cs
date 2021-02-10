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
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.Extensions.DependencyInjection;
using Nvidia.Clara.DicomAdapter.Database;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.Server.Repositories
{
    public interface IDicomAdapterRepository<T> where T : class
    {
        IQueryable<T> AsQueryable();

        Task<T> FindAsync(params object[] keyValues);

        Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

        Task<List<T>> ToListAsync();

        EntityEntry<T> Update(T entity);

        EntityEntry<T> Remove(T entity);

        Task<EntityEntry<T>> AddAsync(T item, CancellationToken cancellationToken = default);

        T FirstOrDefault(Func<T, bool> p);
    }

    internal class DicomAdapterRepository<T> : IDicomAdapterRepository<T> where T : class
    {
        private readonly DicomAdapterContext _dicomAdapterContext;

        public DicomAdapterRepository(IServiceScopeFactory serviceScopeFactory)
        {
            if (serviceScopeFactory is null)
            {
                throw new ArgumentNullException(nameof(serviceScopeFactory));
            }

            _dicomAdapterContext = serviceScopeFactory.CreateScope().ServiceProvider.GetRequiredService<DicomAdapterContext>();
        }

        public IQueryable<T> AsQueryable()
        {
            return _dicomAdapterContext.Set<T>().AsQueryable();
        }

        public async Task<List<T>> ToListAsync()
        {
            return await _dicomAdapterContext.Set<T>().ToListAsync();
        }

        public async Task<T> FindAsync(params object[] keyValues)
        {
            Guard.Against.Null(keyValues, nameof(keyValues));
            
            return await _dicomAdapterContext.FindAsync<T>(keyValues);
        }

        public EntityEntry<T> Update(T entity)
        {
            Guard.Against.Null(entity, nameof(entity));

            return _dicomAdapterContext.Update<T>(entity);
        }

        public EntityEntry<T> Remove(T entity)
        {
            Guard.Against.Null(entity, nameof(entity));
            
            return _dicomAdapterContext.Remove<T>(entity);
        }

        public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        {
            return await _dicomAdapterContext.SaveChangesAsync(cancellationToken);
        }

        public async Task<EntityEntry<T>> AddAsync(T item, CancellationToken cancellationToken = default)
        {
            Guard.Against.Null(item, nameof(item));
            
            return await _dicomAdapterContext.AddAsync(item, cancellationToken);
        }

        public T FirstOrDefault(Func<T, bool> func)
        {
            Guard.Against.Null(func, nameof(func));

            return _dicomAdapterContext.Set<T>().FirstOrDefault(func);
        }
    }
}