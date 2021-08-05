# Clara DICOM Adapter Helm Chart

This asset requires the Clara Deploy SDK. Follow the instructions on the
[Clara Ansible]($NGC_HOST/model-scripts/$NGC_ORG:$NGC_TEAM:clara_ansible) page
to install the Clara Deploy SDK.

## Install via Clara CLI

1. Download and install [Clara CLI]($NGC_HOST/model-scripts/$NGC_ORG:$NGC_TEAM:clara_cli).
2. Configure Clara CLI using the instructions in the (Quick Start Guide)[$NGC_HOST/resources/$NGC_ORG:$NGC_TEAM:clara_cli/quickStartGuide].
3. Run the following to install Clara DICOM Adapter:

    ```bash
    clara pull dicom
    ```

4. Run the following to start Clara Platform:

    ```bash
    clara dicom start
    ```

5. When finished, run the following to stop Clara Platform:

    ```bash
    clara dicom stop
    ```


## Setup

Please refer to [Clara DICOM Adapter User Guide](https://nvidia.github.io/clara-dicom-adapter/) for complete reference.

## License

An [End User License Agreement](https://developer.nvidia.com/nvidia-clara-sdk-license)
is included with the product. By pulling and using the Clara Deploy asset on NGC, you accept the
terms and conditions of these licenses.
