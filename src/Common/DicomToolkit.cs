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
using Dicom;

namespace Nvidia.Clara.DicomAdapter.Common
{
    public class DicomToolkit : IDicomToolkit
    {
        public bool HasValidHeader(string path)
        {
            Guard.Against.NullOrWhiteSpace(path, nameof(path));
            return DicomFile.HasValidHeader(path);
        }

        public DicomFile Open(string path)
        {
            Guard.Against.NullOrWhiteSpace(path, nameof(path));
            return DicomFile.Open(path);
        }

        public bool TryGetString(string path, DicomTag dicomTag, out string value)
        {
            Guard.Against.NullOrWhiteSpace(path, nameof(path));
            Guard.Against.Null(dicomTag, nameof(dicomTag));

            value = string.Empty;
            if (!DicomFile.HasValidHeader(path))
            {
                return false;
            }

            var file = Open(path);

            return TryGetString(file, dicomTag, out value);
        }

        public bool TryGetString(DicomFile file, DicomTag dicomTag, out string value)
        {
            Guard.Against.Null(file, nameof(file));
            Guard.Against.Null(dicomTag, nameof(dicomTag));

            value = string.Empty;
            if (!file.Dataset.Contains(dicomTag))
            {
                return false;
            }

            return file.Dataset.TryGetString(dicomTag, out value);
        }

        public void Save(DicomFile file, string path)
        {
            Guard.Against.Null(file, nameof(file));
            Guard.Against.NullOrWhiteSpace(path, nameof(path));
            file.Save(path);
        }
    }
}