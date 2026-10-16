namespace PhotoImport.Devices;

// Explicit opt-in capability, separate from the metadata-only IPhotoDevice contract.
public interface IJpegContentDevice
{
    Stream OpenJpegRead(string sourcePath, CancellationToken token);
}

internal static class JpegSourcePath
{
    public static bool IsValid(string path) => !string.IsNullOrWhiteSpace(path)
        && path.StartsWith('\\') && !path.StartsWith("\\\\", StringComparison.Ordinal)
        && path.IndexOfAny(['/', '*', '?', ':', '\0']) < 0
        && path.Split('\\').Skip(1).All(part => part.Length > 0 && part is not ("." or ".."))
        && (path.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase));
}
