namespace PhotoImport.Models;

public sealed record DayCheckpoint(
    int SchemaVersion,
    string DeviceId,
    DateOnly CutoffDay,
    string BoundaryMode,
    IReadOnlyList<string> BoundaryKeys
);

public sealed class PreviewInputException(string message) : FormatException(message);
