# Configuration

## Helm Chart Configuration

Clara DICOM Adapter helm chart is stored in `~/.clara/charts/dicom-adapter/` if installed using Clara CLI. The following configuration files may be modified according to environment requirements:

### ~/.clara/charts/dicom-adapter/values.yaml

```yaml


dicomAdapter:
  dicomPort: 104 # default DICOM SCP listening port.
  apiPort: 5000 # default port for the web API.

storage:
  volumeSize: 50Gi # disk space allocated for DICOM Adapter used for temporarily storing of received DICOM instances.
  hostPath: "/clara-io" # host path mounted into /payloads in the container for storing temporary files.

database:
  volumeSize: 3Gi # disk space allocated for the DICOM Adapter database.  Default uses sqlite3 database.
  hostPath: "/clara-io/dicom-adapter" # host path mounted into /database for storing the sqlite3 database file.

```


### ~/.clara/charts/dicom-adapter/files/appsettings.json

```json
{
  "DicomAdapter": {
    "dicom": {
      "scp": {
        "port": 104, // DICOM SCP listening port. (default 104)
        "maximumNumberOfAssociations": 100, // maximum number of concurrent associations. (range: 1-1000, default: 1000)
        "verification": {
          "enabled": true, // respond to c-ECHO commands (default: true)
          "transferSyntaxes": [
            "1.2.840.10008.1.2.1", // Explicit VR Little Endian
            "1.2.840.10008.1.2" , // Implicit VR Little Endian
            "1.2.840.10008.1.2.2", // Explicit VR Big Endian
          ]
        }
        "logDimseDatasets": false, // whether or not to write command and dataset to log (default false)
        "rejectUnknownSources": true // whether to reject unknown sources not listed in the source section. (default true)
      },
      "scu": {
        "export": {
          "maximumRetries": 3, // number of retries the exporter shall perform before reporting failure to Results Service.
          "failureThreshold" 0.5, // failure threshold for a task to be marked as failure.
          "pollFrequencyMs": 500 // number of milliseconds each exporter shall poll tasks from Results Service,
        },
        "aeTitle": "ClaraSCU", // AE Title of the SCU service
        "logDimseDatasets": false,  // whether or not to write command and data datasets to the log.
        "logDataPDUs": false, // whether or not to write message to log for each P-Data-TF PDU sent or received
        "maximumNumberOfAssociations": 2, // maximum number of outbound DICOM associations (range: 1-1000, default: 2)
      }
    },
    "storage" : {
      "temporary" : "/payloads" // storage path used for storing received instances before uploading to Clara Platform.
    }
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft": "Warning",
      "Microsoft.Hosting.Lifetime": "Information",
      "Microsoft.AspNetCore.Mvc.Infrastructure.ControllerActionInvoker": "Error"
    }
  },
  "AllowedHosts": "*"
}
```

## Configuration Validation

Clara DICOM Adapter validates all settings during startup. Any provided values that are invalid
or missing may cause the service to crash. If you are the running the DICOM Adapter inside
Kubernetes/Helm, you may see the `CrashLoopBack` error.  To review the validation errors, simply
run `kubectl logs <name-of-dicom-adapter-pod>`.

