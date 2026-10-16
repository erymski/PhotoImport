namespace PhotoImport.Models;

public sealed record ExifCaptureDirectory(string? DateTimeOriginal, string? SubSecTimeOriginal, string? OffsetTimeOriginal);

public sealed record JpegExifObservation(
    int BytesRead, string Outcome, IReadOnlyDictionary<string, string?> RawFields,
    string? CaptureClock, string? CaptureTimestamp, IReadOnlyList<string> Warnings, string? Error)
{
    public DateOnly? CaptureDay { get; init; }
    public string CaptureDayStatus { get; init; } = "missing";
    public IReadOnlyList<ExifCaptureDirectory> CaptureDirectories { get; init; } = [];
    public string Source { get; init; } = "Embedded EXIF in first JPEG EXIF APP1 segment; MetadataExtractor";
    public int ApplicationReadLimit => 256 * 1024;
}
