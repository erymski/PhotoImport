namespace PhotoImport.Models;

public sealed record JpegExifFileResult(string SourcePath, double ElapsedSeconds, JpegExifObservation Observation);

public sealed class JpegExifReport
{
    public DeviceInfo? Device { get; set; }
    public DateTimeOffset StartedUtc { get; } = DateTimeOffset.UtcNow;
    public double ElapsedSeconds { get; set; }
    public string CompletionStatus { get; set; } = "incomplete";
    public int ApplicationReadLimitPerPhoto => 256 * 1024;
    public bool DriverReadAheadBounded => false;
    public bool ImageBytesSaved => false;
    public bool FullFileFallback => false;
    public required IReadOnlyList<string> RequestedPaths { get; init; }
    public List<JpegExifFileResult> Files { get; } = [];
    public List<string> Errors { get; } = [];
    public List<string> Warnings { get; } = [
        "Explicit media-content read: at most 256 KiB per JPEG in the application. Driver read-ahead may transfer more over USB.",
        "JPEG-only feasibility diagnostic. No image bytes are saved; no import or sync state is changed.",
        "DateTime is reported for comparison with photo-sort. Only DateTimeOriginal is a capture candidate; independent validation is required." ];
}
