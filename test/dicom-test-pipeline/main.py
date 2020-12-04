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

import errno
import logging
import logging.config as logging_config
import os
import shutil
from pathlib import Path

import clara
import pydicom
from clara import Driver, Error, Payload

logger = logging.getLogger(__name__)


def setupLogging(
        default_path='logging_config.json',
        default_level=logging.INFO,
        env_key='LOG_CFG'):
    """ Setup logging configuration """

    path = default_path
    value = os.getenv(env_key, None)
    if value:
        path = value
    if os.path.exists(path):
        with open(path, 'rt') as f:
            config = json.load(f)
            logging_config.dictConfig(config)
    else:
        logging.basicConfig(level=default_level)


def execute(driver, payload):
    input_dir = os.environ.get(
        'NVIDIA_CLARA_INPUTPATHS', '/input').split(":")[1]
    output_dir = os.environ.get(
        'NVIDIA_CLARA_OUTPUTPATHS', '/input').split(":")[1]
    logger.info("Files in {}: {}".format(
        input_dir, [f.name for f in os.scandir(input_dir) if f.is_file()]))

    logger.info('Scanning input directory {}'.format(input_dir))
    try:
        files = _get_all_files(input_dir)
    except Exception as ex:
        logger.info('Failed to list files: {}'.format(ex))

    invalid_dicom_files_count = 0
    for file in files:
        try:
            pydicom.dcmread(file)
        except Exception as ex:
            invalid_dicom_files_count += 1

    logger.info("Copying DICOM from {} to {}".format(input_dir, output_dir))
    try:
        for item in os.listdir(input_dir):
            src = os.path.join(input_dir, item)
            dst = os.path.join(output_dir, item)
            if os.path.isdir(src):
                shutil.copytree(src, dst, symlinks, ignore)
            else:
                shutil.copy2(src, dst)
    except Exception as ex:
        logger.error('Failed to copy files: {}'.format(ex))

    logger.info("Files in {}: {}".format(
        output_dir, [f.name for f in os.scandir(output_dir) if f.is_file()]))

    logger.info('Scanned {} files with {} non-DICOM part-10 file(s).'.format(len(files),
                                                                             invalid_dicom_files_count))

    if invalid_dicom_files_count > 0:
        raise Exception('{} invalid DICOM part-10 file(s) found'.format(invalid_dicom_files_count))


def _get_all_files(input_dir):
    files = []
    for filename in Path(input_dir).glob('**/*'):
        if filename.is_file():
            files.append(os.path.abspath(filename))
    return files


if __name__ == '__main__':
    app_name = 'dicom-test'
    try:
        setupLogging()
    except Exception as ex:
        logger.error('Logging did not set up successfully. {}'.format(ex))
        pass  # Best effort

    logger = logging.getLogger(__name__)
    logger.info('Program {} started.'.format(app_name))

    driver = Driver(execute_handler=execute)
    driver.start()
    driver.wait_for_completion()

    logger.info('Program {} exited.'.format(app_name))
