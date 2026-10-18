using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using MetadataExtractor.Formats.Icc;
using PhotoImport.Services;

namespace PhotoImport.Tests;

public sealed class CaptureDayResolverTests
{
    internal static ExifSubIfdDirectory Exif(string date, string? offset = null, string? sub = null)
    {
        var directory = new ExifSubIfdDirectory();
        directory.Set(0x9003, date);
        if (offset is not null)
            directory.Set(0x9011, offset);
        if (sub is not null)
            directory.Set(0x9291, sub);
        return directory;
    }

    [Fact]
    public void SameCalendarDayWithDifferentTimesAndOffsetsIsAvailable()
    {
        var result = CaptureDayResolver.Resolve([
            Exif("2026:10:15 01:00:00", "+14:00"),
            Exif("2026:10:15 23:59:59", "-12:00"),
        ]);
        Assert.Equal("available", result.Status);
        Assert.Equal(new DateOnly(2026, 10, 15), result.Day);
    }

    [Theory]
    [InlineData("not-an-offset", "762")]
    [InlineData("+02:00", "nonsense")]
    public void InvalidOffsetOrSubsecondsDoesNotInvalidateCaptureDay(string offset, string sub)
    {
        var result = CaptureDayResolver.Resolve([Exif("2026:10:15 19:17:14", offset, sub)]);
        Assert.Equal("available", result.Status);
        Assert.Equal(new DateOnly(2026, 10, 15), result.Day);
    }

    [Fact]
    public void DifferentOriginalDaysAreAmbiguous()
    {
        var result = CaptureDayResolver.Resolve([
            Exif("2026:10:15 12:00:00"),
            Exif("2026:10:16 12:00:00"),
        ]);
        Assert.Equal("ambiguous", result.Status);
        Assert.Null(result.Day);
    }

    [Theory]
    [InlineData("2026:02:30 12:00:00")]
    [InlineData("bad")]
    public void ImpossibleCalendarDayIsInvalidEvenBesideValidEvidence(string bad)
    {
        var result = CaptureDayResolver.Resolve([Exif("2026:10:15 12:00:00"), Exif(bad)]);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.Day);
    }

    [Fact]
    public void GeneralDateTimeAloneIsMissing()
    {
        var general = new ExifIfd0Directory();
        general.Set(0x0132, "2026:10:15 12:00:00");
        Assert.Equal("missing", CaptureDayResolver.Resolve([general]).Status);
    }

    [Fact]
    public void UnrelatedAppleMakerNoteErrorsRemainWarningsButCaptureErrorsDoNot()
    {
        var apple = new AppleMakernoteDirectory();
        apple.AddError("Unsupported maker-note tag format");
        var result = CaptureDayResolver.Resolve([Exif("2026:10:15 12:00:00"), apple]);
        Assert.Equal("available", result.Status);
        Assert.Contains(result.Warnings, w => w.Contains("maker-note"));
        var broken = Exif("2026:10:15 12:00:00");
        broken.AddError("Broken EXIF directory");
        Assert.Equal("invalid", CaptureDayResolver.Resolve([broken]).Status);
    }

    private static IccDirectory BrokenColourProfile()
    {
        var profile = new IccDirectory();
        profile.AddError(
            "ICC data describes an invalid date/time: year=0 month=0 day=0 hour=0 minute=0 second=0"
        );
        return profile;
    }

    [Fact]
    public void IccProfileDateErrorDoesNotInvalidateValidExifCaptureDay()
    {
        var result = CaptureDayResolver.Resolve([
            Exif("2022:01:18 19:53:11"),
            BrokenColourProfile(),
        ]);
        Assert.Equal("available", result.Status);
        Assert.Equal(new DateOnly(2022, 1, 18), result.Day);
        Assert.Contains(result.Warnings, w => w.Contains("ICC") && w.Contains("year=0"));
    }

    [Fact]
    public void IccProfileErrorDoesNotCreateDateWhenExifIsMissing()
    {
        var result = CaptureDayResolver.Resolve([BrokenColourProfile()]);
        Assert.Equal("missing", result.Status);
        Assert.Null(result.Day);
        Assert.Contains(result.Warnings, w => w.Contains("ICC"));
    }

    [Fact]
    public void IccWarningDoesNotMaskImpossibleExifDay()
    {
        var result = CaptureDayResolver.Resolve([
            Exif("2026:02:30 12:00:00"),
            BrokenColourProfile(),
        ]);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.Day);
        Assert.Contains(result.Warnings, w => w.Contains("ICC"));
    }

    [Fact]
    public void IccWarningDoesNotMaskConflictingExifDays()
    {
        var result = CaptureDayResolver.Resolve([
            Exif("2026:10:15 12:00:00"),
            Exif("2026:10:16 12:00:00"),
            BrokenColourProfile(),
        ]);
        Assert.Equal("ambiguous", result.Status);
        Assert.Null(result.Day);
        Assert.Contains(result.Warnings, w => w.Contains("ICC"));
    }

    [Fact]
    public void BrokenExifStillFailsWhenIccWarningIsPresent()
    {
        var broken = Exif("2022:01:18 19:53:11");
        broken.AddError("Broken EXIF directory");
        var result = CaptureDayResolver.Resolve([broken, BrokenColourProfile()]);
        Assert.Equal("invalid", result.Status);
        Assert.Null(result.Day);
        Assert.Contains(result.Warnings, w => w.Contains("ICC"));
    }
}
