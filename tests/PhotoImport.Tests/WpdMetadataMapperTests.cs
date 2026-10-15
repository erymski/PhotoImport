using PhotoImport.Devices;

namespace PhotoImport.Tests;

public class WpdMetadataMapperTests
{
    [Fact]
    public void NullDateRemainsMissing()
    {
        var result = WpdMetadataMapper.ObserveTimestamp("dateAuthored", null, false);
        Assert.Equal("missing", result.Availability); Assert.Null(result.DisplayValue); Assert.Null(result.RawValue);
    }

    [Theory]
    [InlineData(DateTimeKind.Unspecified)]
    [InlineData(DateTimeKind.Utc)]
    [InlineData(DateTimeKind.Local)]
    public void ProjectedDatePreservesWallClockAndUnknownOffset(DateTimeKind kind)
    {
        var result = WpdMetadataMapper.ObserveTimestamp("dateAuthored", new DateTime(2026, 10, 12, 14, 32, 5, kind), false);
        Assert.Equal("2026-10-12T14:32:05.0000000", result.DisplayValue);
        Assert.Equal("unknown", result.OffsetPresence); Assert.Equal("projected", result.Availability);
        Assert.Null(result.RawValue); Assert.Null(result.RawType);
    }

    [Fact]
    public void RawOffsetDateRetainsOffset()
    {
        var result = WpdMetadataMapper.ObserveTimestamp("ptpCaptureDate",
            new DateTimeOffset(2026, 10, 12, 14, 32, 5, TimeSpan.FromHours(2)), true);
        Assert.Equal("2026-10-12T14:32:05.0000000+02:00", result.RawValue);
        Assert.Equal("present", result.OffsetPresence); Assert.Equal("raw", result.Availability);
        Assert.Equal("System.DateTimeOffset", result.RawType);
    }

    [Fact]
    public void FailedPropertyRetainsError()
    {
        var result = WpdMetadataMapper.ObserveTimestamp("dateAuthored", null, false, "Access denied");
        Assert.Equal("error", result.Availability); Assert.Equal("Access denied", result.Error);
    }
}
