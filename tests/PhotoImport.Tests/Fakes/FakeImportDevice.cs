using PhotoImport.Devices;
using PhotoImport.Models;
namespace PhotoImport.Tests.Fakes;
public sealed class FakeImportDevice : IPhotoDevice, IMediaContentDevice
{
    public FakePhotoDevice Metadata { get; }=new();
    public Dictionary<string,byte[]> Contents {get;}=new();
    public List<string> Opened {get;}=[];
    public Func<string,PhotoReadHandle>? OnOpen {get;set;}
    public IReadOnlyList<DeviceEntry> GetStorageRoots(CancellationToken token)=>Metadata.GetStorageRoots(token);
    public IEnumerable<DeviceEntry> GetChildren(DeviceEntry directory,CancellationToken token)=>Metadata.GetChildren(directory,token);
    public PhotoMetadata ReadPhotoMetadata(DeviceEntry file,CancellationToken token)=>Metadata.ReadPhotoMetadata(file,token);
    public PhotoReadHandle OpenMediaRead(string path,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();Opened.Add(path);
        if(OnOpen is not null)return OnOpen(path);
        var source=Metadata.Metadata.Values.Single(m=>m.SourcePath==path);
        return new(new(source.SourcePath,source.SizeBytes,source.PersistentObjectId),new MemoryStream(Contents[path]));
    }
    public void Dispose()=>Metadata.Dispose();
}
