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
using System.Threading.Tasks;

namespace Nvidia.Clara.DicomAdapter.API
{
    public class PayloadFile
    {
        public string Name { get; set; }
        public byte[] Data { get; set; }
    }

    /// <summary>
    /// Interface wrapper for the Platform Payloads API
    /// </summary>
    public interface IPayloads
    {
        /// <summary>
        /// Downloads the specified payload
        /// </summary>
        Task<PayloadFile> Download(string payloadId, string path);

        /// <summary>
        /// Uploads file to the specified payload
        /// </summary>
        Task Upload(string payload, string basePath, IEnumerable<string> filePaths);
    }
}
