# Inference Request APIs

The Clara DICOM Adapter supports the following RESTful APIs on (default) port 5000.

## POST /api/inference

Triggers a new inference job using the specified DICOM dataset from the specified data sources.

> [!Warning]
> This API is a work in progress and may change between releases.

> [!Note]
> The inference API is extended based on the draft created by the ACR (American College of Radiology).
> Please refer to [ACR's Platform-Model Communication for AI](https://www.acrdsi.org/-/media/DSI/Files/ACR-DSI-Model-API.pdf)
> for more information.

> [!IMPORTANT]
> For input and output connections that require credentials, please ensure that all the connections are secured and encrypted.

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

