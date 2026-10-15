using MediaDevices;
using PhotoImport.Models;

namespace PhotoImport.Devices;

public sealed class WpdPhotoDevice : IPhotoDevice
{
    private readonly MediaDevice device;
    private readonly Dictionary<string, MediaFileSystemInfo> objects = new(StringComparer.Ordinal);
    private bool disposed;

    internal WpdPhotoDevice(MediaDevice device) => this.device = device;

    public IReadOnlyList<DeviceEntry> GetStorageRoots(CancellationToken cancellationToken) => Read(() =>
    {
        // Starting at DEVICE also covers nonstandard storage/photo layouts exposed by a driver.
        var root = device.GetRootDirectory();
        return (IReadOnlyList<DeviceEntry>)[Remember(root, true)];
    }, cancellationToken);

    public IReadOnlyList<DeviceEntry> GetChildren(DeviceEntry directory, CancellationToken cancellationToken) => Read(() =>
    {
        var folder = (MediaDirectoryInfo)objects[Key(directory)];
        var result = new List<DeviceEntry>();
        foreach (var item in folder.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(Remember(item, item is MediaDirectoryInfo || (item.Attributes & (MediaFileAttributes.Directory | MediaFileAttributes.Object)) != 0));
        }
        return (IReadOnlyList<DeviceEntry>)result;
    }, cancellationToken);

    public PhotoMetadata ReadPhotoMetadata(DeviceEntry file, CancellationToken cancellationToken) => Read(() =>
    {
        var item = objects[Key(file)];
        var warnings = new List<string>
        {
            "MediaDevices exposes projected dates only; raw timestamp representation and source timezone are unavailable.",
            "Reported content type, format, and separate PTP capture date are unavailable through this package API."
        };
        var size = item.Length;
        if (size == 0) warnings.Add("Reported size is zero or unavailable; byte size is unverified.");
        return new PhotoMetadata(file.ObjectId, NullIfEmpty(item.PersistentUniqueId), file.Path, file.Name,
            size == 0 ? null : size, null, null,
            Date("dateAuthored", () => item.DateAuthored), Date("dateCreated", () => item.CreationTime),
            Date("dateModified", () => item.LastWriteTime), TimestampObservation.Missing("ptpCaptureDate"), warnings);
    }, cancellationToken);

    private static TimestampObservation Date(string source, Func<DateTime?> read)
    {
        try { return WpdMetadataMapper.ObserveTimestamp(source, read(), false); }
        catch (Exception ex) when (ex is MediaDeviceException or NotSupportedException or UnauthorizedAccessException)
        { return WpdMetadataMapper.ObserveTimestamp(source, null, false, ex.Message); }
    }

    private DeviceEntry Remember(MediaFileSystemInfo item, bool directory)
    {
        var entry = new DeviceEntry(item.Id, item.FullName, item.Name, directory);
        objects[Key(entry)] = item;
        return entry;
    }

    private static string Key(DeviceEntry entry) => string.IsNullOrEmpty(entry.ObjectId) ? "path:" + entry.Path : "id:" + entry.ObjectId;
    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private T Read<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        cancellationToken.ThrowIfCancellationRequested();
        try { return operation(); }
        catch (NotConnectedException ex) { throw new DeviceDisconnectedException("The phone disconnected. Reconnect and unlock it, then retry.", ex); }
        catch (MediaDeviceException ex) { throw new IOException($"WPD metadata operation failed: {ex.Message}", ex); }
        catch (NotSupportedException ex) { throw new IOException($"WPD metadata operation is unsupported: {ex.Message}", ex); }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { device.Disconnect(); }
        finally { objects.Clear(); device.Dispose(); }
    }
}
