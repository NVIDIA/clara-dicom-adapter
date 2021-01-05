# Apache License, Version 2.0
# Copyright 2019-2020 NVIDIA Corporation
# 
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
# 
#     http://www.apache.org/licenses/LICENSE-2.0
# 
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

#!/bin/bash
SCRIPT_DIR=$(dirname "$(readlink -f "$0")")
TOP="$(git rev-parse --show-toplevel 2> /dev/null || readlink -f ${SCRIPT_DIR}/..)"

if [ -t 1 ] ; then
    terminal='-it'
fi

docker run $terminal -v $TOP:/clara mcr.microsoft.com/dotnet/core/sdk:3.1.201-bionic /bin/bash /clara/src/run-tests.sh "$@"