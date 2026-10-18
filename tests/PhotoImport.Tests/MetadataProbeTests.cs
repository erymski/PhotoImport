using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Services;
using PhotoImport.Tests.Fakes;
using static PhotoImport.Tests.Fakes.FakePhotoDevice;

namespace PhotoImport.Tests;

public class MetadataProbeTests
{
    [Fact]
    public void FindsCameraAndPicturesAcrossStorageRoots()
    {
        using var device = new FakePhotoDevice();
        var a = Entry("a", "\\Internal", true);
        var b = Entry("b", "\\SD", true);
        var dcim = Entry("dcim", "\\Internal\\DCIM", true);
        var pictures = Entry("pics", "\\SD\\Pictures", true);
        var vendor = Entry("vendor", "\\SD\\Vendor", true);
        device.Roots.AddRange([a, b]);
        device.Children[a.ObjectId] = [dcim];
        device.Children[b.ObjectId] = [pictures, vendor];
        device.Children[dcim.ObjectId] =
        [
            Entry("1", "\\Internal\\DCIM\\a.JPG"),
            Entry("video", "\\Internal\\DCIM\\clip.mp4"),
        ];
        device.Children[pictures.ObjectId] = [Entry("2", "\\SD\\Pictures\\b.HEIC")];
        device.Children[vendor.ObjectId] =
        [
            Entry("3", "\\SD\\Vendor\\c.png"),
            Entry("txt", "\\SD\\Vendor\\note.txt"),
        ];
        var result = MetadataProbe.Run(device, Info(), default);
        Assert.Equal("complete", result.CompletionStatus);
        Assert.Equal(3, result.PhotoCount);
        Assert.Equal(3, result.WithoutCaptureDateCount);
        Assert.Equal(["\\Internal", "\\SD"], result.SearchedRoots);
    }

    [Fact]
    public void RepeatedObjectAndCycleAreVisitedOnce()
    {
        using var device = new FakePhotoDevice();
        var a = Entry("a", "\\A", true);
        var b = Entry("b", "\\B", true);
        var photo = Entry("photo", "\\A\\a.jpg");
        device.Roots.AddRange([a, b]);
        device.Children["a"] = [a, b, photo, photo];
        device.Children["b"] = [a, Entry("second", "\\B\\a.jpg")];
        var result = MetadataProbe.Run(device, Info(), default);
        Assert.Equal(2, result.PhotoCount);
        Assert.Equal("complete", result.CompletionStatus);
    }

    [Fact]
    public void DirectoryFailurePreservesOtherBranches()
    {
        using var device = new FakePhotoDevice();
        device.Roots.AddRange([Entry("bad", "\\Bad", true), Entry("ok", "\\Good", true)]);
        device.Failures["bad"] = new UnauthorizedAccessException("Denied");
        device.Children["ok"] = [Entry("p", "\\Good\\a.jpg")];
        var result = MetadataProbe.Run(device, Info(), default);
        Assert.Equal("incomplete", result.CompletionStatus);
        Assert.Single(result.Photos);
        var error = Assert.Single(result.Errors);
        Assert.Equal("\\Bad", error.Path);
        Assert.Equal("bad", error.ObjectId);
        Assert.Equal("enumerate", error.Operation);
    }

    [Fact]
    public void PropertyFailurePreservesPhoto()
    {
        using var device = SimpleDevice();
        var file = Entry("p", "\\DCIM\\a.jpg");
        device.Metadata["p"] = Photo(file) with
        {
            DateAuthored = new("dateAuthored", null, null, null, "unknown", "error", "Unsupported"),
        };
        var result = MetadataProbe.Run(device, Info(), default);
        Assert.Single(result.Photos);
        Assert.Equal(1, result.WithoutCaptureDateCount);
        Assert.Equal("incomplete", result.CompletionStatus);
        Assert.Contains(
            result.Errors,
            e => e.Operation == "dateAuthored" && e.Message == "Unsupported"
        );
    }

