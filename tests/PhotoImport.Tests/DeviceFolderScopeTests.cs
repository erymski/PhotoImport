using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Services;
using PhotoImport.Tests.Fakes;
using static PhotoImport.Tests.Fakes.FakePhotoDevice;

namespace PhotoImport.Tests;

public class DeviceFolderScopeTests
{
    private const string Camera = @"\Internal storage\DCIM\Camera";

    [Fact]
    public void CameraDoesNotIncludeCamera2OrAncestorFiles()
    {
        using var device = Tree();
        var reads = new List<string>(); var listings = new List<string>();
        device.OnRead = e => reads.Add(e.Path);
        device.ChildrenIterator = e => { listings.Add(e.Path); return device.Children.GetValueOrDefault(e.ObjectId) ?? []; };
        var roots = DeviceFolderScope.Resolve(device, [Camera.ToLowerInvariant()], default);
        var result = MetadataProbe.Run(device, Info(), roots, default);
        Assert.Equal([Camera], result.SearchedRoots);
        Assert.Equal([Camera + @"\a.jpg"], reads);
        Assert.DoesNotContain(Camera + "2", listings);
        Assert.Equal("complete", result.CompletionStatus);
    }

    [Theory]
    [InlineData(@"relative")][InlineData(@"\\server\folder")][InlineData(@"\A\..\B")]
    [InlineData(@"\A\*")][InlineData(@"\A/B")][InlineData(@"\A\\B")][InlineData(@"\A\")]
    public void InvalidRootIsRejected(string path) => Assert.Throws<PreviewInputException>(() => DeviceFolderScope.Validate([path]));

    [Theory]
    [InlineData(@"\A", @"\a")][InlineData(@"\A", @"\a\B")]
    public void OverlapIsRejected(string a, string b) => Assert.Throws<PreviewInputException>(() => DeviceFolderScope.Validate([a,b]));

    [Theory]
    [InlineData(@"\Internal storage\missing")]
    [InlineData(@"\Internal storage\note.jpg")]
    public void MissingOrFileRootFails(string path)
    { using var device=Tree(); Assert.Throws<IOException>(()=>DeviceFolderScope.Resolve(device,[path],default)); }

    [Fact]
    public void AmbiguousResolutionFails()
    {
        using var device=Tree(); device.Children["dcim"]=[Entry("x",Camera,true),Entry("y",Camera.ToLowerInvariant(),true)];
        Assert.Throws<IOException>(()=>DeviceFolderScope.Resolve(device,[Camera],default));
    }

    [Fact]
    public void EscapingChildAndMetadataAreNeverAccepted()
    {
        using var device=Tree(); var reads=new List<string>(); device.OnRead=e=>reads.Add(e.Path);
        device.Children["camera"]=[Entry("escape",Camera+@"2\escape.jpg"),Entry("p",Camera+@"\a.jpg")];
        device.Metadata["p"]=Photo(Entry("outside",@"\Outside\x.jpg"));
        var result=MetadataProbe.Run(device,Info(),[Entry("camera",Camera,true)],default);
        Assert.Empty(result.Photos); Assert.Equal([Camera+@"\a.jpg"],reads);
        Assert.Equal("incomplete",result.CompletionStatus); Assert.Equal(2,result.Errors.Count);
    }

    [Theory]
    [InlineData(false,false,"incomplete")][InlineData(true,false,"cancelled")][InlineData(false,true,"incomplete")]
    public void IteratorFailureRetainsEarlierRows(bool cancel,bool disconnect,string status)
    {
        using var device=Tree(); using var cts=new CancellationTokenSource();
        device.ChildrenIterator=e=>e.ObjectId=="camera"?Children():device.Children.GetValueOrDefault(e.ObjectId)??[];
        IEnumerable<DeviceEntry> Children()
        {
            yield return Entry("a",Camera+@"\a.jpg"); yield return Entry("b",Camera+@"\b.jpg");
            if(cancel){cts.Cancel();cts.Token.ThrowIfCancellationRequested();}
            if(disconnect)throw new DeviceDisconnectedException("gone");
            throw new IOException("partial folder");
        }
        var result=MetadataProbe.Run(device,Info(),[Entry("camera",Camera,true)],cts.Token);
        Assert.Equal(2,result.Photos.Count); Assert.Equal(status,result.CompletionStatus);
    }

    [Fact]
    public void CycleAndThumbnailsDoNotExpandTraversal()
    {
        using var device=Tree(); var root=Entry("camera",Camera,true);var thumb=Entry("thumb",Camera+@"\.thumbnails",true);
        device.Children["camera"]=[root,thumb,Entry("a",Camera+@"\a.jpg"),Entry("a",Camera+@"\a.jpg")];
        device.Children["thumb"]=[Entry("t",thumb.Path+@"\t.jpg")];
        var result=MetadataProbe.Run(device,Info(),[root],default);
        Assert.Single(result.Photos); Assert.NotEmpty(result.Warnings);
    }

    [Fact]
    public void InvalidChildSyntaxAndMismatchedMetadataNameRemainErrors()
    {
        using var device=Tree();var reads=new List<string>();device.OnRead=e=>reads.Add(e.Path);
        device.Children["camera"]=[Entry("slash",Camera+@"\elsewhere/file.jpg"),Entry("p",Camera+@"\a.jpg")];
        device.Metadata["p"]=Photo(Entry("p",Camera+@"\a.jpg")) with {FileName="different.jpg"};
        var report=MetadataProbe.Run(device,Info(),[Entry("camera",Camera,true)],default);
        Assert.Empty(report.Photos);Assert.Equal([Camera+@"\a.jpg"],reads);Assert.Equal(2,report.Errors.Count);
    }

    internal static FakePhotoDevice Tree()
    {
        var device=new FakePhotoDevice(); device.Roots.Add(Entry("device",@"\",true));
        device.Children["device"]=[Entry("storage",@"\Internal storage",true)];
        device.Children["storage"]=[Entry("dcim",@"\Internal storage\DCIM",true),Entry("note",@"\Internal storage\note.jpg")];
        device.Children["dcim"]=[Entry("camera",Camera,true),Entry("camera2",Camera+"2",true)];
        device.Children["camera"]=[Entry("a",Camera+@"\a.jpg")];
        device.Children["camera2"]=[Entry("other",Camera+@"2\other.jpg")]; return device;
    }
}
