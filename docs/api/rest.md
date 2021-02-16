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

Response Content Type: JSON - [InferenceRequestResponse](xref:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestResponse).

| Code | Description                                                  |
| ---- | ------------------------------------------------------------ |
| 200  | Inference request received and scheduled for processing.     |
| 422  | Request contains invalid data or is missing required fields. |
| 500  | Server error.                                                |

---

## GET /api/inference/status/{id}

Retrieves status of an inference request.

### Parameters

The transaction ID or the Clara Job ID must be provided as part of the request URI.

### Responses

Response Content Type: JSON - [InferenceStatusResponse](xref:Nvidia.Clara.DicomAdapter.API.Rest.InferenceStatusResponse).

| Code | Description                            |
| ---- | -------------------------------------- |
| 200  | Inference request status is available. |
| 404  | Inference request cannot be found.     |
| 500  | Server error.                          |


---

## GET /api/config/claraaetitle

Retrieves a list of Clara AE Titles.

### Parameters

N/A

### Responses

Response Content Type: JSON - Array of [ClaraApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.ClaraApplicationEntity).

| Code | Description                       |
| ---- | --------------------------------- |
| 200  | AE Titles retrieved successfully. |
| 500  | Server error.                     |

### Example Request

```bash
curl --location --request GET 'http://localhost:5000/api/config/claraaetitle'
```

---

## GET /api/config/claraaetitle/{ae-title}

Retrieves the named Clara AE Title.

### Parameters

| Name     | Type   | Description                   |
| -------- | ------ | ----------------------------- |
| ae-title | string | the AE Title to be retrieved. |

### Responses

Response Content Type: JSON - [ClaraApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.ClaraApplicationEntity).

| Code | Description                       |
| ---- | --------------------------------- |
| 200  | AE Titles retrieved successfully. |
| 404  | AE Titles not found.              |
| 500  | Server error.                     |

### Example Request

```bash
curl --location --request GET 'http://localhost:5000/api/config/claraaetitle/my-brain-aet'
```

---

## POST /api/config/claraaetitle

Creates a new Clara AE Title.

### Parameters

Please see the [ClaraApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.ClaraApplicationEntity)
class definition for details.

### Responses

Response Content Type: JSON - [ClaraApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API).

| Code | Description                   |
| ---- | ----------------------------- |
| 201  | AE Title crated successfully. |
| 400  | Validation error.             |
| 500  | Server error.                 |

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

## DELETE /api/config/sourceaetitle/{ae-title}

Deletes a Source AE Title.

### Parameters

| Name | Type   | Description                   |
| ---- | ------ | ----------------------------- |
| name | string | the AE Title to be retrieved. |

### Responses

Response Content Type: JSON - [SourceApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.SourceApplicationEntity).

| Code | Description         |
| ---- | ------------------- |
| 200  | AE Title deleted.   |
| 404  | AE Title not found. |
| 500  | Server error.       |

### Example Request

```bash
curl --location --request DELETE 'http://localhost:5000/api/config/sourceaetitle/pacs'
```

---

## GET /api/config/sourceaetitle

Retrieves a list of Source AE Titles.

### Parameters

N/A

### Responses

Response Content Type: JSON - Array of [SourceApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.SourceApplicationEntity).

| Code | Description                       |
| ---- | --------------------------------- |
| 200  | AE Titles retrieved successfully. |
| 500  | Server error.                     |

### Example Request

```bash
curl --location --request GET 'http://localhost:5000/api/config/sourceaetitle'
```

---

## GET /api/config/destinationaetitle

Retrieves a list of Destination AE Titles.

### Parameters

N/A

### Responses

Response Content Type: JSON - Array of [DestinationApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.DestinationApplicationEntity).

| Code | Description                       |
| ---- | --------------------------------- |
| 200  | AE Titles retrieved successfully. |
| 500  | Server error.                     |

### Example Request

```bash
curl --location --request GET 'http://localhost:5000/api/config/destinationaetitle'
```

---

## GET /api/config/sourceaetitle/{ae-title}

Retrieves the named source AE Title.

### Parameters

