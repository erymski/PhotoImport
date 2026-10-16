using MetadataExtractor;
using PhotoImport.Models;

namespace PhotoImport.Services;

public static class MediaExifReader
{
    public static JpegExifObservation Read(Stream source, string extension, CancellationToken token)
    {
        var ext = extension.ToLowerInvariant();
        if (ext is ".jpg" or ".jpeg") return JpegExifReader.Read(source, token);
        var count = 0;
        var provenance = $"Embedded EXIF in bounded {ext} memory prefix; MetadataExtractor";
        JpegExifObservation Failure(string outcome, string message) => new(count, outcome, new Dictionary<string,string?>(), null, null, [], message) { Source = provenance };
        if (ext is not (".heic" or ".heif" or ".dng")) return Failure("unsupported", "Capture reader is not validated for this format; no content read.");
        var buffer = new byte[256*1024];
        try
        {
            while (count < buffer.Length)
            {
                token.ThrowIfCancellationRequested();
                var read = source.Read(buffer, count, Math.Min(16*1024, buffer.Length-count));
                if (read == 0) break;
                count += read;
            }
            token.ThrowIfCancellationRequested();
            using var memory = new MemoryStream(buffer, 0, count, writable:false);
            var directories = ImageMetadataReader.ReadMetadata(memory);
            return ExifObservationReader.FromDirectories(directories, count, provenance,
                ["Only the bounded memory prefix was parsed; later metadata may be outside this observation."]);
        }
        catch (OperationCanceledException) { return Failure("cancelled", "Cancelled between reads."); }
        catch (Exception ex) when (ex is IOException or ImageProcessingException or UnauthorizedAccessException or ArgumentException)
        { return Failure(token.IsCancellationRequested ? "cancelled" : "incomplete", ex.Message); }
        finally { Array.Clear(buffer); }
    }
}


