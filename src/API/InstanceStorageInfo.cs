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

using System.Collections.Generic;
using System.IO.Abstractions;
using Ardalis.GuardClauses;
using Dicom;
using Dicom.Network;
using Nvidia.Clara.DicomAdapter.Common;

namespace Nvidia.Clara.DicomAdapter.API
{
    /// <summary>
    /// Provides basic information for a DICOM instance and storage hierarchy/path.
    /// </summary>
    public class InstanceStorageInfo
    {
        /// <summary>
        /// Gets SOP Class UID of the DICOM instance.
        /// </summary>
        public string SopClassUid { get; }

        /// <summary>
        /// Gets Patient ID (0010,0020) of the DICOM instance.
        /// </summary>
        public string PatientId { get; }

        /// <summary>
        /// Gets Study Instance UID of the DICOM instance.
        /// </summary>
        public string StudyInstanceUid { get; }

        /// <summary>
        /// Gets Series Instance UID of the DICOM instance.
        /// </summary>
        public string SeriesInstanceUid { get; }

        /// <summary>
        /// Gets SOP Instance UID of the DICOM instance.
        /// </summary>
        public string SopInstanceUid { get; }

        /// <summary>
        /// Gets the name of the AE Title receiving the instance.
        /// </summary>
        public string CalledAeTitle { get; }

        /// <summary>
        /// Gets the root path to the storage location.
        /// </summary>
        public string StorageRootPath { get; }

        /// <summary>
        /// Gets the root path to the location of the AE storage.
        /// </summary>
        public string AeStoragePath { get; }

        /// <summary>
        /// Gets the path to the location for this patient ID in the AE Storage.
        /// </summary>
        public string PatientStoragePath { get; }

        /// <summary>
        /// Gets the path to the location for this Study in the AE Storage.
        /// </summary>
        public string StudyStoragePath { get; }

        /// <summary>
        /// Gets the path to the location for this Series in the AE Storage.
        /// </summary>
        public string SeriesStoragePath { get; }

        /// <summary>
        /// Gets the full path to the instance.
        /// </summary>
        public string InstanceStorageFullPath { get; }

        /// <summary>
        /// Static method to create an instance of <code>InstanceStorageInfo</code>.
        /// </summary>
        /// <param name="request">Instance of <code>DicomCStoreRequest</code>.</param>
        /// <param name="storageRootFullPath">Root path to the storage location.</param>
        /// <param name="calledAeTitle">The calling AE title where the instance was sent from.</param>
        /// <param name="iFilesystem">An (optional) instance of IFileSystem from System.IO.Abstractions</param>
        /// <returns></returns>
        public static InstanceStorageInfo CreateInstanceStorageInfo(DicomCStoreRequest request, string storageRootFullPath, string calledAeTitle, IFileSystem iFilesystem = null)
        {
            return new InstanceStorageInfo(request, storageRootFullPath, calledAeTitle, iFilesystem ?? new FileSystem());
        }

        private InstanceStorageInfo(DicomCStoreRequest request, string storageRootFullPath, string calledAeTitle, IFileSystem fileSystem)
        {
            Guard.Against.Null(request, nameof(request));
            Guard.Against.NullOrWhiteSpace(storageRootFullPath, nameof(storageRootFullPath));
            Guard.Against.NullOrWhiteSpace(calledAeTitle, nameof(calledAeTitle));

            StorageRootPath = storageRootFullPath;
            CalledAeTitle = calledAeTitle;

            var temp = string.Empty;
            var missingTags = new List<DicomTag>();
            if (!request.Dataset.TryGetSingleValue(DicomTag.PatientID, out temp))
            {
                missingTags.Add(DicomTag.PatientID);
            }
            else
            {
                PatientId = temp;
            }

            if (!request.Dataset.TryGetSingleValue(DicomTag.StudyInstanceUID, out temp))
            {
                missingTags.Add(DicomTag.StudyInstanceUID);
            }
            else
            {
                StudyInstanceUid = temp;
            }
            if (!request.Dataset.TryGetSingleValue(DicomTag.SeriesInstanceUID, out temp))
            {
                missingTags.Add(DicomTag.SeriesInstanceUID);
            }
            else
            {
                SeriesInstanceUid = temp;
            }

            if (missingTags.Count != 0)
            {
                throw new MissingRequiredTagException(missingTags.ToArray());
            }

            SopClassUid = request.SOPClassUID.UID;
            SopInstanceUid = request.SOPInstanceUID.UID;

            AeStoragePath = fileSystem.Path.Combine(StorageRootPath, calledAeTitle.RemoveInvalidPathChars());
            PatientStoragePath = fileSystem.Path.Combine(AeStoragePath, PatientId.RemoveInvalidPathChars());

            StudyStoragePath = fileSystem.Path.Combine(PatientStoragePath, StudyInstanceUid.RemoveInvalidPathChars());
            SeriesStoragePath = fileSystem.Path.Combine(StudyStoragePath, SeriesInstanceUid.RemoveInvalidPathChars());

            fileSystem.Directory.CreateDirectoryIfNotExists(SeriesStoragePath);

            InstanceStorageFullPath = fileSystem.Path.Combine(SeriesStoragePath, SopInstanceUid.RemoveInvalidPathChars()) + ".dcm";
        }
    }
}
