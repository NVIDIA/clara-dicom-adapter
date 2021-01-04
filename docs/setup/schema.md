# Configuration Schema

```json
{
  "DicomAdapter": {
    "readAeTitlesFromCrd": false, // indicates whether to read Clara AE Titles, DICOM Sources and DICOM Destinations from Kubernetes CRD. (default: true),
    "crdReadIntervals": 10000, // indicates how often to update AE Titles from Kubernetes CRD in milliseconds.
    "dicom": {
      "scp": {
        "port": 104, // DICOM SCP listening port. (default 104)
        "aeTitles": [ // A list of AE Titles used to accept and associate DICOM instances when `readAeTitlesFromCrd` is set to `false`,
          {
            "name": "brain-tumor", // name of ae title
            "aeTitle": "Brain-Tumor-AE", // DICOM AE Title
            "overwriteSameInstance": false, // whether or not to overwrite existing instance with same SOP Instance UID 
            "ignoredSopClasses": [], // an array of strings containing SOP Class UIDs that is used to blacklist (ignore and not store) received instances. 
            "processor": "Nvidia.Clara.DicomAdapter.Server.Processors.AeTitleJobProcessor, Nvidia.Clara.DicomAdapter", // job process to attach to this AE Title (default: Nvidia.Clara.DicomAdapter.Server.Processors.AeTitleJobProcessor, Nvidia.Clara.DicomAdapter)
            "processorSettings": { // settings to be used by the configured Job Processor.  Please refer to Job Processors page for additional information.
              "timeout": 5,
              "pipeline-brain-tumor": "12345"
            }
          },
        ],
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
        "rejectUnknownSources": true, // whether to reject unknown sources not listed in the source section. (default true)
        "sources": [] // a list know know DICOM sources
      },
      "scu": {
        "aeTitle": "ClaraSCU", // AE Title of the SCU service
        "logDimseDatasets": false,  // whether or not to write command and data datasets to the log.
        "logDataPdus": false, // whether or not to write message to log for each P-Data-TF PDU sent or received
        "maximumNumberOfAssociations": 2, // maximum number of outbound DICOM associations (range: 1-1000, default: 2)
      }
    },
    "storage" : {
      "temporary" : "/payloads" // storage path used for storing received instances before uploading to Clara Platform.
    }
  },
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console"
    ],
    "MinimumLevel": "Information", // log filter level. (Available optioins: Debug, Information, Warning, Error)
    "WriteTo": [
      {
        "Name": "Console",
        "Args": {
          "theme": "Serilog.Sinks.SystemConsole.Themes.AnsiConsoleTheme::Code, Serilog.Sinks.Console",
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u4}] [{MachineName}] {SourceContext}[{ThreadId}] {Properties} {Message:l}{NewLine}{Exception}"
        }
      },
      {
        "Name": "File",
        "Args": {
          "path": "logs/clara-dicom.log",
          "rollingInterval": "Day",
          "rollOnFileSizeLimit": true,
          "outputTemplate": "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u4}] [{MachineName}] {SourceContext}[{ThreadId}] {Properties} {Message}{NewLine}{Exception}"
        }
      }
    ],
    "Enrich": [
      "FromLogContext",
      "WithMachineName",
      "WithThreadId"
    ]
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

Clara DICOM Adapter validates all settings during `start i[]`. Any provided values that are invalid
or missing may cause the service to crash. If you are the running the DICOM Adapter inside
Kubernetes/Helm, you may see the `CrashLoopBack` error.  To review the validation errors, simply
run `kubectl logs <name-of-dicom-adapter-pod>`.

