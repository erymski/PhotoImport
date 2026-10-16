namespace PhotoImport.Models;

public sealed record JpegExifObservation(
    int BytesRead, string Outcome, IReadOnlyDictionary<string, string?> RawFields,
    string? CaptureClock, string? CaptureTimestamp, IReadOnlyList<string> Warnings, string? Error)
{
    public string Source => "Embedded EXIF in first JPEG EXIF APP1 segment; MetadataExtractor";
    public int ApplicationReadLimit => 256 * 1024;
}
