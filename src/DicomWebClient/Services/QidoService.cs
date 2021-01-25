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

using Ardalis.GuardClauses;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.Dicom.DicomWeb.Client.API;
using System;
using System.Collections.Generic;
using System.Net.Http;

namespace Nvidia.Clara.Dicom.DicomWeb.Client
{
    internal class QidoService : ServiceBase, IQidoService
    {
        public QidoService(HttpClient httpClient, ILogger logger = null)
            : base(httpClient, logger)
        {
        }

        /// <inheritdoc />
        public IAsyncEnumerable<T> SearchForStudies<T>()
            => SearchForStudies<T>(null);

        /// <inheritdoc />
        public IAsyncEnumerable<T> SearchForStudies<T>(IReadOnlyDictionary<string, string> queryParameters)
            => SearchForStudies<T>(queryParameters, null);

        /// <inheritdoc />
        public IAsyncEnumerable<T> SearchForStudies<T>(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude)
            => SearchForStudies<T>(queryParameters, fieldsToInclude, false);

        /// <inheritdoc />
        public IAsyncEnumerable<T> SearchForStudies<T>(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude, bool fuzzyMatching)
            => SearchForStudies<T>(queryParameters, fieldsToInclude, fuzzyMatching, 0);

        /// <inheritdoc />
        public IAsyncEnumerable<T> SearchForStudies<T>(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude, bool fuzzyMatching, int limit)
            => SearchForStudies<T>(queryParameters, fieldsToInclude, fuzzyMatching, limit, 0);

        /// <inheritdoc />
        public async IAsyncEnumerable<T> SearchForStudies<T>(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude, bool fuzzyMatching, int limit, int offset)
        {
            var studyUri = GetStudiesUri();

            var queries = new List<string>();

            AppendQueryParameters(queries, queryParameters);
            AppendAdditionalFields(queries, fieldsToInclude);
            AppendQueryOptions(queries, fuzzyMatching, limit, offset);

            var searchUri = new Uri($"{studyUri}{(queries.Count > 0 ? "?" : "")}{string.Join('&', queries)}", UriKind.Relative);
            await foreach (var metadata in GetMetadata<T>(searchUri))
            {
                yield return metadata;
            }
        }

        private void AppendQueryOptions(List<string> queries, bool fuzzyMatching, int limit, int offset)
        {
            Guard.Against.Null(queries, nameof(queries));
            if (fuzzyMatching)
            {
                queries.Add("fuzzymatching=true");
            }

            if (limit > 0)
            {
                queries.Add($"limit={limit}");
            }

            if (offset > 0)
            {
                queries.Add($"offset={offset}");
            }
        }

        private void AppendAdditionalFields(List<string> queries, IReadOnlyList<string> fieldsToInclude)
        {
            Guard.Against.Null(queries, nameof(queries));

            if (fieldsToInclude == null || fieldsToInclude.Count == 0)
            {
                return;
            }

            foreach (var item in fieldsToInclude)
            {
                queries.Add($"includefield={item}");
            }
        }

        private void AppendQueryParameters(List<string> queries, IReadOnlyDictionary<string, string> queryParameters)
        {
            Guard.Against.Null(queries, nameof(queries));

            if (queryParameters == null || queryParameters.Count == 0)
            {
                return;
            }

            foreach (var key in queryParameters.Keys)
            {
                queries.Add($"{key}={queryParameters[key]}");
            }
        }
    }
}