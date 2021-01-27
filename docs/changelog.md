# Changelog

## 0.8.1

- :new: new: DICOMweb client for WADO (Web Access to DICOM Objects)/QIDO (Query based on ID for DICOM Objects)/STOW 
  (Store Over the Web) and a CLI is available in [DicomWebClient](https://github.com/NVIDIA/clara-dicom-adapter/tree/main/src/DicomWebClient).
- :new: new: New REST API to trigger a new inference request is now avilable based on the specs defined by the 
  American College of Radiology (ACR). Please refer to the API Documentation for more information.
- :warning: All derived classes of [JobProcessorBase](xref:Nvidia.Clara.DicomAdapter.API.JobProcessorBase) must
  be decorated with a [ProcessorValidationAttribute] (xref:Nvidia.Clara.DicomAdapter.API.ProcessorValidationAttribute) 
  attribute so its settings can be validated when the Create Clara AE Title is called (POST /api/config/ClaraAeTitle)
- :new: new: [New REST APIs](./api/rest.md):
  - `POST /api/inference`
  - `GET /api/inference/status/{id}`
  - `GET /health/ready`
  - `GET /health/live`
  - `GET /health/status`
## 0.7.0

- :new: new: DICOM Adapter now accepts concurrent associations per AE Title and has a new Job
  Processor extension that allows developers to extend and customize how to associate received DICOM
  instances with a pipeline job.
- :warning: breaking: The YAML-formatted configuration file has been replaced and consolidated into
  a single `appsettings.json` file.

## 0.6.0

- :new: new: The ability to configure *Clara AE-Title*s, _Sources_, and _Destinations_ via
  Kubernetes CRD has been added. This allows a user to add a new Clara AE-Title and
  associate it with a Clara Pipeline without restarting DICOM Adapter. DICOM sources and
  destinations can also be added via CRD.
- :no_entry: removed: `timeout-group` is no longer supported. This can be replaced with a custom
  plug-in if required. `timeout` can still accept multiple associations and associate all
  received DICOM instances with a Clara job.
