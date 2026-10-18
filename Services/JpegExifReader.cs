using System.Globalization;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Jpeg;
using MetadataExtractor.IO;
using PhotoImport.Models;

namespace PhotoImport.Services;

public static class JpegExifReader
{
    public static JpegExifObservation Read(Stream stream, CancellationToken token)
    {
        using var bounded = new PrefixStream(stream, token);
        JpegExifObservation Result(string outcome, string? error = null) => new(bounded.BytesRead, outcome, new Dictionary<string,string?>(), null, null, [], error);
        try
        {
            // Read every segment type lazily: the package's filtered mode may seek to skip
            // unwanted segments. USB streams are forward-only. No EXIF decoding lives here.
            var segment = JpegSegmentReader.ReadSegments(new SequentialStreamReader(bounded))
                .FirstOrDefault(s => s.Type == JpegSegmentType.App1 && ExifReader.StartsWithJpegExifPreamble(s.Bytes));
            token.ThrowIfCancellationRequested();
            if (segment is null) return Result("missing", "No EXIF segment before JPEG scan data/end.");
            return ExifObservationReader.FromDirectories(new ExifReader().ReadJpegSegments([segment]).ToList(), bounded.BytesRead, "Embedded EXIF in first JPEG EXIF APP1 segment; MetadataExtractor", ["Later segments were not inspected; this observation covers the first EXIF APP1 segment only."]);
        }
        catch (OperationCanceledException) { return Result("cancelled", "Cancelled between USB reads; synchronous driver calls may delay cancellation."); }
        catch (JpegProcessingException ex) { return Result(bounded.BytesRead <= 2 ? "unsupported" : "invalid", ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return Result(token.IsCancellationRequested ? "cancelled" : "incomplete", ex.Message); }
    }

    private sealed class PrefixStream(Stream source, CancellationToken token) : Stream
    {
        public int BytesRead { get; private set; }
        public override int Read(byte[] buffer, int offset, int count)
        {
            token.ThrowIfCancellationRequested();
            if (count == 0) return 0;
            var remaining = 256 * 1024 - BytesRead;
            if (remaining == 0) throw new IOException("Application prefix read limit reached; no full-file fallback.");
            var read = source.Read(buffer, offset, Math.Min(count, remaining));
            BytesRead += read;
            return read;
        }
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Position { get => BytesRead; set => throw new NotSupportedException(); }
        public override long Length => throw new NotSupportedException();
        public override void Flush() => throw new NotSupportedException();
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}

