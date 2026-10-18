namespace PhotoImport.Models;

public record TimestampObservation(
    string Source,
    string? RawValue,
    string? RawType,
    string? DisplayValue,
    string OffsetPresence,
    string Availability,
    string? Error
)
{
    public static TimestampObservation Missing(string source) =>
        new(source, null, null, null, "unknown", "missing", null);

    public bool HasValue => Availability is "raw" or "projected" && DisplayValue is not null;
}

public record PhotoMetadata(
    string ObjectId,
    string? PersistentObjectId,
    string SourcePath,
    string FileName,
    ulong? SizeBytes,
    string? ContentType,
    string? Format,
    TimestampObservation DateAuthored,
    TimestampObservation DateCreated,
    TimestampObservation DateModified,
    TimestampObservation PtpCaptureDate,
    IReadOnlyList<string> Warnings
);
