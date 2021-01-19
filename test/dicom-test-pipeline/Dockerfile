#!/bin/bash
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


FROM nvcr.io/nvidia/clara/python-base:0.7.2-2010.1

WORKDIR /home/clara

COPY . ./

# Install requirements
RUN pip install --no-cache-dir -r ./requirements.txt

ENTRYPOINT ["bash", "-c", "python -u main.py"]
