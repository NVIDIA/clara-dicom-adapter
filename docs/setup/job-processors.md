# Job Processors

A *Job Processor* allows the user to configure how received DICOM instances will be associated with one or more configured Clara pipelines.
Each configured Clara SCP AE Title must have one `processor` configured; if omitted, the default *AE Title Job Processor* is used.

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

