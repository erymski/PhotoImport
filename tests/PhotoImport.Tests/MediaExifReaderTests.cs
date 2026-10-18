using PhotoImport.Services;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public sealed class MediaExifReaderTests
{
    [Theory]
    [InlineData(".dng")]
    [InlineData(".heic")]
    public void RenamedJpegDoesNotBecomeValidatedDngOrHeic(string extension)
    {
        using var source = new ForwardOnlyStream(JpegFixture.Create());
        var result = MediaExifReader.Read(source, extension, default);
        Assert.Equal("unsupported", result.Outcome);
        Assert.Null(result.CaptureDay);
    }

    [Theory]
    [InlineData(".jpg")]
    [InlineData(".dng")]
    public void RealLibraryExtractsDayFromForwardOnlySyntheticMedia(string extension)
    {
        var jpeg = JpegFixture.Create();
        var payload = extension == ".dng" ? jpeg[12..^6] : jpeg;
        using var stream = new ForwardOnlyStream(payload, 3);
        var result = MediaExifReader.Read(stream, extension, default);
        Assert.Equal("available", result.CaptureDayStatus);
        Assert.Equal(new DateOnly(2026, 10, 15), result.CaptureDay);
        Assert.Equal(stream.Consumed, result.BytesRead);
        Assert.False(stream.Disposed);
    }

    [Theory]
    [InlineData("+15:00", "762")]
    [InlineData("+02:00", "bad")]
    public void ExactTimestampFailureDoesNotDiscardValidCalendarDay(
        string offset,
        string subseconds
    )
    {
        using var stream = new ForwardOnlyStream(
            JpegFixture.Create(offset: offset, subseconds: subseconds)
        );
        var result = MediaExifReader.Read(stream, ".jpg", default);
        Assert.Equal("available", result.CaptureDayStatus);
        Assert.Equal(new DateOnly(2026, 10, 15), result.CaptureDay);
        Assert.Equal("invalid", result.Outcome);
        Assert.Null(result.CaptureTimestamp);
    }

    [Fact]
    public void DngReadsStayWithinCapAndPngIsUnsupportedWithoutReads()
    {
        var payload = JpegFixture.Create()[12..^6].Concat(new byte[300000]).ToArray();
        using var source = new ForwardOnlyStream(payload, 71);
        var result = MediaExifReader.Read(source, ".DNG", default);
        Assert.Equal(262144, source.Consumed);
        Assert.Equal("available", result.CaptureDayStatus);
        using var png = new ForwardOnlyStream([1, 2, 3]);
        Assert.Equal("unsupported", MediaExifReader.Read(png, ".png", default).Outcome);
        Assert.Equal(0, png.Consumed);
    }

    [Fact]
    public void TruncatedHeicReturnsUnresolvedWithoutFallback()
    {
        using var source = new ForwardOnlyStream([
            0,
            0,
            0,
            40,
            102,
            116,
            121,
            112,
            104,
            101,
            105,
            99,
            0,
            0,
            0,
            0,
            104,
            101,
            105,
            99,
        ]);
        var result = MediaExifReader.Read(source, ".heic", default);
        Assert.NotEqual("available", result.CaptureDayStatus);
        Assert.Null(result.CaptureDay);
        Assert.Equal(source.Consumed, result.BytesRead);
        Assert.False(source.Disposed);
    }

    [Fact]
    public void DngCancellationRetainsConsumedCount()
    {
        using var cts = new CancellationTokenSource();
        using var source = new ForwardOnlyStream(new byte[300000], 1);
        source.OnRead = cts.Cancel;
        var result = MediaExifReader.Read(source, ".dng", cts.Token);
        Assert.Equal("cancelled", result.Outcome);
        Assert.Equal(source.Consumed, result.BytesRead);
        Assert.InRange(source.Consumed, 0, 1);
    }
}
