namespace PhotoImport.Models;

public sealed record PreviewRow(string SourcePath, ulong? SizeBytes, DateOnly? CaptureDay, string? BoundaryKey, string Decision, string Reason);
public sealed class PreviewReport
{
    public int SchemaVersion => 1;
    public required DeviceInfo Device { get; init; }
    public required IReadOnlyList<string> InventoryScope { get; init; }
    public required DayCheckpoint Checkpoint { get; init; }
    public required int TotalPhotoCount { get; init; }
    public string CompletionStatus { get; set; } = "incomplete";
    public bool IsComplete => CompletionStatus == "complete";
    public List<PreviewRow> Rows { get; } = [];
    public List<string> InputProblems { get; } = [];
    public List<string> Warnings { get; } = ["Offline snapshot preview only; source path and size association is not content identity or current phone verification."];
    public int ProcessedCount => Rows.Count;
    public int SelectedCount => Rows.Count(r => r.Decision == "eligible");
    public decimal SelectedKnownBytes => Rows.Where(r => r.Decision == "eligible").Sum(r => (decimal)(r.SizeBytes ?? 0));
    public int UnknownSizeCount { get; init; }
    public int UnresolvedCount => Rows.Count(r => r.Decision == "unresolved");
}
