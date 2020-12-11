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

FROM mcr.microsoft.com/dotnet/core/sdk:3.1-bionic as build

ARG Version
ARG FileVersion

WORKDIR /app

COPY . ./
RUN echo "Building DICOM Adapter $Version ($FileVersion)..."
RUN dotnet publish -c Release -o out --nologo /p:Version=$Version /p:FileVersion=$FileVersion src/Server/Nvidia.Clara.DicomAdapter.csproj


# Build runtime image
FROM mcr.microsoft.com/dotnet/core/runtime:3.1-bionic
WORKDIR /opt/nvidia/clara
COPY --from=build /app/out .

# RUN chmod 777 /opt/nvidia/clara

EXPOSE 104
EXPOSE 5000

RUN ls -lR /opt/nvidia/clara

ENTRYPOINT ["/opt/nvidia/clara/Nvidia.Clara.DicomAdapter"]
