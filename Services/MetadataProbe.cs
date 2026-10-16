using System.Diagnostics;
using PhotoImport.Devices;
using PhotoImport.Models;

namespace PhotoImport.Services;

public static class MetadataProbe
{
    public static ProbeReport Run(IPhotoDevice device, DeviceInfo info, IReadOnlyList<DeviceEntry> roots, CancellationToken token) => RunCore(device,info,token,roots);
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".heic", ".heif", ".png", ".dng", ".tif", ".tiff" };

    public static ProbeReport Run(IPhotoDevice device, DeviceInfo deviceInfo, CancellationToken cancellationToken)
        => RunCore(device,deviceInfo,cancellationToken,null);

    private static ProbeReport RunCore(IPhotoDevice device, DeviceInfo deviceInfo, CancellationToken cancellationToken,IReadOnlyList<DeviceEntry>? scope)
    {
        var started = DateTimeOffset.UtcNow;
        var clock = Stopwatch.StartNew();
        var photos = new List<PhotoMetadata>(); var errors = new List<ProbeError>();
        var warnings = new List<string>(); var roots = new List<string>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<DeviceEntry>();
        var cancelled = false;
        try
        {
            foreach (var root in scope ?? device.GetStorageRoots(cancellationToken))
            {
                roots.Add(root.Path);
                stack.Push(root);
            }
            while (stack.TryPop(out var entry))
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!entry.IsDirectory) { InspectPhoto(entry); continue; }
                if (!Visit(entry)) continue;
                try
                {
                    if (entry.IsDirectory)
                    {
                        if (entry.Name.Equals(".thumbnails", StringComparison.OrdinalIgnoreCase))
                        {
                            warnings.Add($"Skipped thumbnail directory: {entry.Path}");
                            continue;
                        }
                        foreach (var child in device.GetChildren(entry, cancellationToken))
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            if(scope is not null && child.Path!=entry.Path && !DeviceFolderScope.IsDirectChild(child.Path,entry.Path))
                            { errors.Add(new("enumerate",child.ObjectId,child.Path,"Provider returned a child outside its parent folder.")); continue; }
                            if (child.IsDirectory) stack.Push(child);
                            else InspectPhoto(child);
                        }
                    }
                }
                catch (OperationCanceledException) { throw; }
                catch (DeviceDisconnectedException) { throw; }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
                {
                    errors.Add(new(entry.IsDirectory ? "enumerate" : "metadata", entry.ObjectId, entry.Path, ex.Message));
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) { cancelled = true; }
        catch (DeviceDisconnectedException ex) { errors.Add(new("disconnect",null,null,ex.Message)); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            errors.Add(new("enumeration", null, null, ex.Message));
        }
        cancelled |= cancellationToken.IsCancellationRequested;
        return new(deviceInfo, started, clock.Elapsed, cancelled ? "cancelled" : errors.Count > 0 ? "incomplete" : "complete",
            roots, photos, errors) { Warnings = warnings };

        bool Visit(DeviceEntry entry)
        {
            var key = string.IsNullOrEmpty(entry.ObjectId) ? "path:" + entry.Path : "id:" + entry.ObjectId;
            if (!visited.Add(key)) return false;
            if (string.IsNullOrEmpty(entry.ObjectId)) warnings.Add($"Object ID missing at {entry.Path}; using path for this traversal only.");
            return true;
        }

        void InspectPhoto(DeviceEntry entry)
        {
            if (!Visit(entry) || !Extensions.Contains(Path.GetExtension(entry.Name))) return;
            try
            {
                var photo = device.ReadPhotoMetadata(entry, cancellationToken);
                if(scope is not null && (photo.SourcePath!=entry.Path||photo.FileName!=Path.GetFileName(entry.Path)))
                    throw new IOException("Metadata source path or filename differs from canonical inventory entry.");
                var photoWarnings = photo.Warnings.ToList();
                if (string.IsNullOrWhiteSpace(photo.Format)) photoWarnings.Add("Reported media format is missing; included by photo extension.");
                if (photo.ContentType is not null && !photo.ContentType.Equals("Image", StringComparison.OrdinalIgnoreCase)
                    && !photo.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                    photoWarnings.Add($"Reported content type {photo.ContentType} is not an image; included for inspection.");
                if (photo.Format is not null && (photo.Format.Contains("MP4", StringComparison.OrdinalIgnoreCase)
                    || photo.Format.Contains("Video", StringComparison.OrdinalIgnoreCase)))
                    photoWarnings.Add($"Reported format {photo.Format} contradicts the photo extension.");
                photos.Add(photo with { Warnings = photoWarnings });
                foreach (var date in new[] { photo.DateAuthored, photo.DateCreated, photo.DateModified, photo.PtpCaptureDate })
                    if (date.Error is not null) errors.Add(new(date.Source, entry.ObjectId, entry.Path, date.Error));
            }
            catch (DeviceDisconnectedException) { throw; }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            { errors.Add(new("metadata", entry.ObjectId, entry.Path, ex.Message)); }
        }
    }
}
