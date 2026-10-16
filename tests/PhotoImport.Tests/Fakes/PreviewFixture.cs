using PhotoImport.Models;

namespace PhotoImport.Tests.Fakes;

internal static class PreviewFixture
{
    public static PhotoMetadata Photo(string path = "\\a.jpg", ulong? size = 100) =>
        FakePhotoDevice.Photo(FakePhotoDevice.Entry(path, path)) with { SizeBytes = size, PersistentObjectId = "p:" + path };
    public static ProbeReport Inventory(params PhotoMetadata[] photos) =>
        new(FakePhotoDevice.Info(), DateTimeOffset.Parse("2026-10-16T00:00:00Z"), TimeSpan.Zero, "complete", ["\\"], photos, []);
    public static JpegExifFileResult Capture(string path = "\\a.jpg", string original = "2026:10:11 12:00:00", ulong? size = 100)
    {
        var day = DateOnly.ParseExact(original[..10], "yyyy:MM:dd");
        return new(path, 0, new(100, "available", new Dictionary<string,string?> { ["dateTimeOriginal"] = original }, null, null, [], null) {
            CaptureDay = day, CaptureDayStatus = "available", CaptureDirectories = [new(original, null, null)] }) {
            Source = new(path, size, "p:" + path) };
    }
    public static CaptureReportInput Report(params JpegExifFileResult[] photos) => new(2, FakePhotoDevice.Info(), "complete", photos);
    public static DayCheckpoint Checkpoint(string mode = "completedDay", params string[] keys) => new(1, "phone", new(2026,10,10), mode, keys);
}
