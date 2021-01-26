# REST APIs

The Clara DICOM Adapter supports the following RESTful APIs on (default) port 5000.

## POST /api/inference

Triggers a new inference job using the specified DICOM dataset from the specified data sources.

> [!Warning]
> This API is a work in progress and may change between releases.

> [!Note]
> The inference API is extended based on the draft created by the ACR (American College of Radiology).
> Please refer to [ACR's Platform-Model Communication for AI](https://www.acrdsi.org/-/media/DSI/Files/ACR-DSI-Model-API.pdf)
> for more information.

### Parameters

Please see the [InferenceRequest](xref:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequest) class
definition for examples.

Request Content Type: JSON

| Name            | Type                                                                                                | Description                                                                                                                                                                       |
| --------------- | --------------------------------------------------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| transactionID   | string                                                                                              | **Required**. User provided transaction ID for correlating an inference request.                                                                                                  |
| priority        | number                                                                                              | Valid range 0-255. Please refer to [Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequest.Priority](xref:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequest.Priority) for details. |
| inputMetadata   | [inputMetadata](xref:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestMetadata) object            | **Required**. Specifies the dataset associated with the inference request.                                                                                                        |
| inputResources  | array of [inputResource](xref:Nvidia.Clara.DicomAdapter.API.Rest.RequestInputDataResource) objects  | **Required**. Data sources where the specified dataset to be retrieved. **Clara Only** Must include one `interface` that is type of `Algorithm`.                                  |
| outputResources | array of [inputResource](xref:Nvidia.Clara.DicomAdapter.API.Rest.RequestOutputDataResource) objects | **Required**. Output destinations where results are exported to.                                                                                                                  |

### Responses

Returns [InferenceRequestResponse](xref:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestResponse).

| Code | Description                                                  |
| ---- | ------------------------------------------------------------ |
| 200  | Inference request received and scheduled for processing.     |
| 422  | Request contains invalid data or is missing required fields. |
| 500  | Server error.                                                |

---

## GET /api/inference/status/{id}

Retrieves status of an inference request.

### Parameters

THe transaction ID or the Clara Job ID must be provided as part of the request URI.

### Responses

Response Content Type: JSON

Returns Nvidia.Clara.Dicom.API.Rest.InferenceStatusResponse](xref:Nvidia.Clara.Dicom.API.Rest.InferenceStatusResponse).

| Code | Description                            |
| ---- | -------------------------------------- |
| 200  | Inference request status is available. |
| 404  | Inference request cannot be found.     |
| 500  | Server error.                          |

---

## GET /api/config/claraaetitle

## GET /api/config/sourceaetitle

## GET /api/config/destinationaetitle

Retrieves a list of (Clara|Source|Destination) AE Titles.

### Parameters

N/A

### Responses

Response Content Type: JSON

Returns a list of AE Titles (in Kubernetes CRD JSON format):

| Name       | Type   | Description                                                                                |
| ---------- | ------ | ------------------------------------------------------------------------------------------ |
| apiVersion | string | The CRD apiVersion                                                                         |
| items      | crd[]  | An array of CRDs                                                                           |
| kind       | string | The ClaraAeTitleList, SourceList, or DestinationList.                                      |
| metadata   | object | A unique ID representing the payload associated with the job where the results are stored. |

| Code   | Description                                |
| ------ | ------------------------------------------ |
| 200    | CRDs retrieved successfully.               |
| 500    | Server error                               |
| 503    | CRDs not enabled.                          |
| others | Other errors received from Kubernetes API. |

### Example Request

```bash
curl --location --request GET 'http://localhost:5000/api/config/destinationaetitle'
```

---

## POST /api/config/claraaetitle

Creates a new Clara AE Title.

### Parameters

Please see the [ClaraApplicationEntity](xref:Nvidia.Clara.DicomAdapter.Configuration.ClaraApplicationEntity)
class definition for details.

Required fields are listed below. Refer to the Schema section for complete list.

| Name                  | Type        | Description                                                                                                                                            |
| --------------------- | ----------- | ------------------------------------------------------------------------------------------------------------------------------------------------------ |
| name                  | string      | The name of the CRD                                                                                                                                    |
| aeTitle               | string      | The Clara AE Title                                                                                                                                     |
| overwriteSameInstance | bool        | Overwrite existing instance with same SOP Instance UID (default: false).                                                                               |
| ignoredSopClasses     | string[]    | An array of strings containing SOP Class UIDs that are used to add received instances to the denylist (ignore and not store)                           |
| processor             | string      | The job processor associated with the AE Title (default: "Nvidia.Clara.DicomAdapter.Server.Processors.AeTitleJobProcessor, Nvidia.Clara.DicomAdapter") |
| processorSettings     | JSON object | A JSON object containing key/value pairs of settings to be used for the Job Processor.                                                                 |

### Responses

Response Content Type: JSON

Returns a created CRD formatted in JSON.

