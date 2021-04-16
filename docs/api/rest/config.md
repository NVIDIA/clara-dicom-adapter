# Configuration APIs

The Clara DICOM Adapter supports the following RESTful APIs on (default) port 5000.

## GET /api/config/claraaetitle

Retrieves a list of Clara SCP Application Entities.

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

Retrieves details of the named Clara SCP AE Title.

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

Creates a new Clara SCP AE Title.

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

## DELETE /api/config/claraaetitle/{ae-title}

Deletes a Clara SCP AE Title.

### Parameters

| Name     | Type   | Description                 |
| -------- | ------ | --------------------------- |
| ae-title | string | the AE Title to be deleted. |

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

## GET /api/config/sourceaetitle

Retrieves a list of calling (Source) Application Entities.

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

## GET /api/config/sourceaetitle/{ae-title}

Retrieves details of the named DICOM calling (source) AE Title.

### Parameters

| Name     | Type   | Description                                 |
| -------- | ------ | ------------------------------------------- |
| ae-title | string | the details of an AE Title to be retrieved. |

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

## POST /api/config/sourceaetitle

Creates a new calling (Source) AE Title.

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

## DELETE /api/config/sourceaetitle/{ae-title}

Deletes a calling (Source) AE Title.

### Parameters

| Name | Type   | Description             |
| ---- | ------ | ----------------------- |
| name | string | the AE to be retrieved. |

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

## GET /api/config/destinationaetitle

Retrieves a list of Destination Application Entities.

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

### Job Names

The Inference API generates the job names using the following pattern:

```
{Transaction ID}-{Algorithm}-{UTC Time "yyyyMMddHHmmss"}
```

e.g.
Given:
* Transaction ID: ABC123
* Pipeline: b3c306293939461794f4fc5b16d3cb94
  
```
ABC123-b3c306293939461794f4fc5b16d3cb94-20211225101030
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
