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

using System;
using System.Linq;

namespace Nvidia.Clara.DicomAdapter.Test.Shared
{
    public static class Extensions
    {
        public static void PrintStringArray(this string[] output, string message)
        {
            Console.WriteLine($"====== {message} =====");
            foreach (var line in output)
            {
                Console.WriteLine($"\t{line}");
            }
        }

        public static string[] Filter(this string[] output, string filter)
        {
            return output.Where(p => p.Contains(filter, StringComparison.InvariantCultureIgnoreCase)).ToArray();
        }
    }
}