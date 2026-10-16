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
        var fields = new Dictionary<string, string?>();
        var warnings = new List<string>();
        JpegExifObservation Result(string outcome, string? error = null, string? clock = null, string? timestamp = null) =>
            new(bounded.BytesRead, outcome, fields, clock, timestamp, warnings, error);
        try
        {
            // Read every segment type lazily: the package's filtered mode may seek to skip
            // unwanted segments. USB streams are forward-only. No EXIF decoding lives here.
            var segment = JpegSegmentReader.ReadSegments(new SequentialStreamReader(bounded))
                .FirstOrDefault(s => s.Type == JpegSegmentType.App1 && ExifReader.StartsWithJpegExifPreamble(s.Bytes));
            token.ThrowIfCancellationRequested();
            if (segment is null) return Result("missing", "No EXIF segment before JPEG scan data/end.");
            warnings.Add("Later segments were not inspected; this observation covers the first EXIF APP1 segment only.");
            var directories = new ExifReader().ReadJpegSegments([segment]).ToList();
            var tags = new (string Name, int Tag, bool Ifd0)[] {
                ("dateTime", 0x0132, true), ("offsetTime", 0x9010, false),
                ("dateTimeOriginal", 0x9003, false), ("dateTimeDigitized", 0x9004, false),
                ("offsetTimeOriginal", 0x9011, false), ("offsetTimeDigitized", 0x9012, false),
                ("subSecTimeOriginal", 0x9291, false), ("subSecTimeDigitized", 0x9292, false) };
            var ambiguous = false;
            foreach (var (name, tag, ifd0) in tags)
            {
                var values = directories.Where(d => ifd0 ? d is ExifIfd0Directory : d is ExifSubIfdDirectory)
                    .Select(d => d.GetString(tag)).Where(v => v is not null).Distinct(StringComparer.Ordinal).ToArray();
                fields[name] = values.Length == 1 ? values[0] : null;
                if (values.Length > 1) { ambiguous = true; warnings.Add($"Conflicting {name} values: {string.Join("; ", values)}"); }
            }
            warnings.AddRange(directories.SelectMany(d => d.Errors.Select(e => $"{d.Name}: {e}")));
            if (ambiguous) return Result("ambiguous", "Conflicting EXIF date fields.");
            if (directories.Any(d => d.HasError)) return Result("invalid", "EXIF parser reported errors; capture date is unverified.");
            var original = fields["dateTimeOriginal"];
            if (string.IsNullOrWhiteSpace(original)) return Result("missing", "EXIF DateTimeOriginal is absent; no DateTime or filename fallback.");
            if (!DateTime.TryParseExact(original, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var date)) return Result("invalid", "Invalid EXIF DateTimeOriginal.");
            var subseconds = fields["subSecTimeOriginal"];
            if (subseconds is not null)
            {
                if (subseconds.Length is < 1 or > 7 || subseconds.Any(c => c is < '0' or > '9'))
                    return Result("invalid", "Invalid or unsupported EXIF SubSecTimeOriginal precision.");
                date = date.AddTicks(long.Parse(subseconds.PadRight(7, '0'), CultureInfo.InvariantCulture));
            }
            var offset = fields["offsetTimeOriginal"];
            if (offset is not null && !ValidOffset(offset)) return Result("invalid", "Invalid EXIF OffsetTimeOriginal.");
            var clock = date.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
            if (offset is null) warnings.Add("Capture offset is unknown; no UTC instant or PC timezone was inferred.");
            return Result("available", clock: clock, timestamp: offset is null ? null : clock + offset);
        }
        catch (OperationCanceledException) { return Result("cancelled", "Cancelled between USB reads; synchronous driver calls may delay cancellation."); }
        catch (JpegProcessingException ex) { return Result(bounded.BytesRead <= 2 ? "unsupported" : "invalid", ex.Message); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { return Result(token.IsCancellationRequested ? "cancelled" : "incomplete", ex.Message); }
    }

    private static bool ValidOffset(string value)
    {
        if (value.Length != 6 || value[0] is not ('+' or '-') || value[3] != ':'
            || new[] { value[1], value[2], value[4], value[5] }.Any(c => c is < '0' or > '9')) return false;
        var hours = (value[1] - '0') * 10 + value[2] - '0';
        var minutes = (value[4] - '0') * 10 + value[5] - '0';
        return hours <= 14 && minutes < 60 && (hours != 14 || minutes == 0);
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
