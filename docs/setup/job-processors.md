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

### Sample Snippet

```csharp
[ProcessorValidation(ValidatorType = typeof(CustomJobProcessorValidator))]
public class MyJobProcessor : JobProcessorBase
{
    public override string Name => "My Custom Job Processor";
    public override string AeTitle => _configuration.AeTitle;

    public override void HandleInstance(InstanceStorageInfo value)
    {
        if (value is null)
        {
            throw new ArgumentNullException(nameof(value));
        };
        if (!value.CalledAeTitle.Equals(_configuration.AeTitle))
        {
            throw new InstanceNotSupportedException(value);
        };

        // handle the instance here
    }

    protected override void Dispose(bool disposing)
    {
        // dispose any resource if needed
    }
}


public class CustomJobProcessorValidator : IJobProcessorValidator
{

    public void Validate(string aeTitle, Dictionary<string, string> processorSettings)
    {
        // validate all processor settings
        // throw if anything is invalid
        // optionally throw on keys/values that are not used
    }
}
```

`ProcessorValidation` attribute must be decorated for each derived class of `JobProcessorBase` and a job processor validator must be provided.  The validator must implement `IJobProcessorValidator` inteface and validate any processor settings passed into the Clara Create AE Title API call, including the CLI.
