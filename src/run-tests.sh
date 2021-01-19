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
RESULTS_DIR=$SCRIPT_DIR/results
VERBOSITY=normal


if [ $CI" = "true ]; then
    VERBOSITY=minimal
fi

if [ -f /.dockerenv ]; then
    echo "##### Installing apt packages..."
    apt-get update
    apt-get install -y dcmtk sudo
    git clean -fdx
fi

if [ ! -z ${CI} ]; then
    echo "##### Installing apt packages..."
    sudo apt-get update
    sudo apt-get install -y dcmtk
    sudo git clean -fdx
fi


if [ -d "$RESULTS_DIR" ]; then 
    rm -r "$RESULTS_DIR"
fi

mkdir -p "$RESULTS_DIR"

echo "##### Building DICOM Adapter..."
cd $TOP/src
dotnet build -r $runtime linux-x64 Nvidia.Clara.Dicom.sln


if [ $# -eq 0 ]
  then
    echo "Executing all tests"
    dotnet test -v=$VERBOSITY --runtime linux-x64 --results-directory "$RESULTS_DIR" --collect:"XPlat Code Coverage" --settings "$SCRIPT_DIR/coverlet.runsettings" Nvidia.Clara.Dicom.sln
    exit $?
else
    while test $# -gt 0
    do
        case "$1" in
            --unit) echo "##### Executing unit test..."
                dotnet test -v=$VERBOSITY --runtime linux-x64 --results-directory "$RESULTS_DIR" --collect:"XPlat Code Coverage" --settings "$SCRIPT_DIR/coverlet.runsettings" Nvidia.Clara.Dicom.Unit.sln
                exit $?
                ;;
            --integration) echo "##### Executing integration test..."
                dotnet test -v=$VERBOSITY --runtime linux-x64 --results-directory "$RESULTS_DIR" --collect:"XPlat Code Coverage" --settings "$SCRIPT_DIR/coverlet.runsettings" $SCRIPT_DIR/Server/Test/Integration/Nvidia.Clara.DicomAdapter.Test.Integration.csproj
                exit $?
                ;;
            --crd) echo "##### Executing integration with CRD test..."
                dotnet test -v=$VERBOSITY --runtime linux-x64 --results-directory "$RESULTS_DIR" --collect:"XPlat Code Coverage" --settings "$SCRIPT_DIR/coverlet.runsettings" $SCRIPT_DIR/Server/Test/IntegrationCrd/Nvidia.Clara.DicomAdapter.Test.IntegrationCrd.csproj
                exit $?
                ;;
            --*) echo "##### Bad option $1"
                ;;
            *) echo "##### Bad argument $1"
                ;;
        esac
        shift
    done
fi
exit 0