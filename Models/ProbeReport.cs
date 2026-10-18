namespace PhotoImport.Models;

public record ProbeError(string Operation, string? ObjectId, string? Path, string Message);

public record ProbeReport(DeviceInfo Device, DateTimeOffset StartedAt, TimeSpan EnumerationDuration,
    string CompletionStatus, IReadOnlyList<string> SearchedRoots, IReadOnlyList<PhotoMetadata> Photos,
    IReadOnlyList<ProbeError> Errors)
{
    public int PhotoCount => Photos.Count;
    public int WithCaptureDateCount => Photos.Count(p => p.DateAuthored.HasValue || p.PtpCaptureDate.HasValue);
    public int WithoutCaptureDateCount => PhotoCount - WithCaptureDateCount;
    public int WithAuthoredDateCount => Photos.Count(p => p.DateAuthored.HasValue);
    public int WithPtpDateCount => Photos.Count(p => p.PtpCaptureDate.HasValue);
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
