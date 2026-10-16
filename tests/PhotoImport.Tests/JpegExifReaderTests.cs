using PhotoImport.Services;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public sealed class JpegExifReaderTests
{
    [Theory]
    [InlineData(0x9011)]
    [InlineData(0x9291)]
    public void OrphanOffsetOrSubsecondsCannotBeBorrowedFromAnotherDirectory(int orphanTag)
    {
        using var stream = new ForwardOnlyStream(JpegFixture.Create(offset: null, subseconds: null, orphanTag: (ushort)orphanTag));
        var result = JpegExifReader.Read(stream, default);
        Assert.Equal("ambiguous", result.Outcome);
        Assert.Null(result.CaptureClock);
        Assert.Null(result.CaptureTimestamp);
        Assert.Contains(result.Warnings, w => w.Contains("directory", StringComparison.OrdinalIgnoreCase));
        Assert.Equal(2, result.CaptureDirectories.Count);
        Assert.Equal("2026:10:15 19:17:14", result.CaptureDirectories[0].DateTimeOriginal);
        Assert.Null(result.CaptureDirectories[0].OffsetTimeOriginal);
        Assert.Null(result.CaptureDirectories[0].SubSecTimeOriginal);
        Assert.Null(result.CaptureDirectories[1].DateTimeOriginal);
    }

    [Fact]
    public void ConflictingCaptureDirectoriesAreAmbiguousRatherThanSilentlyChosen()
    {
        using var stream = new ForwardOnlyStream(JpegFixture.Create(conflict: true));
        var result = JpegExifReader.Read(stream, default);
        Assert.Equal("ambiguous", result.Outcome);
        Assert.Null(result.CaptureTimestamp);
        Assert.Contains(result.Warnings, w => w.Contains("2020:01:01") && w.Contains("2026:10:15"));
    }

    [Fact]
    public void GeneralDateTimeDoesNotReplaceMissingDateTimeOriginal()
    {
        using var stream = new ForwardOnlyStream(JpegFixture.Create(original: ""));
        var result = JpegExifReader.Read(stream, default);
        Assert.Equal("missing", result.Outcome);
        Assert.Equal("2026:10:15 19:17:17", result.RawFields["dateTime"]);
        Assert.Null(result.CaptureTimestamp);
    }

    [Fact]
    public void PreservesBothDatePoliciesAndOffsetAndSubsecondsWithoutReadingScanData()
    {
        var jpeg = JpegFixture.Create();
        using var stream = new ForwardOnlyStream(jpeg, 3);
        var result = JpegExifReader.Read(stream, default);
        Assert.Equal("available", result.Outcome);
        Assert.Equal("2026:10:15 19:17:17", result.RawFields["dateTime"]);
        Assert.Equal("2026:10:15 19:17:14", result.RawFields["dateTimeOriginal"]);
        Assert.Equal("+02:00", result.RawFields["offsetTimeOriginal"]);
        Assert.Equal("762", result.RawFields["subSecTimeOriginal"]);
        Assert.Equal("2026-10-15T19:17:14.762", result.CaptureClock);
        Assert.Equal("2026-10-15T19:17:14.762+02:00", result.CaptureTimestamp);
        Assert.Equal(jpeg.Length - 6, result.BytesRead);
        Assert.Equal(result.BytesRead, stream.Consumed);
        Assert.False(stream.Disposed);
        Assert.Contains(result.Warnings, w => w.Contains("Later segments"));
    }

    [Fact]
    public void MissingOffsetKeepsWallClockWithoutInferringAnInstant()
    {
        using var stream = new ForwardOnlyStream(JpegFixture.Create(offset: null, subseconds: null));
        var result = JpegExifReader.Read(stream, default);
        Assert.Equal("available", result.Outcome);
        Assert.Equal("2026-10-15T19:17:14", result.CaptureClock);
        Assert.Null(result.CaptureTimestamp);
        Assert.Null(result.RawFields["offsetTimeOriginal"]);
    }

    [Theory]
    [InlineData("2026:02:30 10:00:00", "+02:00", "762")]
    [InlineData("2026:10:15 19:17:14", "+15:00", "762")]
    [InlineData("2026:10:15 19:17:14", "+02:60", "762")]
    [InlineData("2026:10:15 19:17:14", "+02:00", "not-a-number")]
    public void InvalidCaptureFieldsRemainRawWithoutFabricatingTimestamp(string date, string offset, string sub)
    {
        using var stream = new ForwardOnlyStream(JpegFixture.Create(date, offset, sub));
        var result = JpegExifReader.Read(stream, default);
        Assert.Equal("invalid", result.Outcome);
        Assert.Equal(date, result.RawFields["dateTimeOriginal"]);
        Assert.Equal(offset, result.RawFields["offsetTimeOriginal"]);
        Assert.Equal(sub, result.RawFields["subSecTimeOriginal"]);
        Assert.Null(result.CaptureTimestamp);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void NoExifIsMissingAndNonJpegIsUnsupported()
    {
        using var noExif = new ForwardOnlyStream([0xFF, 0xD8, 0xFF, 0xDA]);
        using var other = new ForwardOnlyStream([0x89, 0x50, 0x4E, 0x47]);
        Assert.Equal("missing", JpegExifReader.Read(noExif, default).Outcome);
        Assert.Equal("unsupported", JpegExifReader.Read(other, default).Outcome);
    }

    [Fact]
    public void TruncatedExifIsIncomplete()
    {
        using var stream = new ForwardOnlyStream(JpegFixture.Create()[..30], 2);
        var result = JpegExifReader.Read(stream, default);
        Assert.Equal("incomplete", result.Outcome);
        Assert.Equal(30, result.BytesRead);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void StopsAtBudgetOnLargePrecedingSegments()
    {
        var app = JpegFixture.Segment(0xE2, new byte[65533]);
        var jpeg = new byte[] { 0xFF, 0xD8 }.Concat(Enumerable.Range(0, 5).SelectMany(_ => app)).Concat(JpegFixture.Create()[2..]).ToArray();
        using var stream = new ForwardOnlyStream(jpeg, 127);
        var result = JpegExifReader.Read(stream, default);
        Assert.Equal("incomplete", result.Outcome);
        Assert.Equal(262144, result.BytesRead);
        Assert.Equal(262144, stream.Consumed);
        Assert.Contains("limit", result.Error!);
    }

    [Fact]
    public void CancellationRetainsActualByteCount()
    {
        using var cts = new CancellationTokenSource();
        using var stream = new ForwardOnlyStream(JpegFixture.Create(), 1);
        stream.OnRead = () => { if (stream.Consumed >= 10) cts.Cancel(); };
        var result = JpegExifReader.Read(stream, cts.Token);
        Assert.Equal("cancelled", result.Outcome);
        Assert.Equal(stream.Consumed, result.BytesRead);
        Assert.InRange(result.BytesRead, 10, 11);
    }
}
