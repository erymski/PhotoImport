namespace PhotoImport.Models;

public sealed record CaptureCollectionResult(
    JpegExifReport Capture,
    int AttemptedCount,
    long ApplicationBytesRead,
    bool LimitReached,
    bool Disconnected
);
