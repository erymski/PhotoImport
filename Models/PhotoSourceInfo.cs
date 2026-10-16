namespace PhotoImport.Models;

public sealed record PhotoSourceInfo(string CanonicalPath, ulong? SizeBytes, string? PersistentObjectId);
