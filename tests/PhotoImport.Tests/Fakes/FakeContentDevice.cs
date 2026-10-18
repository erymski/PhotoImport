using PhotoImport.Devices;
using PhotoImport.Models;

namespace PhotoImport.Tests.Fakes;

internal sealed class FakeContentDevice : IPhotoDevice, IPhotoContentDevice
{
    public FakePhotoDevice Metadata { get; set; } = new();
    public Dictionary<string,Func<PhotoReadHandle>> Content { get; } = new(StringComparer.Ordinal);
    public List<string> Opened { get; } = [];
    public Action<string>? OnOpen { get; set; }
    public bool Disposed { get; private set; }
    public Exception? DisposeError { get; set; }
    public PhotoReadHandle OpenPhotoRead(string path,CancellationToken token)
    { token.ThrowIfCancellationRequested(); Opened.Add(path); OnOpen?.Invoke(path); return Content[path](); }
    public IReadOnlyList<DeviceEntry> GetStorageRoots(CancellationToken token)=>Metadata.GetStorageRoots(token);
    public IEnumerable<DeviceEntry> GetChildren(DeviceEntry directory,CancellationToken token)=>Metadata.GetChildren(directory,token);
    public PhotoMetadata ReadPhotoMetadata(DeviceEntry file,CancellationToken token)=>Metadata.ReadPhotoMetadata(file,token);
    public void Dispose(){Disposed=true;Metadata.Dispose();if(DisposeError is not null)throw DisposeError;}
}
