# Setup

This section will walk you through setting up Clara DICOM Adapter, using the [Clara Deploy AI COVID-19 Classification Pipeline](https://ngc.nvidia.com/catalog/resources/nvidia:clara:clara_ai_covid19_pipeline) as an example.

## Install Clara DICOM Adapter Helm Chart

To install Clara DICOM Adapter Helm Chart, please follow these steps:

1. If the system doesn't already have Docker and Kubernetes installed, first run the [Bootstrap script](https://ngc.nvidia.com/catalog/resources/nvidia:clara:clara_bootstrap).
2. Install [Clara Deploy CLI](https://ngc.nvidia.com/catalog/resources/nvidia:clara:clara_cli).
3. Install Clara DICOM Adapter Helm Chart using the following command:

```bash
$ clara pull dicom
```

Next, we'll configure Clara DICOM Adapter.


## Configuring Clara DICOM Adapter

The DICOM Adapter configuration is stored as JSON in `~/.clara/charts/dicom-adapter/files/appsettings.json`.
The default settings enable DICOM *C-STORE SCP* and *C-STORE-SCU* and set listening on port `104`.  


### Default Settings (appsettings.json)

``` json
{
  "DicomAdapter": {
    "readAeTitlesFromCrd": true,
    "dicom": {
      "scp": {
        "port": 104,
        "log-dimse-datasets": false,
        "reject-unknown-sources": true
      },
      "scu": {
        "ae-title": "ClaraSCU",
        "log-dimse-datasets": false,
        "log-data-pdus": false
      }
    },
    "storage" : {
      "temporary" : "/payloads"
    }
  },
  "Serilog": {
    "Using": [
      "Serilog.Sinks.Console"
    ],
    "MinimumLevel": "Information",
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

Please refer to [Configuration Schema](schema.md) for complete reference.


## Starting Clara DICOM Adapter

Once you have configured Clara DICOM Adapter, run the following command to start the service:

```bash
clara dicom start
```

## Enable Incoming Associations

Before setting up a new *Clara AE Title*, you must first register the COVID-19 pipeline: Refer to the [COVID-19 Setup Guide](https://ngc.nvidia.com/catalog/resources/nvidia:clara:clara_ai_covid19_pipeline/setup) to register the pipeline.

Once you have registered the pipeline with *Clara Platform* and have the **Pipeline ID**, you can now setup a new *Clara AE Title*
to accept DICOM associations and store DICOM instances.

First, create a new *Clara AE Title*:

```bash
$ clara dicom create aetitle -a COVIDAET pipeline-covid=<PIPELINE-ID>
```

> [!Note]
> Per the DICOM standard, the length of the `ae-Title` value should not exceed 16 characters.

Next, create a DICOM Source to allow that DICOM device to communicate with DICOM Adapter:

```
$ clara dicom create -a MYPACS -i 10.20.30.1
```

Now you have a DICOM device with the AE Title `MYPACS` registered at IP address `10.20.30.1`, and *Clara DICOM Adapter* will now accept DICOM associations from this device.  

.. Note:: If you would like DICOM Adapter to accept any incoming DICOM association without verifying the source, you may set `DicomAdapter>dicom>scp>reject-unknown-sources` to `false` in the configuration file.

## Exporting Processed Results

If the pipeline (in this case, the COVID-19 pipeline) generates results in DICOM format and needs to export the results back to 
a DICOM device, e.g. PACS, you will need to create a DICOM destination:

```bash
$ clara dicom create dest -n MYPACS -a MYPACSAET -i 10.20.30.2 -p 1104 
```

With the command and arguments above, we have created a new DICOM Destination named `MYPACS` with AE Title `MYPACSAET` at IP address `10.20.30.2` and port  `1104`.  Please note that the name argument (`-n`) must match the arguments defined in the pipeline.  Please refer to [Register Results Operator](/sdk/Services/ResultsService/public/docs/README.md) for a complete reference.

## Summary

With all the steps completed above, Clara DICOM Adapter is now ready to receive DICOM associations with the AE Title **COVIDAET**. To summarize, you've done the following:

1. Install Clara DICOM Adapter Helm Chart
2. Register COVID-19 pipeline with Clara
3. Setup Clara AE Title to run the COVID-19 pipeline
4. Setup DICOM Source to allow incoming association requests
5. Setup DICOM Destination for exporting processed results


## Other Settings

### Helm Chart Storage Configuration

The DICOM Adapter helm chart by default creates a persistent volume claim with 50Gi on `/clara-io/clara-core/payloads`, which is mounted to `/payloads` inside the DICOM Adapter container.

To increase or decrease the size of the volume claim, find and modify the `volumeSize` property in `~/.clara/charts/dicom-adapter/values.yaml` and restart the DICOM Adapter.

