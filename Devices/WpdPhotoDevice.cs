using MediaDevices;
using PhotoImport.Models;

namespace PhotoImport.Devices;

public sealed class WpdPhotoDevice : IPhotoDevice, IJpegContentDevice, IPhotoContentDevice
{
    private readonly MediaDevice device;
    private readonly Dictionary<string, MediaFileSystemInfo> objects = new(StringComparer.Ordinal);
    private bool disposed;

    internal WpdPhotoDevice(MediaDevice device) => this.device = device;

    public Stream OpenJpegRead(string sourcePath, CancellationToken token)
    {
        if (!JpegSourcePath.IsValid(sourcePath)) throw new ArgumentException("An absolute device JPEG path is required.", nameof(sourcePath));
        return OpenPhotoRead(sourcePath, token).Stream;
    }

    public PhotoReadHandle OpenPhotoRead(string sourcePath, CancellationToken token)
    {
        if (!PhotoSourcePath.IsValid(sourcePath)) throw new ArgumentException("An absolute supported device photo path is required.", nameof(sourcePath));
        return Read(() =>
        {
            var file = device.GetFileInfo(sourcePath);
            if (!string.Equals(file.FullName, sourcePath, StringComparison.OrdinalIgnoreCase))
                throw new IOException("The selected photo resolved to a different device path.");
            var size = file.Length;
            var source = new PhotoSourceInfo(file.FullName, size == 0 ? null : size, NullIfEmpty(file.PersistentUniqueId));
            return new PhotoReadHandle(source, new WpdReadStream(file.OpenRead(), token));
        }, token);
    }

    public IReadOnlyList<DeviceEntry> GetStorageRoots(CancellationToken cancellationToken) => Read(() =>
    {
        // Starting at DEVICE also covers nonstandard storage/photo layouts exposed by a driver.
        var root = device.GetRootDirectory();
        return (IReadOnlyList<DeviceEntry>)[Remember(root, true)];
    }, cancellationToken);

    public IEnumerable<DeviceEntry> GetChildren(DeviceEntry directory, CancellationToken cancellationToken)
    {
        var folder = Read(() => (MediaDirectoryInfo)objects[Key(directory)], cancellationToken);
        var iterator = Read(() => folder.EnumerateFileSystemInfos("*", SearchOption.TopDirectoryOnly).GetEnumerator(), cancellationToken);
        try
        {
            while (Read(iterator.MoveNext, cancellationToken))
            {
                var item = Read(() => iterator.Current, cancellationToken);
                yield return Read(() => Remember(item, item is MediaDirectoryInfo
                    || (item.Attributes & (MediaFileAttributes.Directory | MediaFileAttributes.Object)) != 0), cancellationToken);
            }
        }
        finally { WpdOperation.Run(iterator.Dispose, default); }
    }

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
        return WpdOperation.Run(operation, cancellationToken);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        try { WpdOperation.Run(device.Disconnect, default); }
        finally { objects.Clear(); WpdOperation.Run(device.Dispose, default); }
    }
}
