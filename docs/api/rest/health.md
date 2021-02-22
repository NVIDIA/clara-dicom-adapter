# Health APIs

The Clara DICOM Adapter supports the following RESTful APIs on (default) port 5000.

## GET /health/status

DICOM Adapter service status:

- Active DICOM DIMSE associations
- Internal service status

### Parameters

N/A

### Responses

Response Content Type: JSON - [HealthStatusResponse](xref:Nvidia.Clara.DicomAdapter.API.Rest.HealthStatusResponse).

| Code | Description                               |
| ---- | ----------------------------------------- |
| 200  | Status is available.                      |
| 500  | Server error.                             |

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
| 503  | Service is unhealthy. |
