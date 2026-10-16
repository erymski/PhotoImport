namespace PhotoImport.Models;

public sealed record LivePreviewOptions(string DeviceSelector,IReadOnlyList<string> Roots,int MaxPhotos,DateOnly? CutoffDay,string? CheckpointPath,string OutputDirectory);
