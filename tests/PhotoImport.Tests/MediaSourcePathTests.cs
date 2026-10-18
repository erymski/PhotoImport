using PhotoImport.Devices;

namespace PhotoImport.Tests;

public class MediaSourcePathTests
{
    [Theory]
    [InlineData("\\Internal storage\\DCIM\\Camera\\20260904_1.mp4", true)]
    [InlineData("\\Internal storage\\DCIM\\Camera\\20260904_1.JPG", true)]
    [InlineData("relative.mp4", false)]
    [InlineData("\\Internal storage\\..\\a.mp4", false)]
    [InlineData("\\Internal storage\\a.mp4:stream", false)]
    [InlineData("\\Internal storage/a.mp4", false)]
    [InlineData("\\Internal storage\\a.txt", false)]
    public void ValidatesFullMediaPaths(string path, bool valid) =>
        Assert.Equal(valid, MediaSourcePath.IsValid(path));

    [Fact]
    public void DiagnosticPathsStillRejectVideos() =>
        Assert.False(PhotoSourcePath.IsValid("\\Internal storage\\a.mp4"));
}
