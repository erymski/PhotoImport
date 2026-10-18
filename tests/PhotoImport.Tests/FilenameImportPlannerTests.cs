using PhotoImport.Importing;
using PhotoImport.Models;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public class FilenameDayPolicyTests
{
    [Theory]
    [InlineData("20260904_120000.JPG", "2026-09-04")]
    [InlineData("Screenshot_20260904-120000_App.png", "2026-09-04")]
    [InlineData("WP_20150621_023.jpg", "2015-06-21")]
    [InlineData("20240229(1).heic", "2024-02-29")]
    [InlineData("20260230_120000.jpg", null)]
    [InlineData("20230229_120000.jpg", null)]
    [InlineData("20260904evil.jpg", null)]
    [InlineData("IMG_1234.jpg", null)]
    public void RecognizesOnlyValidCalendarPrefixes(string name, string? expected)
    {
        var valid = FilenameDayPolicy.TryGetDay(name, out var day);
        Assert.Equal(expected is not null, valid);
        if (valid) Assert.Equal(expected, day.ToString("yyyy-MM-dd"));
    }

    [Theory]
    [InlineData("a.MP4", "video")][InlineData("a.mov", "video")]
    [InlineData("a.dng", "photo")][InlineData("a.png", "photo")]
    [InlineData("a.txt", null)]
    public void ClassifiesMediaWithoutOpeningIt(string name, string? kind) => Assert.Equal(kind, FilenameDayPolicy.Classify(name));
}

public class FilenameImportPlannerTests
{
    internal static ImportRegistration Registration(string mode = "completedDay", params string[] keys) => new(
        Guid.Parse("11111111-1111-1111-1111-111111111111"), "S21", Path.GetTempPath(),
        ["\\Internal storage\\DCIM\\Camera"], new("deviceId", null, "phone", "Maker", "Model"),
        "androidFilenameDayV1", new(new(2026,9,3), mode, keys));
    internal static PhotoMetadata File(string name, ulong? size = 42) => FakePhotoDevice.Photo(
        FakePhotoDevice.Entry(name, "\\Internal storage\\DCIM\\Camera\\" + name)) with { SizeBytes = size };
    internal static ImportInventory Inventory(params PhotoMetadata[] files) => new(files, [], [], true);

    [Fact] public void CompletedDay_ExcludesSeptember3_SelectsSeptember4()
    {
        var plan = FilenameImportPlanner.Build(Registration(), Inventory(File("20260903_120000.jpg"), File("20260904_120000.mp4")));
        Assert.Single(plan.Candidates); Assert.Equal("video", plan.Candidates[0].Kind);
        Assert.Equal("20260904_120000.mp4", plan.Candidates[0].Source.FileName);
    }
    [Fact] public void TrackedDay_AllowsAbsentKey_KeepsKnownKeyExcluded()
    {
        var known = File("20260903_120000.jpg");
        var key = FilenameImportPlanner.CreateKey(Registration().Id, known, new(2026,9,3));
        var plan = FilenameImportPlanner.Build(Registration("tracked", key), Inventory(known, File("20260903_130000.jpg")));
        Assert.Single(plan.Candidates); Assert.Equal("20260903_130000.jpg", plan.Candidates[0].Source.FileName);
    }
    [Fact] public void UnknownName_IsSkipped_WhileValidNewFileRemainsEligible()
    {
        var plan = FilenameImportPlanner.Build(Registration(), Inventory(File("IMG_1234.jpg"), File("20260904_120000.jpg")));
        Assert.True(plan.Complete); Assert.Single(plan.Candidates); Assert.Single(plan.Rows,r=>r.Decision=="skipped");
    }
    [Fact] public void UnknownEligibleSizeAndDuplicatePathsBlockPlan()
    {
        var file = File("20260904_120000.jpg", null);
        Assert.False(FilenameImportPlanner.Build(Registration(), Inventory(file)).Complete);
        Assert.False(FilenameImportPlanner.Build(Registration(), Inventory(file, file)).Complete);
    }
    [Fact] public void OldUnknownSizeDoesNotBlockPlan() => Assert.True(FilenameImportPlanner.Build(Registration(), Inventory(File("20260901_120000.jpg", null))).Complete);
    [Fact] public void ByteOverflowAndBasenameMismatchBlockPlan()
    {
        Assert.False(FilenameImportPlanner.Build(Registration(),Inventory(File("20260904_1.jpg",long.MaxValue),File("20260904_2.jpg",1))).Complete);
        Assert.False(FilenameImportPlanner.Build(Registration(),Inventory(File("20260904_1.jpg") with {FileName="other.jpg"})).Complete);
    }
    [Fact] public void RegistrationSeparatesKeysAndIgnoresObjectHandle()
    {
        var file = File("20260904_120000.jpg"); var id = Registration().Id;
        var key = FilenameImportPlanner.CreateKey(id, file, new(2026,9,4));
        Assert.Equal(key, FilenameImportPlanner.CreateKey(id, file with { ObjectId="new" }, new(2026,9,4)));
        Assert.NotEqual(key, FilenameImportPlanner.CreateKey(Guid.NewGuid(), file, new(2026,9,4)));
    }
}