    [Theory]
    [InlineData(false, "incomplete")]
    [InlineData(true, "cancelled")]
    public void DisconnectOrCancellationPreservesPartialInventory(bool cancel, string expected)
    {
        using var cts = new CancellationTokenSource();
        using var device = SimpleDevice();
        device.Children["root"] = [Entry("p", "\\DCIM\\a.jpg"), Entry("q", "\\DCIM\\b.jpg")];
        device.OnRead = file =>
        {
            if (file.ObjectId == "p" && cancel)
                cts.Cancel();
        };
        if (!cancel)
            device.Failures["q"] = new DeviceDisconnectedException("Unplugged");
        var result = MetadataProbe.Run(device, Info(), cts.Token);
        Assert.Single(result.Photos);
        Assert.Equal(expected, result.CompletionStatus);
    }

    [Fact]
    public void FailureDuringChildEnumerationKeepsEarlierPhoto()
    {
        using var device = SimpleDevice();
        device.ChildrenIterator = _ => Children();
        static IEnumerable<DeviceEntry> Children()
        {
            yield return Entry("p", "\\DCIM\\a.jpg");
            throw new IOException("Folder enumeration failed after one image");
        }
        var result = MetadataProbe.Run(device, Info(), default);
        Assert.Equal("incomplete", result.CompletionStatus);
        Assert.Single(result.Photos);
        Assert.Contains(result.Errors, e => e.Message.Contains("after one image"));
    }

    [Fact]
    public void CancellationDuringChildEnumerationKeepsEarlierPhoto()
    {
        using var device = SimpleDevice();
        using var cts = new CancellationTokenSource();
        device.ChildrenIterator = _ => Children();
        IEnumerable<DeviceEntry> Children()
        {
            yield return Entry("p", "\\DCIM\\a.jpg");
            cts.Cancel();
            cts.Token.ThrowIfCancellationRequested();
        }
        var result = MetadataProbe.Run(device, Info(), cts.Token);
        Assert.Equal("cancelled", result.CompletionStatus);
        Assert.Single(result.Photos);
    }

    [Fact]
    public void CancellationDuringDisconnectKeepsCancelledStatus()
    {
        using var device = SimpleDevice();
        using var cts = new CancellationTokenSource();
        device.OnRead = _ =>
        {
            cts.Cancel();
            throw new DeviceDisconnectedException("Device disappeared during cancellation");
        };
        var result = MetadataProbe.Run(device, Info(), cts.Token);
        Assert.Equal("cancelled", result.CompletionStatus);
        Assert.Contains(result.Errors, e => e.Message.Contains("disappeared"));
    }

    [Fact]
    public void EmptyDeviceCompletes()
    {
        using var device = new FakePhotoDevice();
        var result = MetadataProbe.Run(device, Info(), default);
        Assert.Empty(result.Photos);
        Assert.Equal("complete", result.CompletionStatus);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("MP4")]
    public void UnknownOrContradictoryFormatKeepsImageWithWarning(string? format)
    {
        using var device = SimpleDevice();
        var file = Entry("p", "\\DCIM\\a.jpg");
        device.Metadata["p"] = Photo(file) with
        {
            Format = format,
            ContentType = format is null ? null : "Video",
        };
        var result = MetadataProbe.Run(device, Info(), default);
        Assert.NotEmpty(Assert.Single(result.Photos).Warnings);
    }

    [Fact]
    public void MissingObjectIdsUsePathsAndWarn()
    {
        using var device = SimpleDevice();
        device.Children["root"] = [Entry("", "\\DCIM\\a.jpg"), Entry("", "\\DCIM\\b.jpg")];
        var result = MetadataProbe.Run(device, Info(), default);
        Assert.Equal(2, result.PhotoCount);
        Assert.NotEmpty(result.Warnings);
    }

    public static FakePhotoDevice SimpleDevice()
    {
        var device = new FakePhotoDevice();
        device.Roots.Add(Entry("root", "\\DCIM", true));
        device.Children["root"] = [Entry("p", "\\DCIM\\a.jpg")];
        return device;
    }
}
