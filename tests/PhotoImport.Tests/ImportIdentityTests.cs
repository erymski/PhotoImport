using PhotoImport.Importing;
using PhotoImport.Models;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public class ImportIdentityTests
{
    internal static DeviceInfo Phone(string serial = "123", string id = "phone") =>
        FakePhotoDevice.Info(id) with
        {
            Identifiers = new Dictionary<string, string?> { { "SerialNumber", serial } },
        };

    [Fact]
    public void SameFriendlyName_DifferentSerial_DoesNotMatch()
    {
        var identity = ImportIdentity.Enroll(Phone());
        Assert.False(ImportIdentity.Matches(identity, Phone("other")));
        Assert.False(ImportIdentity.Matches(identity, Phone("")));
        Assert.True(ImportIdentity.Matches(identity, Phone(id: "different-transport-id")));
        Assert.False(ImportIdentity.Matches(identity, Phone() with { Model = null }));
    }

    [Fact]
    public void FallbackRequiresExactDeviceId()
    {
        var identity = ImportIdentity.Enroll(FakePhotoDevice.Info());
        Assert.True(ImportIdentity.Matches(identity, FakePhotoDevice.Info()));
        Assert.False(ImportIdentity.Matches(identity, FakePhotoDevice.Info("different")));
    }

    [Fact]
    public void DuplicatePhysicalMatches_RequireUnambiguousSelection()
    {
        var r = FilenameImportPlannerTests.Registration() with
        {
            Identity = ImportIdentity.Enroll(Phone()),
        };
        Assert.Throws<ImportException>(() =>
            ImportIdentity.Select([r], [Phone(), Phone(id: "other")], null)
        );
        Assert.Throws<ImportException>(() =>
            ImportIdentity.Select([r], [Phone("different")], r.Alias)
        );
    }
}

public class ScopedMediaInventoryTests
{
    internal const string Root = "\\Internal storage\\DCIM\\Camera";

    internal static FakePhotoDevice Device()
    {
        var d = new FakePhotoDevice();
        var root = FakePhotoDevice.Entry("camera", Root, true);
        d.Roots.Add(root);
        d.Children["camera"] = [FakePhotoDevice.Entry("video", Root + "\\20260904_1.MP4")];
        return d;
    }

    [Fact]
    public void IncludesVideosAndSkipsTrash()
    {
        var d = Device();
        d.Children["camera"] =
        [
            .. d.Children["camera"],
            FakePhotoDevice.Entry("trash", Root + "\\.trash", true),
        ];
        d.Failures["trash"] = new IOException("must not inspect");
        var result = ScopedMediaInventory.Read(d, [Root], default);
        Assert.True(result.Complete);
        Assert.Single(result.Files);
        Assert.EndsWith(".MP4", result.Files[0].FileName);
    }

    [Theory]
    [InlineData("\\Internal storage")]
    [InlineData("\\Internal storage\\DCIM\\CameraOther")]
    [InlineData("\\Internal storage\\Pictures")]
    public void RejectsUnapprovedRoots(string root) =>
        Assert.ThrowsAny<Exception>(() => ScopedMediaInventory.ValidateRoots([root]));

    [Fact]
    public void DuplicateAndEscapedChildrenMakeInventoryIncomplete()
    {
        var d = Device();
        d.Children["camera"] = [.. d.Children["camera"], .. d.Children["camera"]];
        Assert.False(ScopedMediaInventory.Read(d, [Root], default).Complete);
        d.Children["camera"] = [FakePhotoDevice.Entry("escape", "\\Internal storage\\other.jpg")];
        Assert.False(ScopedMediaInventory.Read(d, [Root], default).Complete);
    }

    [Fact]
    public void MissingRootAndMetadataFailureCannotBecomeComplete()
    {
        var d = Device();
        d.Failures["video"] = new IOException("failed");
        Assert.False(ScopedMediaInventory.Read(d, [Root], default).Complete);
        Assert.ThrowsAny<Exception>(() =>
            ScopedMediaInventory.Read(d, [Root + "\\absent"], default)
        );
    }
}
