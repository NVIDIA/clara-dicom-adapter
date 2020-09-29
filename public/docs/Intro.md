# Introduction

Designed for the Clara Deploy SDK, the Clara DICOM Adapter implements the
necessary DICOM services for interoperability between Clara and other medical
devices. The Clara DICOM Adapter allows you to send/receive DICOM objects
using standard DICOM protocols and interpret standard DICOM part-10 formats.

## Requirements

* Docker 18.09.2 or higher
* Helm v2.15.2
* Kubernetes v1.15.12


## Services

*Clara DICOM Adapter* contains the following services:

* **DICOM SCP Service**: For handling incoming DICOM associations; responsible for storing received instances and notifying the *Instance
Stored Notification Service* of each received DICOM instance.
* **DICOM SCU Service**: For exporting processed DICOM results to configured DICOM destinations.
* **Instance Stored Notification Service**: Designed with [.NET Observer Pattern](https://docs.microsoft.com/en-us/dotnet/standard/events/observer-design-pattern) to allow every Job Processor to be notified when a new instance is available.
* **Storage Space Reclaimer Service**: Responsible for cleaning up received DICOM instances from temporary storage once they are uploaded to Clara Platform.

### DICOM SCP Service

The *DICOM SCP Service* accepts standard DICOM C-ECHO and C-STORE commands. Please see the
**DICOM Interface** section for more information.

All received instances are saved immediately to the configured temporary storage location
(`DicomAdapter>storage>temporary` which is mapped to `/clara-io/clara-core/payloads/` by default on the host system) and then registered with the *Instance Stored Notification Service*.

Received DICOM instances are stored on disk as-is using the original transfer syntax described in
the **DICOM Interface** section. Users of the Clara Deploy SDK must handle the encoding/decoding
of the DICOM files in their container(s). See **Third Party Tools** for a list of DICOM toolkits
available for parsing, encoding, and decoding DICOM files.

### DICOM SCU Service

The *DICOM SCU Service*, which is part of the Clara DICOM Adapter, queries the *Clara Results Service*
for available tasks using the Clara SCU AET as the agent name (`DicomAdapter>dicom>scu>aeTitle`). Each retrieved
task contains a list of DICOM files to be exported to the configured DICOM devices. If more than 50%
of the files fails to be exported, the job is marked as failed and reported back to the *Results
Service*--it will be retried up to three times at a later time. 

.. Note:: DICOM instances are sent as-is; no codec conversion is done as part of the SCU process. 
          See the **DICOM Interface** section for more information.

### Instance Stored Notification Service

The *Instance Stored Notification Service* is designed to allow *Job Processors* to subscribe to DICOM Receive & Store events. This allows developers to extend or customize the processing logic without worrying about
where and how to store DICOM instances.

.. Note:: DICOM instances that are not handled by the associated *Job Processor* are sent to the *Storage Space Reclaimer Service* immediately for cleanup.

### Storage Space Reclaimer Service

The *Storage Space Reclaimer Service* is responsible for removing stored DICOM instances from temporary storage after *Job Processor* has completed submission of a Clara pipeline job.
 

## Changelog

### 0.7.0
* ➕New: DICOM Adapter now accepts concurrent associations per AE Title and has a new Job Processor extension, designed
to allow developers to extend and customize how received DICOM instances can be associated with a pipeline job.
* ⚠Breaking: The YAML-formatted configuration file has been replaced and consolidated into a single `appsettings.json` file.



### 0.6.0

* ➕New: configure *Clara AE-Title*s, *Sources and *Destinations* via Kubernetes CRD is added which allows user to add a new Clara AE-Title and 
associate it with a Clara Pipeline without restarting DICOM Adapter.  DICOM sources and destination can also be added via CRD.
* ➖Removed: `timeout-group` is no longer supported.  This can be replaced by custom plug-in if required.  `timeout` is still supported
to accept multiple associations and associate al received DICOM instances with a Clara job.


## Third Party Tools

### DICOM Toolkits

* [fo-dicom](https://github.com/fo-dicom/fo-dicom) .NET
* [dcmtk](https://dicom.offis.de/dcmtk.php.en) C++
* [pydicom](https://github.com/pydicom/pydicom) Python
* [go-dicom](https://github.com/gillesdemey/go-dicom) Go

