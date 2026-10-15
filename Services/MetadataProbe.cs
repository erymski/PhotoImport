using System.Diagnostics;
using PhotoImport.Devices;
using PhotoImport.Models;

namespace PhotoImport.Services;

public static class MetadataProbe
{
    private static readonly HashSet<string> Extensions = new(StringComparer.OrdinalIgnoreCase)
        { ".jpg", ".jpeg", ".heic", ".heif", ".png", ".dng", ".tif", ".tiff" };

    public static ProbeReport Run(IPhotoDevice device, DeviceInfo deviceInfo, CancellationToken cancellationToken)
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
            foreach (var root in device.GetStorageRoots(cancellationToken))
            {
                roots.Add(root.Path);
                stack.Push(root);
            }
            while (stack.TryPop(out var entry))
            {
                cancellationToken.ThrowIfCancellationRequested();
                var key = string.IsNullOrEmpty(entry.ObjectId) ? "path:" + entry.Path : "id:" + entry.ObjectId;
                if (!visited.Add(key)) continue;
                if (string.IsNullOrEmpty(entry.ObjectId)) warnings.Add($"Object ID missing at {entry.Path}; using path for this traversal only.");
                try
                {
                    if (entry.IsDirectory)
                    {
                        if (entry.Name.Equals(".thumbnails", StringComparison.OrdinalIgnoreCase))
                        {
                            warnings.Add($"Skipped thumbnail directory: {entry.Path}");
                            continue;
                        }
                        // Reverse to retain the device's reported sibling order in this depth-first traversal.
                        var children = device.GetChildren(entry, cancellationToken);
                        for (var i = children.Count - 1; i >= 0; i--) stack.Push(children[i]);
                    }
                    else if (Extensions.Contains(Path.GetExtension(entry.Name)))
                    {
                        var photo = device.ReadPhotoMetadata(entry, cancellationToken);
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
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
        {
            errors.Add(new("enumeration", null, null, ex.Message));
        }
        return new(deviceInfo, started, clock.Elapsed, cancelled ? "cancelled" : errors.Count > 0 ? "incomplete" : "complete",
            roots, photos, errors) { Warnings = warnings };
    }
}
