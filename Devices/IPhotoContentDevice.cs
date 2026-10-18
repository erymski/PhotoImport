using PhotoImport.Models;

namespace PhotoImport.Devices;

public interface IPhotoContentDevice
{
    PhotoReadHandle OpenPhotoRead(string sourcePath, CancellationToken token);
}

public sealed class PhotoReadHandle(PhotoSourceInfo source, Stream stream) : IDisposable
{
    public PhotoSourceInfo Source { get; } = source;
    public Stream Stream { get; } = stream;

    public void Dispose() => Stream.Dispose();
}
