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
using System.Linq;
using System.Text.RegularExpressions;

namespace Nvidia.Clara.DicomAdapter.Common
{
    public static class ExtensionMethods
    {
        public const int CLARA_JOB_NAME_MAX_LENGTH = 25;
        private static readonly Regex ValidJobNameRegex = new Regex("[^a-zA-Z0-9-]");

        /// <summary>
        /// Extension method for checking a IEnumerable<T> is null.
        /// </summary>
        /// <param name="enumerable"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>true if null; false otherwise.</returns>
        public static bool IsNull<T>(this IEnumerable<T> enumerable)
        {
            return (enumerable is null);
        }

        /// <summary>
        /// Extension method for checking a IEnumerable<T> is null or empty.
        /// </summary>
        /// <param name="enumerable"></param>
        /// <typeparam name="T"></typeparam>
        /// <returns>true if null or empty; false otherwise.</returns>
        public static bool IsNullOrEmpty<T>(this IEnumerable<T> enumerable)
        {
            if (enumerable is null)
            {
                return true;
            }

            if (enumerable is ICollection<T> collection)
            {
                return collection.Count == 0;
            }

            return !enumerable.Any();
        }

        /// <summary>
        /// Removes characters that cannot be used in file paths.
        /// </summary>
        /// <param name="input">string to be scanned</param>
        /// <returns><code>input</code> without invalid path characters.</returns>
        public static string RemoveInvalidPathChars(this string input)
        {
            if (string.IsNullOrWhiteSpace(input))
            {
                return input;
            }

            foreach (var c in System.IO.Path.GetInvalidPathChars())
            {
                input = input.Replace(c.ToString(), "");
            }
            return input;
        }

        /// <summary>
        /// Removes characters that cannot be used in file paths.
        /// </summary>
        /// <param name="input">string to be scanned</param>
        /// <returns><code>input</code> without invalid path characters.</returns>
        public static string FixJobName(this string input)
        {
            var jobName = ValidJobNameRegex.Replace(input, "-").TrimStart('-');

            while (jobName.IndexOf("--") != -1)
            {
                jobName = jobName.Replace("--", "-");
            }

            if (jobName.Length > CLARA_JOB_NAME_MAX_LENGTH)
            {
                jobName = jobName.Substring(0, CLARA_JOB_NAME_MAX_LENGTH);
            }
            return jobName.ToLowerInvariant();
        }
    }
}
