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

using Ardalis.GuardClauses;
using System;
using System.IO.Abstractions;

namespace Nvidia.Clara.DicomAdapter.Common
{
    public static class FileSystemExtensions
    {
        public static void CreateDirectoryIfNotExists(this IDirectory directory, string path)
        {
            Guard.Against.NullOrWhiteSpace(path, nameof(path));
            if (!directory.Exists(path))
            {
                directory.CreateDirectory(path);
            }
        }

        public static bool TryDelete(this IDirectory directory, string dirPath)
        {
            Guard.Against.NullOrWhiteSpace(dirPath, nameof(dirPath));
            try
            {
                directory.Delete(dirPath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public static bool TryGenerateDirectory(this IDirectory directory, string path, out string generatedPath)
        {
            Guard.Against.NullOrWhiteSpace(path, nameof(path));

            var tryCount = 0;
            generatedPath = string.Empty;
            do
            {
                generatedPath = $"{path}-{DateTime.UtcNow.Millisecond}";
                try
                {
                    directory.CreateDirectory(generatedPath);
                    return true;
                }
                catch
                {
                    if (++tryCount > 5)
                    {
                        return false;
                    }
                }
            } while (true);
        }

        public static string GetDicomStoragePath(this IPath path, string root)
        {
            Guard.Against.NullOrWhiteSpace(root, nameof(root));

            return path.Combine(root, "dcm");
        }

        public static string GetFhirStoragePath(this IPath path, string root)
        {
            Guard.Against.NullOrWhiteSpace(root, nameof(root));

            return path.Combine(root, "ehr");
        }
    }
}