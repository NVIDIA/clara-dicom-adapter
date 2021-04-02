using Ardalis.GuardClauses;
using Dicom;
using Microsoft.Extensions.Logging;
using Nvidia.Clara.DicomAdapter.Common;
using System;
using System.Collections.Generic;

namespace Nvidia.Clara.DicomAdapter.API
{
    public interface IJobMetadataBuilderFactory
    {
        JobMetadataBuilder Build(bool uploadMetadata, IReadOnlyList<string> dicomTags, IReadOnlyList<string> files);
    }

    public class JobMetadataBuilderFactory : IJobMetadataBuilderFactory
    {
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

            var dicomTagsToExtract = ConvertToDicomTagStack(dicomTags);

            foreach (var file in files)
            {
                var retryLater = new List<DicomTag>();

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

                while (dicomTagsToExtract.Count > 0)
                {
                    var dicomTag = dicomTagsToExtract.Pop();
                    try
                    {
                        if (_dicomToolkit.TryGetString(file, dicomTag, out var value))
                        {
                            metadata.Add($"{dicomTag.Group:X4}{dicomTag.Element:X4}", value);
                        }
                        else
                        {
                            retryLater.Add(dicomTag);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.Log(LogLevel.Error, ex, $"Error extracting metadata from file {file}, DICOM tag {dicomTag}.");
                        retryLater.Add(dicomTag);
                    }
                }

                if (retryLater.Count == 0)
                {
                    break;
                }

                dicomTagsToExtract = new Stack<DicomTag>(retryLater);
            }
            return metadata;
        }

        private Stack<DicomTag> ConvertToDicomTagStack(IReadOnlyList<string> metadata)
        {
            Guard.Against.Null(metadata, nameof(metadata));
            var stack = new Stack<DicomTag>();
            foreach (var tag in metadata)
            {
                // Validation already done in ConfigurationValidator.
                stack.Push(DicomTag.Parse(tag));
                _logger.Log(LogLevel.Debug, $"DICOM Tag added for metadata extraction: {tag}");
            }
            return stack;
        }
    }
}