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
using Microsoft.Extensions.DependencyInjection;
using Nvidia.Clara.DicomAdapter.Configuration;

namespace Nvidia.Clara.DicomAdapter.Server.Common
{
    public static class TypeExtensions
    {
        public static T CreateInstance<T>(this Type interfaceType, IServiceProvider serviceProvider, string typeString, params object[] parameters)
        {
            var type = Type.GetType(
                    typeString,
                    (name) =>
                    {
                        return AppDomain.CurrentDomain.GetAssemblies().Where(z => z.FullName.StartsWith(name.FullName)).FirstOrDefault();
                    },
                    null,
                    true);
            object processor = null;
            try
            {
                processor = ActivatorUtilities.CreateInstance(serviceProvider, type, parameters);
            }
            catch (System.Exception ex)
            {
                throw new ConfigurationException($"Failed to instantiate specified type '{typeString}'", ex);
            }

            if (interfaceType.IsAssignableFrom(type))
            {
                return (T)processor;
            }
            else
            {
                throw new ConfigurationException($"'{typeString}' must implement '{interfaceType.Name}' interface");
            }
        }
    }
}
