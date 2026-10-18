namespace PhotoImport.Models;

public sealed class LivePreviewManifest
{
    public int SchemaVersion => 1;
    public IReadOnlyList<string> RequestedRoots { get; init; } = [];
    public IReadOnlyList<string> ResolvedRoots { get; set; } = [];
    public int MaxPhotos { get; init; }
    public long MaximumApplicationBytes => (long)MaxPhotos * 262144;
    public int AttemptedCount { get; set; }
    public long ApplicationBytesRead { get; set; }
    public bool LimitReached { get; set; }
    public string CompletionStatus { get; set; } = "incomplete";
    public List<string> Errors { get; } = [];
}
