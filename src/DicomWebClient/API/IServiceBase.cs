namespace Nvidia.Clara.Dicom.DicomWeb.Client.API
{
    public interface IServiceBase
    {
        bool TryConfigureServiceUriPrefix(string uriPrefix);
    }
}