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

namespace Nvidia.Clara.DicomAdapter.DicomWeb.Client
{
    internal class QidoService : ServiceBase, IQidoService
    {
        public QidoService(HttpClient httpClient, Uri serviceUri, ILogger logger = null)
            : base(httpClient, serviceUri, logger)
        {
        }

        /// <inheritdoc />
        public IAsyncEnumerable<string> SearchForStudies()
            => SearchForStudies(null);

        /// <inheritdoc />
        public IAsyncEnumerable<string> SearchForStudies(IReadOnlyDictionary<string, string> queryParameters)
            => SearchForStudies(queryParameters, null);

        /// <inheritdoc />
        public IAsyncEnumerable<string> SearchForStudies(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude)
            => SearchForStudies(queryParameters, fieldsToInclude, false);

        /// <inheritdoc />
        public IAsyncEnumerable<string> SearchForStudies(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude, bool fuzzyMatching)
            => SearchForStudies(queryParameters, fieldsToInclude, fuzzyMatching, 0);

        /// <inheritdoc />
        public IAsyncEnumerable<string> SearchForStudies(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude, bool fuzzyMatching, int limit)
            => SearchForStudies(queryParameters, fieldsToInclude, fuzzyMatching, limit, 0);

        /// <inheritdoc />
        public async IAsyncEnumerable<string> SearchForStudies(IReadOnlyDictionary<string, string> queryParameters, IReadOnlyList<string> fieldsToInclude, bool fuzzyMatching, int limit, int offset)
        {
            var studyUri = GetStudiesUri();

            var uriBuilder = new UriBuilder(studyUri);

            AppendQueryParameters(uriBuilder, queryParameters);
            AppendAdditionalFields(uriBuilder, fieldsToInclude);
            AppendQueryOptions(uriBuilder, fuzzyMatching, limit, offset);

            await foreach (var metadata in GetMetadata<string>(uriBuilder.Uri))
            {
                yield return metadata;
            }
        }

        private void AppendQueryOptions(UriBuilder uriBuilder, bool fuzzyMatching, int limit, int offset)
        {
            Guard.Against.Null(uriBuilder, nameof(uriBuilder));
            AppendAmpersandIfNeeded(uriBuilder);
            if (fuzzyMatching)
            {
                uriBuilder.Query += "fuzzymatching=true&";
            }

            if (limit > 0)
            {
                uriBuilder.Query += $"limit={limit}&";
            }

            if (offset > 0)
            {
                uriBuilder.Query += $"offset={offset}&";
            }
        }

        private void AppendAdditionalFields(UriBuilder uriBuilder, IReadOnlyList<string> fieldsToInclude)
        {
            Guard.Against.Null(uriBuilder, nameof(uriBuilder));

            if (fieldsToInclude == null || fieldsToInclude.Count == 0)
            {
                return;
            }

            AppendAmpersandIfNeeded(uriBuilder);

            foreach (var item in fieldsToInclude)
            {
                uriBuilder.Query += $"includefield={item}&";
            }
        }

        private void AppendQueryParameters(UriBuilder uriBuilder, IReadOnlyDictionary<string, string> queryParameters)
        {
            Guard.Against.Null(uriBuilder, nameof(uriBuilder));

            if (queryParameters == null || queryParameters.Count == 0)
            {
                return;
            }

            foreach (var key in queryParameters.Keys)
            {
                uriBuilder.Query += $"{key}={queryParameters[key]}&";
            }
        }

        private void AppendAmpersandIfNeeded(UriBuilder uriBuilder)
        {
            Guard.Against.Null(uriBuilder, nameof(uriBuilder));
            if (!uriBuilder.Query.EndsWith("&"))
            {
                uriBuilder.Query += "&";
            }
        }

        private Uri GetStudiesUri(string studyInstanceUid = "")
        {
            return string.IsNullOrWhiteSpace(studyInstanceUid) ?
                new Uri(_serviceUri, "studies/") :
                new Uri(_serviceUri, $"studies/{studyInstanceUid}/");
        }
    }
}