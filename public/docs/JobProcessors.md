# Job Processors

A *Job Processor* allows the user to configure how received DICOM instances will be associated with one or more configured Clara pipelines.
Each configured Clara SCP AE Title must have one `processor` configured; if omitted, the default *AE Title Job Processor* is used.

## AE Title Job Processor

**AE Title Job Processor** is the default *Job Processor* used if not specified in the configuration.

For example, this job processor can group received instances by Study Instance UID (0020,000D) so that all received instances with the same Study Instance UID are used to trigger the configured Clara Pipeline(s).

### Configuration

**AE Title Job Processor** allows the following parameters to be customized:

* `priority`: The priority of the job to use during creation of the Clara pipeline-job.  Allowed values are  `lower`, `normal`, `higher`, `immediate`. Please refer to Clara Platform for additional details on job priorities.
* `timeout`: The number of seconds to wait before creating a new Clara pipeline job (minimum 5 seconds).
* `pipeline-*`: Any settings that are prefixed with `pipeline-` tell the job processor that it's a Clara pipeline and a new Clara pipeline job shall be created with the received instance(s).

#### Examples

##### Scenario 1

```json
"priority: "normal",
"timeout": 60,
"pipeline-lung": "8abf244aff7647989d4f6b3987a85759",
"pipeline-heart": "c5f996a71e1d4959bd6a2c8cf7130f88",
"pipeline-breast": "eb48c784ef20425580db7d46a30829b2"

```
With the settings above, the job processor will wait for 60 seconds before composing three Clara pipeline jobs, one for lung, one for heart, and one for breast.

For example, if there are five studies for Patient X, and each of these studies were sent to DICOM Adapter in a separate DICOM association within 60 seconds, then three Clara pipeline jobs are created, each with all five studies.


##### Scenario 2

```json
"priority": "higher",
"pipeline-breast": "8abf244aff7647989d4f6b3987a85759"
```

In this scenario, the job processor will group received instances with `timeout` set to `5` seconds.

For example, if studies A, B, and C are all sent over in one DICOM association, and no additional instances are sent afterwards, then after five seconds, three *breast* pipeline jobs are created, one for each study.

As another example, there is one study with four instances, and all four instances are sent in a separate association: The 2nd instance is received at T1+4 (where T1 is the time when 1st instance was received); the 3rd is received at T2+3; and the last at T3+1. In this case, one Clara pipeline job will be created with all four instances.

## Extending JobProcessorBase

By extending `JobProcessorBase`, which is found in `Nvidia.Clara.Dicom.API.dll`, developers can customize their inference job submission workflow.

The following code snippet contains the properties and methods that are required
when implementing a Job Processor.

```csharp
public abstract class JobProcessorBase : IDisposable, IObserver<InstanceStorageInfo>
{
    public abstract string Name { get; }
    public abstract string AeTitle { get; }
    public abstract void HandleInstance(InstanceStorageInfo value);

}
```

* `Name`: The name for the job processor.
* `AeTitle`: The AE Title that the processor is attached to.
* `HandleInstance(...)`: Contains a detailed implementation of how a job processor handles received instances. 

To submit a job in the `HandleInstance(...)` method, developers can simply call `base.SubmitPipelineJob(...)`, giving the name of the
job, the pipeline ID that has been registered with Clara Platform, the priority, and the DICOM instances to be associated with the job.

Once job submission is completed, `RemoveInstances(...)` should be called with the instances so the DICOM files in the temporary storage can be cleaned up by the *Storage Space Reclaimer Service*.

### Example Usage: Cache Service

For example, you may extend `JobProcessorBase` and have `HandleInstance(...)` build up a internal database and cache the DICOM instances.  Then, you can provide an API (gRPC/REST) to compose a new job by specifying the studies to use, submit the job, and then cleanup the instances.

