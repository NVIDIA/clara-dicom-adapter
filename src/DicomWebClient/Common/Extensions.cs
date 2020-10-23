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
using System.Collections.Generic;
using System.Text;

namespace Nvidia.Clara.Dicom.DicomWeb.Client.Common
{
    public static class Extensions
    {
        /// <summary>
        /// Trim() removes null values from the input array.
        /// </summary>
        /// <typeparam name="T">Any data type.</typeparam>
        /// <param name="input">Array to be trimmed.</param>
        /// <returns></returns>
        public static T[] Trim<T>(this T[] input)
        {
            if(input is null)
            {
                return input;
            }

            var list = new List<T>();
            foreach(var item in input)
            {
                if( item != null)
                {
                    list.Add(item);
                }
            }
            return list.ToArray();
        }
    }
}
