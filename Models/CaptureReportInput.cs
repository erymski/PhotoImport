namespace PhotoImport.Models;

public sealed record CaptureReportInput(int? SchemaVersion, DeviceInfo Device, string CompletionStatus, IReadOnlyList<JpegExifFileResult> Files)
{
    public IReadOnlyList<string> Errors { get; init; } = [];
}
