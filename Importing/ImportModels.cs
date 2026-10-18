using PhotoImport.Models;

namespace PhotoImport.Importing;

public record ImportCheckpoint(DateOnly Day, string Mode, IReadOnlyList<string> BoundaryKeys);
public record ImportIdentityRecord(string Mode, string? SerialNumber, string DeviceId, string? Manufacturer, string? Model);
public record ImportRegistration(Guid Id, string Alias, string Destination, IReadOnlyList<string> Roots,
    ImportIdentityRecord Identity, string DatePolicy, ImportCheckpoint Checkpoint);
public record ImportConfiguration(int SchemaVersion, string Kind, IReadOnlyList<ImportRegistration> Devices);
public record ImportInventory(IReadOnlyList<PhotoMetadata> Files, IReadOnlyList<string> Errors, IReadOnlyList<string> Warnings, bool Complete,int IgnoredNonmediaCount=0);
public record ImportCandidate(PhotoMetadata Source, DateOnly Day, string Key, string Kind);
public record ImportPlanRow(PhotoMetadata Source, DateOnly? Day, string? Key, string Decision, string Reason);
public record ImportPlan(IReadOnlyList<ImportPlanRow> Rows, IReadOnlyList<ImportCandidate> Candidates, IReadOnlyList<string> Errors,int IgnoredNonmediaCount=0)
{
    public bool Complete => Errors.Count == 0;
    public long TotalBytes => Candidates.Aggregate(0L, (sum,c) => checked(sum + (long)c.Source.SizeBytes!.Value));
    public int SkippedCount => Rows.Count(r => r.Decision == "skipped");
}
public class ImportException(string message, int exitCode = 2, Exception? inner = null) : Exception(message, inner)
{
    public int ExitCode { get; } = exitCode;
}
public record PendingImportFile(ImportCandidate Candidate,string FinalPath,string PartialPath,string Stage="planned",long Length=0,string? Sha256=null);
public record PendingImport(int SchemaVersion,Guid SessionId,ImportRegistration Registration,IReadOnlyList<PendingImportFile> Files,
    string Stage="copying",ImportCheckpoint? NewCheckpoint=null);
public record ImportRunResult
{
    public int ExitCode {get;init;}
    public bool CheckpointCommitted {get;init;}
    public int Copied {get;init;}
    public int Reused {get;init;}
    public long Bytes {get;init;}
    public int IgnoredNonmediaCount {get;init;}
    public IReadOnlyList<ImportPlanRow> Rows {get;init;}=[];
    public IReadOnlyList<string> Errors {get;init;}=[];
    public IReadOnlyList<string> Warnings {get;init;}=[];
    public ImportCheckpoint? OldCheckpoint {get;init;}
    public ImportCheckpoint? NewCheckpoint {get;init;}
    public DateTimeOffset Started {get;init;}=DateTimeOffset.UtcNow;
    public DateTimeOffset Finished {get;init;}=DateTimeOffset.UtcNow;
}
public enum ImportWritePoint {BeforeConfigReplace,AfterConfigReplace,BeforeJournalReplace,AfterJournalReplace,BeforeArchive}
