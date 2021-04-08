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
using Dicom;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.Common;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Nvidia.Clara.DicomAdapter.API
{
    public interface IJobMetadataBuilderFactory
    {
        JobMetadataBuilder Build(bool uploadMetadata, IReadOnlyList<string> dicomTags, IReadOnlyList<string> files);
    }

    public class JobMetadataBuilderFactory : IJobMetadataBuilderFactory
    {
        private const int VALUE_LENGTH_LIMIT = 256;
        private readonly ILogger<JobMetadataBuilderFactory> _logger;
        private readonly IDicomToolkit _dicomToolkit;

        public JobMetadataBuilderFactory(ILogger<JobMetadataBuilderFactory> logger, IDicomToolkit dicomToolkit)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _dicomToolkit = dicomToolkit ?? throw new ArgumentNullException(nameof(dicomToolkit));
        }

        public JobMetadataBuilder Build(bool uploadMetadata, IReadOnlyList<string> dicomTags, IReadOnlyList<string> files)
        {
            Guard.Against.Null(files, nameof(files));

            var metadata = new JobMetadataBuilder();
            metadata.AddInstanceCount(files.Count);

            if (!uploadMetadata || dicomTags.IsNullOrEmpty())
            {
                return metadata;
            }
            var stopwatch = new Stopwatch();
            stopwatch.Start();
            var dicomTagsToExtract = ConvertToDicomTags(dicomTags);
            var uniqueBags = ExtractValues(dicomTagsToExtract, files);

            foreach (var tag in uniqueBags.Keys)
            {
                AddToMetadataDictionary(metadata, tag, uniqueBags[tag].ToList());
            }

            stopwatch.Stop();
            _logger.Log(LogLevel.Debug, $"Metadata built with {dicomTags.Count} DICOM tags and {files.Count} files in {stopwatch.ElapsedMilliseconds}ms.");
            return metadata;
        }

        private void AddToMetadataDictionary(JobMetadataBuilder metadata, DicomTag dicomTag, IList<string> values)
        {
            Guard.Against.Null(metadata, nameof(metadata));
            Guard.Against.Null(values, nameof(values));

            if (values.Count() == 0)
            {
                return;
            }
            else if (values.Count() == 1)
            {
                metadata.Add($"{dicomTag.Group:X4}{dicomTag.Element:X4}", values.First());
            }
            else
            {
                for (var i = 0; i < values.Count(); i++)
                {
                    metadata.Add($"{dicomTag.Group:X4}{dicomTag.Element:X4}-{i}", values.ElementAt(i));
                }
            }
        }

        private Dictionary<DicomTag, HashSet<string>> ExtractValues(List<DicomTag> dicomTagsToExtract, IReadOnlyList<string> files)
        {
            Guard.Against.Null(dicomTagsToExtract, nameof(dicomTagsToExtract));
            Guard.Against.Null(files, nameof(files));

            var bags = new Dictionary<DicomTag, HashSet<string>>();

            foreach (var dicomTag in dicomTagsToExtract)
            {
                if (!bags.ContainsKey(dicomTag))
                {
                    bags.Add(dicomTag, new HashSet<string>());
                }
            }

            foreach (var file in files)
            {
                try
                {
                    if (!_dicomToolkit.HasValidHeader(file))
                    {
                        continue;
                    }
                }
                catch (Exception ex)
                {
                    _logger.Log(LogLevel.Debug, ex, $"Error opening file {file}.");
                    continue;
                }

                var dicomFile = _dicomToolkit.Open(file);

                foreach (var dicomTag in dicomTagsToExtract)
                {
                    try
                    {
                        if (_dicomToolkit.TryGetString(dicomFile, dicomTag, out var value) &&
                            !string.IsNullOrWhiteSpace(value))
                        {
                            bags[dicomTag].Add(value.Substring(0, Math.Min(value.Length, VALUE_LENGTH_LIMIT)));
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, ex, $"Error extracting metadata from file {file}, DICOM tag {dicomTag}.");
                    }
                }
            }
            return bags;
        }

        private List<DicomTag> ConvertToDicomTags(IReadOnlyList<string> metadata)
        {
            Guard.Against.Null(metadata, nameof(metadata));

            var list = new List<DicomTag>();
            foreach (var tag in metadata)
            {
                // Validation already done in ConfigurationValidator.
                list.Add(DicomTag.Parse(tag));
                _logger.Log(LogLevel.Debug, $"DICOM Tag added for metadata extraction: {tag}");
            }
            return list;
        }
    }
}