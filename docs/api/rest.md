# REST APIs

Clara DICOM Adapter supports the following RESTful APIs on (default) port 5000.


## POST /api/inference

Triggers a new inference job using the specified DICOM dataset from specified data sources.

> [!Warning]
> The API is work in progress and still in draft stage and may change in between releases.


> [!Note]
> The inference API is extended based on the draft created by the ACR (American College of Radiology).
> Please refer to [ACR's Platform-Model Communication for AI](https://www.acrdsi.org/-/media/DSI/Files/ACR-DSI-Model-API.pdf)
> for more information.

### Parameters

Please see [InferenceRequest](xref:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequest) class definition for examples.

Request Content Type: JSON

| Name | Type | Description |
| - | - | - | 
| transactionID | string | **Required**. User provided transaction ID for correlating an inference request. |
| priority | number | Valid range 0-255. Please refer to [Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequest.Priority](xref:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequest.Priority) for details. |
| inputMetadata | [inputMetadata](xref:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestMetadata) object | **Required**. Specifies the dataset associated with the inference request. |
| inputResources | array of [inputResource](xref:Nvidia.Clara.DicomAdapter.API.Rest.RequestInputDataResource) objects | **Required**. Data sources where the specified dataset to be retrieved. **Clara Only** Must include one `interface` that is type of `Algorithm`.

### Responses

Returns [InferenceRequestResponse](xref:Nvidia.Clara.DicomAdapter.API.Rest.InferenceRequestResponse).

| Code | Description |
| - | - |
| 200  | Inference request received and scheduled for processing. |
| 422  | Request contains invalid data or missing required fields. |
| 500  | Server error. |

## GET /api/config/claraaetitle
## GET /api/config/sourceaetitle
## GET /api/config/destinationaetitle

Retrieves list of (Clara|Source|Destination) AE Titles.

### Parameters

N/A

### Responses

Response Content Type: JSON


Returns list of AE Titles (in Kubernetes CRD JSON format):

| Name | Type | Description |
| - | - | - | 
| apiVersion | string | CRD apiVersion. |
| items | crd[] | An array of CRDs. |
| kind | string | ClaraAeTitleList, SourceList or DestinationList. | 
| metadata  | object | A unique ID representing the payload associated with the job where the results are stored.| 


| Code | Description |
| - | - |
| 200  | CRDs retrieved successfully. |
| 500  | Server error. |
| 503  | CRDs is not enabled. |
| others  | Other errors received from Kubernetes API. |

### Example Request

```bash
curl --location --request GET 'http://localhost:5000/api/config/destinationaetitle'
```


## POST /api/config/claraaetitle

Create a new Clara AE Title.

### Parameters

Please see [ClaraApplicationEntity](xref:Nvidia.Clara.DicomAdapter.Configuration.ClaraApplicationEntity) class definition for details.

Required fields listed below.  Refer to Schema section for complete list.


| Name | Type | Description |
| - | - | - | 
| name | string | Name of the CRD. |
| aeTitle | string | Clara AE Title. | 
| overwriteSameInstance | bool | Overwrite existing instance with same SOP Instance UID (default: false). | 
| ignoredSopClasses | string[] | An array of strings containing SOP Class UIDs that is used to add received instances to the denylist (ignore and not store). |
| processor | string | Job processor associated with the AE Title. (default: "Nvidia.Clara.DicomAdapter.Server.Processors.AeTitleJobProcessor, Nvidia.Clara.DicomAdapter") |
| processorSettings | JSON object | A JSON object containing key/value pairs of settings to be used for the Job Processor. |


### Responses

Response Content Type: JSON

Returns created CRD formatted in JSON.

| Name | Type | Description |
| - | - | - | 
| apiVersion | string | CRD apiVersion. |
| kind | string | ClaraAeTitle. | 
| spec | Clara AET | Clara AE Title specs. | 


| Code | Description |
| - | - |
| 200 | CRD created successfully. |
| 500 | Server error. |
| 503 | CRDs is not enabled. |
| others | Other errors received from Kubernetes API. |

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



## POST /api/config/sourceaetitle

Create a new Source AE Title.

### Parameters

Please see [SourceApplicationEntity](xref:Nvidia.Clara.DicomAdapter.Configuration.SourceApplicationEntity) class definition for details.

Required fields listed below. Refer to Schema section for complete list.


| Name | Type | Description |
| - | - | - | 
| hostIp | string | Host name or IP address of DICOM source. |
| aeTitle | string | AE Title of DICOM source. | 


### Responses

Response Content Type: JSON

Returns created CRD formatted in JSON.

| Name | Type | Description |
| - | - | - | 
| apiVersion | string | CRD apiVersion. |
| kind | string | Source. | 
| spec | Clara AET | Clara AE Title specs. | 


| Code | Description |
| - | - |
| 200 | CRDs created successfully . |
| 500 | Server error. |
| 503 | CRDs is not enabled. |
| others | Other errors received from Kubernetes API. |

### Example Request

```bash
curl --location --request POST 'http://localhost:5000/api/config/sourceaetitle' \
--header 'Content-Type: application/json' \
--data-raw '{
	"hostIp": "1.2.3.4",
	"aeTitle": "Orthanc"
	
}'
```


## POST /api/config/destinationaetitle

Create a new Destination AE Title.

### Parameters

Please see [DestinationApplicationEntity](xref:Nvidia.Clara.DicomAdapter.Configuration.DestinationApplicationEntity) class definition for details.

Required fields listed below. Refer to Schema section for complete list.


| Name | Type | Description |
| - | - | - | 
| name | string | Name of DICOM instance that can be referenced by Results Service. |
| hostIp | string | Host name or IP address of DICOM destination. |
| aeTitle | string | AE Title of DICOM destination. | 
| port | int | Port of DICOM destination. | 



### Responses

Response Content Type: JSON

Returns created CRD formatted in JSON.

| Name | Type | Description |
| - | - | - | 
| apiVersion | string | CRD apiVersion. |
| kind | string | Destination.  | 
| spec | Clara AET | Clara AE Title specs. | 


| Code | Description |
| - | - |
| 200  | CRDs created successfully. |
| 500  | Server error. |
| 503  | CRDs is not enabled. |
| others  | Other errors received from Kubernetes API. |

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



## DELETE /api/config/claraaetitle/[name]
## DELETE /api/config/sourceaetitle/[name]
## DELETE /api/config/destinationaetitle/[name]

Deletes a (Clara|Source|Destination) AE Title.

### Parameters

| Name | Type | Description                                                                                                                                                                       
| - | - | - | 
| name | string | `name` of the Kubernetes custom resource. *Note: name can be found in the metadata section of a CRD.* |

### Responses

Response Content Type: JSON

Returns status of the deleted CRD.

| Name | Type | Description |
| - | - | - | 
| apiVersion | string | v1. |
| status | string | Status of the call. |
| kind | string | Status. | 


| Code | Description |
| - | - |
| 200  | CRDs deleted successfully. |
| 500  | Server error. |
| 503  | CRDs is not enabled. |
| others  | Other errors received from Kubernetes API. |

### Example Request

```bash
curl --location --request DELETE 'http://localhost:5000/api/config/claraaetitle/clara-brain-tumor'
```
