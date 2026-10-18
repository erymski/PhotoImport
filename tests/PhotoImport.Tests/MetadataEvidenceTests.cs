using PhotoImport.Models;

namespace PhotoImport.Tests;

public class MetadataEvidenceTests
{
    [Fact]
    public void MissingCaptureDateDoesNotUseCreatedOrModified()
    {
        var missing = new TimestampObservation(
            "dateAuthored",
            null,
            null,
            null,
            "unknown",
            "missing",
            null
        );
        var created = new TimestampObservation(
            "dateCreated",
            null,
            null,
            "2026-10-12T14:32:05",
            "unknown",
            "projected",
            null
        );
        var photo = new PhotoMetadata(
            "1",
            null,
            "\\DCIM\\a.jpg",
            "a.jpg",
            42,
            null,
            null,
            missing,
            created,
            created,
            missing,
            []
        );
        var report = new ProbeReport(
            new("phone", "Phone", null, null, null, new Dictionary<string, string?>()),
            DateTimeOffset.UtcNow,
            TimeSpan.Zero,
            "complete",
            [],
            [photo],
            []
        );
        Assert.Equal(0, report.WithCaptureDateCount);
        Assert.Equal(1, report.WithoutCaptureDateCount);
    }
}
