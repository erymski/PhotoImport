using PhotoImport.Models;

namespace PhotoImport.Devices;

public record DeviceEntry(string ObjectId, string Path, string Name, bool IsDirectory);

public interface IPhotoDeviceProvider
{
    IReadOnlyList<DeviceInfo> Discover(CancellationToken cancellationToken);
    IPhotoDevice Open(DeviceInfo device, CancellationToken cancellationToken);
}

// Deliberately metadata-only. The probe cannot open content or mutate a phone.
public interface IPhotoDevice : IDisposable
{
    IReadOnlyList<DeviceEntry> GetStorageRoots(CancellationToken cancellationToken);
    IReadOnlyList<DeviceEntry> GetChildren(DeviceEntry directory, CancellationToken cancellationToken);
    PhotoMetadata ReadPhotoMetadata(DeviceEntry file, CancellationToken cancellationToken);
}

public class DeviceDisconnectedException(string message, Exception? innerException = null) : IOException(message, innerException);
