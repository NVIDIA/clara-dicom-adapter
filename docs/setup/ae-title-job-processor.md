# AE Title Job Processor

The **AE Title Job Processor** is the default *Job Processor* used if not specified in the
configuration.

This job processor can group received instances by Study Instance UID (0020,000D) so that all
received instances with the same Study Instance UID are used to trigger the configured Clara
Pipeline(s).

## Configuration

The **AE Title Job Processor** allows the following parameters to be customized:

* `priority`: The priority of the job to use during creation of the Clara pipeline job. Allowed
  values are  `lower`, `normal`, `higher`, and `immediate`. Refer to the Clara Platform
  documentation for additional details on job priorities.
* `timeout`: The number of seconds to wait before creating a new Clara pipeline job
  (minimum 5 seconds).
* `pipeline-*`: Any settings that are prefixed with `pipeline-` tell the job processor that it is
  a Clara pipeline, and a new Clara pipeline job shall be created with the received instance(s).

### Examples

#### Scenario 1

```json
"priority": "normal",
"timeout": 60,
"pipeline-lung": "8abf244aff7647989d4f6b3987a85759",
"pipeline-heart": "c5f996a71e1d4959bd6a2c8cf7130f88",
"pipeline-breast": "eb48c784ef20425580db7d46a30829b2"

```
With the settings above, the job processor will wait for 60 seconds before composing three
Clara pipeline jobs: one for lung, one for heart, and one for breast.

For example, if there are five studies for Patient X, and each of these studies are sent to
the DICOM Adapter in a separate DICOM association within 60 seconds, then three Clara pipeline
jobs are created, each with all five studies.


#### Scenario 2

```json
"priority": "higher",
"pipeline-breast": "8abf244aff7647989d4f6b3987a85759"
```

In this scenario, the job processor will group received instances with `timeout` set to `5` seconds.

For example, if studies A, B, and C are all sent over in one DICOM association, and no additional
instances are sent afterwards, then after five seconds, three *breast* pipeline jobs are created,
one for each study.

As another example, there is one study with four instances, and all four instances are sent in a
separate association: The 2nd instance is received at T1+4 (where T1 is the time when the 1st
instance is received); the 3rd is received at T2+3; and the last at T3+1. In this case,
one Clara pipeline job will be created with all four instances.