| Name     | Type   | Description                   |
| -------- | ------ | ----------------------------- |
| ae-title | string | the AE Title to be retrieved. |

### Responses

Response Content Type: JSON - [SourceApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.SourceApplicationEntity).

| Code | Description                       |
| ---- | --------------------------------- |
| 200  | AE Titles retrieved successfully. |
| 404  | AE Titles not found.              |
| 500  | Server error.                     |

### Example Request

```bash
curl --location --request GET 'http://localhost:5000/api/config/sourceaetitle/pacs'
```

---

## GET /api/config/destinationaetitle/{name}

Retrieves the named destination AE Title.

### Parameters

| Name | Type   | Description                         |
| ---- | ------ | ----------------------------------- |
| name | string | the named AE Title to be retrieved. |

### Responses

Response Content Type: JSON - [DestinationApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.DestinationApplicationEntity).

| Code | Description                       |
| ---- | --------------------------------- |
| 200  | AE Titles retrieved successfully. |
| 404  | AE Titles not found.              |
| 500  | Server error.                     |

### Example Request

```bash
curl --location --request GET 'http://localhost:5000/api/config/destinationaetitle/my-pacs'
```


---

## POST /api/config/sourceaetitle

Creates a new Source AE Title.

### Parameters

Please see the [SourceApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.SourceApplicationEntity)
class definition for details.

### Responses

Response Content Type: JSON - [SourceApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.SourceApplicationEntity).

| Code | Description                    |
| ---- | ------------------------------ |
| 201  | AE Title created successfully. |
| 400  | Validation error.              |
| 500  | Server error.                  |

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

Please see the [DestinationApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.DestinationApplicationEntity)
class definition for details.

### Responses

Response Content Type: JSON - [DestinationApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.DestinationApplicationEntity).

| Code | Description                    |
| ---- | ------------------------------ |
| 201  | AE Title created successfully. |
| 400  | Validation error.              |
| 500  | Server error.                  |

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

## DELETE /api/config/claraaetitle/{ae-title}

Deletes a Clara AE Title.

### Parameters

| Name     | Type   | Description                   |
| -------- | ------ | ----------------------------- |
| ae-title | string | the AE Title to be retrieved. |

### Responses

Response Content Type: JSON - [ClaraApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.ClaraApplicationEntity).

| Code | Description         |
| ---- | ------------------- |
| 200  | AE Title deleted.   |
| 404  | AE Title not found. |
| 500  | Server error.       |

### Example Request

```bash
curl --location --request DELETE 'http://localhost:5000/api/config/claraaetitle/clara-brain-tumor'
```

---

## DELETE /api/config/destinationaetitle/{name}

Deletes a Destination AE Title.

### Parameters

| Name | Type   | Description                         |
| ---- | ------ | ----------------------------------- |
| name | string | the named AE Title to be retrieved. |

### Responses

Response Content Type: JSON - [DestinationApplicationEntity](xref:Nvidia.Clara.DicomAdapter.API.DestinationApplicationEntity).

| Code | Description         |
| ---- | ------------------- |
| 200  | AE Title deleted.   |
| 404  | AE Title not found. |
| 500  | Server error.       |

### Example Request

```bash
curl --location --request DELETE 'http://localhost:5000/api/config/claraaetitle/dicom-router'
```

---

## GET /health/status

DICOM Adapter service status:

- Active DICOM DIMSE associations
- Internal service status

### Parameters

N/A

### Responses

Response Content Type: JSON - [HealthStatusResponse](xref:Nvidia.Clara.DicomAdapter.API.Rest.HealthStatusResponse).

| Code | Description          |
| ---- | -------------------- |
| 200  | Status is available. |
| 500  | Server error.        |

---

## GET /health/ready

## GET /health/live

DICOM Adapter service readiness and liveness.

### Parameters

N/A

### Responses

Response Content Type: string

- `Health`: All services are running.
- `Unhealthy`: One or more services have stopped or crashed.

| Code | Description           |
| ---- | --------------------- |
| 200  | Service is healthy.   |
| 509  | Service is unhealthy. |
