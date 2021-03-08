# DICOM Interface

The following reference describes the connectivity capabilities of Clara Deploy SDK out of the box.
Users implementing the Clara Deploy SDK must update their DICOM Conformance Statement according
to the actual capabilities of their application.

## DICOM SCP

Clara DICOM SCP implements C-ECHO and C-Store services to interface with other medical devices,
such as PACS. It allows users to define multiple AE Titles to enable DICOM communication. It then
maps each AE Title to a pipeline.

### DIMSE Services (SCP)

* **C-STORE**: Accepts incoming DICOM objects
* **C-ECHO**: Accepts incoming DICOM verification requests

### SOP Classes (Transfer) and Transfer Syntax Supported

Clara DICOM SCP accepts any proposed transfer syntaxes and stores any accepted instances as-is on
disk without any decoding support. Each AE Title may be configured to ignore and not save certain
SOP Classes.

### Association Policies

* Clara DICOM Storage SCP accepts associations but does not initiate associations.
* Clara DICOM Storage SCP accepts up to 1000 (default: 25) simultaneous associations across all configured AE Titles.
* Clara DICOM Storage SCP accepts associations when storage space usage is less than the configured watermark (default: 85% of the storage partition) and the available storage space is above the configured reserved storage size (default: 5GB of free space).
* Asynchronous mode is not supported. All operations are performed synchronously.
* The Implementation Class UID is "1.3.6.1.4.1.30071.8" and the Implementation Version Name is
  "fo-dicom 4.0.0".
* An association must be released properly for received instances to be associated with a pipeline.
  Files received from an aborted association or an interrupted connection are either removed
  immediately or removed based on a configured timeout value.

### Security Profiles

Clara DICOM Storage SCP does not conform to any defined DICOM Security Profiles. It is assumed that
the product is used within a secured environment that uses a firewall, router protection, VPN,
and/or other network security provisions.

The Clara DICOM Storage SCP service can be configured to check the following DICOM values when
determining whether to accept Association Open Requests:

* Calling AE Title
* Called AE Title

Clara SCP AE Title can be configured to accept Association Requests from only a limited list of
Calling AE Titles.

## DICOM SCU

The Clara DICOM Storage SCU provides the DICOM Storage Service for interfacing with other medical
devices such as PACS. It is executed at system startup and exists in a container using a single
configurable AE Title.

### DIMSE Services (SCU)

**C-STORE**: Sends processed results that are stored in DICOM format

The Clara DICOM Storage SCU initiates a push of DICOM objects to the Remote DICOM Storage SCP.
The system allows multiple remote SCPs to be configured.

### SOP Classes (Transfer) Supported and Transfer Syntax

The DICOM Store SCU service supports all SOP classes of the Storage Service Class. 
The DICOM Store SCU service transfers a DICOM object as-is using the stored Transfer Syntax,
without the support of compression, decompression, or Transfer Syntax conversion.

### Association Policies

* Clara DICOM Storage SCU initiates associations but does not accept associations.
* Clara DICOM Storage SCU allows two (configurable) SCU instances simultaneously.
* Asynchronous mode is not supported. All operations are performed synchronously.
* The Implementation Class UID is "1.3.6.1.4.1.30071.8" and the Implementation Version Name is 
  "fo-dicom 4.0.0".

### Security Profiles

Not applicable