| Name       | Type      | Description              |
| ---------- | --------- | ------------------------ |
| apiVersion | string    | The CRD apiVersion       |
| kind       | string    | The ClaraAeTitle         |
| spec       | Clara AET | The Clara AE Title specs |

| Code   | Description                               |
| ------ | ----------------------------------------- |
| 200    | CRD created successfully.                 |
| 500    | Server error                              |
| 503    | CRDs not enabled.                         |
| others | Other errors received from Kubernetes API |

### Example Request

```bash
curl --location --request POST 'http://localhost:5000/api/config/ClaraAeTitle/' \
--header 'Content-Type: application/json' \
--data-raw '{
    "name": "brain-tummor",
    "aeTitle": "BrainTumorModel",
    "overwriteSameInstance": true,
    "ignoredSopClasses": [
        "1.2.840.10008.5.1.4.1.1.7"
    ],
    "processorSettings": {
        "timeout": 5,
        "priority": "higher",
        "pipeline-brain-tumor": "7b9cda79ed834fdc87cd4169216c4011"
    }
}'
```

---

## POST /api/config/sourceaetitle

Creates a new Source AE Title.

### Parameters

Please see the [SourceApplicationEntity](xref:Nvidia.Clara.DicomAdapter.Configuration.SourceApplicationEntity)
class definition for details.

Required fields listed below. Refer to [Schema Section](~/setup/schema.md) for a complete list.

| Name    | Type   | Description                                      |
| ------- | ------ | ------------------------------------------------ |
| hostIp  | string | The Host name or IP address of the DICOM source. |
| aeTitle | string | The AE Title of the DICOM source.                |

### Responses

Response Content Type: JSON

Returns the created CRD formatted in JSON.

| Name       | Type      | Description          |
| ---------- | --------- | -------------------- |
| apiVersion | string    | The CRD apiVersion   |
| kind       | string    | Source               |
| spec       | Clara AET | Clara AE Title specs |

| Code   | Description                               |
| ------ | ----------------------------------------- |
| 200    | CRDs created successfully                 |
| 500    | Server error                              |
| 503    | CRDs not enabled                          |
| others | Other errors received from Kubernetes API |

### Example Request

```bash
curl --location --request POST 'http://localhost:5000/api/config/sourceaetitle' \
--header 'Content-Type: application/json' \
--data-raw '{
	"hostIp": "1.2.3.4",
	"aeTitle": "Orthanc"

}'
```

---

## POST /api/config/destinationaetitle

Creates a new Destination AE Title.

### Parameters

Please see the [DestinationApplicationEntity](xref:Nvidia.Clara.DicomAdapter.Configuration.DestinationApplicationEntity)
class definition for details.

Required fields are listed below. Refer to the **Schema** section for a complete list.

| Name    | Type   | Description                                                                  |
| ------- | ------ | ---------------------------------------------------------------------------- |
| name    | string | The name of the DICOM instance that can be referenced by the Results Service |
| hostIp  | string | The host name or IP address of the DICOM destination                         |
| aeTitle | string | The AE Title of the DICOM destination                                        |
| port    | int    | The Port of the DICOM destination                                            |

### Responses

Response Content Type: JSON

Returns the created CRD formatted in JSON.

| Name       | Type      | Description          |
| ---------- | --------- | -------------------- |
| apiVersion | string    | CRD apiVersion       |
| kind       | string    | Destination          |
| spec       | Clara AET | Clara AE Title specs |

| Code   | Description                                |
| ------ | ------------------------------------------ |
| 200    | CRDs created successfully.                 |
| 500    | Server error                               |
| 503    | CRDs not enabled.                          |
| others | Other errors received from Kubernetes API. |

### Example Request

```bash
curl --location --request POST 'http://localhost:5000config/destinationaetitle' \
--header 'Content-Type: application/json' \
--data-raw '{
	"name":"pacs",
	"hostIp": "10.20.30.40",
	"aeTitle": "ARCHIVEX",
	"port": 104

}'
```

---

## DELETE /api/config/claraaetitle/[name]

## DELETE /api/config/sourceaetitle/[name]

## DELETE /api/config/destinationaetitle/[name]

Deletes a (Clara|Source|Destination) AE Title.

### Parameters

| Name | Type   | Description                                                                                               |
| ---- | ------ | --------------------------------------------------------------------------------------------------------- |
| name | string | The `name` of the Kubernetes custom resource. _Note: name can be found in the metadata section of a CRD._ |

### Responses

Response Content Type: JSON

Returns the status of the deleted CRD.

| Name       | Type   | Description             |
| ---------- | ------ | ----------------------- |
| apiVersion | string | v1                      |
| status     | string | The status of the call. |
| kind       | string | Status                  |

| Code   | Description                                |
| ------ | ------------------------------------------ |
| 200    | CRDs deleted successfully.                 |
| 500    | Server error                               |
| 503    | CRDs not enabled                           |
| others | Other errors received from Kubernetes API. |

### Example Request

```bash
curl --location --request DELETE 'http://localhost:5000/api/config/claraaetitle/clara-brain-tumor'
```
