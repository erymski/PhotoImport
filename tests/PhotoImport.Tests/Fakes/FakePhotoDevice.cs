using PhotoImport.Devices;
using PhotoImport.Models;

namespace PhotoImport.Tests.Fakes;

public sealed class FakePhotoDevice : IPhotoDevice
{
    public static DeviceInfo Info(string id = "phone", string name = "Phone") =>
        new(id, name, "Maker", "Model", "USB", new Dictionary<string, string?> { ["DeviceId"] = id });
    public static DeviceEntry Entry(string id, string path, bool directory = false) =>
        new(id, path, path.Split('\\').Last(), directory);
    public static PhotoMetadata Photo(DeviceEntry entry) => new(entry.ObjectId, null, entry.Path, entry.Name,
        42, "Image", "EXIF", TimestampObservation.Missing("dateAuthored"), TimestampObservation.Missing("dateCreated"),
        TimestampObservation.Missing("dateModified"), TimestampObservation.Missing("ptpCaptureDate"), []);

    public List<DeviceEntry> Roots { get; } = [];
    public Dictionary<string, IReadOnlyList<DeviceEntry>> Children { get; } = [];
    public Dictionary<string, PhotoMetadata> Metadata { get; } = [];
    public Dictionary<string, Exception> Failures { get; } = [];
    public Action<DeviceEntry>? OnRead { get; set; }
    public bool IsDisposed { get; private set; }
    public IReadOnlyList<DeviceEntry> GetStorageRoots(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Failures.TryGetValue("roots", out var error)) throw error;
        return Roots;
    }
    public IReadOnlyList<DeviceEntry> GetChildren(DeviceEntry directory, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Failures.TryGetValue(directory.ObjectId, out var error)) throw error;
        return Children.GetValueOrDefault(directory.ObjectId) ?? [];
    }
    public PhotoMetadata ReadPhotoMetadata(DeviceEntry file, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (Failures.TryGetValue(file.ObjectId, out var error)) throw error;
        OnRead?.Invoke(file);
        return Metadata.GetValueOrDefault(file.ObjectId) ?? Photo(file);
    }
    public void Dispose() => IsDisposed = true;
}
