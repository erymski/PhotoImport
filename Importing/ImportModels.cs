using PhotoImport.Models;

namespace PhotoImport.Importing;

public record ImportCheckpoint(DateOnly Day, string Mode, IReadOnlyList<string> BoundaryKeys);
public record ImportIdentityRecord(string Mode, string? SerialNumber, string DeviceId, string? Manufacturer, string? Model);
public record ImportRegistration(Guid Id, string Alias, string Destination, IReadOnlyList<string> Roots,
    ImportIdentityRecord Identity, string DatePolicy, ImportCheckpoint Checkpoint);
public record ImportConfiguration(int SchemaVersion, string Kind, IReadOnlyList<ImportRegistration> Devices);
public record ImportInventory(IReadOnlyList<PhotoMetadata> Files, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, bool Complete);
public record ImportCandidate(PhotoMetadata Source, DateOnly Day, string Key, string Kind);
public record ImportPlanRow(PhotoMetadata Source, DateOnly? Day, string? Key, string Decision, string Reason);
public record ImportPlan(IReadOnlyList<ImportPlanRow> Rows, IReadOnlyList<ImportCandidate> Candidates, IReadOnlyList<string> Errors)
{
    public bool Complete => Errors.Count == 0;
    public long TotalBytes => Candidates.Aggregate(0L, (sum,c) => checked(sum + (long)c.Source.SizeBytes!.Value));
    public int SkippedCount => Rows.Count(r => r.Decision == "skipped");
}
public class ImportException(string message, int exitCode = 2, Exception? inner = null) : Exception(message, inner)
{
    public int ExitCode { get; } = exitCode;
}
