namespace PhotoImport.Models;

public sealed record CaptureDayResolution(
    string Status,
    DateOnly? Day,
    IReadOnlyList<string> Warnings,
    string? Error
);
