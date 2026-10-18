using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Services;
using PhotoImport.Tests.Fakes;
using static PhotoImport.Tests.Fakes.FakePhotoDevice;

namespace PhotoImport.Tests;

public class BoundedCaptureCollectorTests
{
    internal static ProbeReport Inventory(params string[] names)=>new(Info(),DateTimeOffset.UtcNow,TimeSpan.Zero,"complete",[@"\Camera"],
        names.Select((n,i)=>Photo(Entry(i.ToString(),@"\Camera\"+n)) with {SizeBytes=1000,PersistentObjectId="id-"+n}).ToArray(),[]);
    internal static FakeContentDevice Content(ProbeReport inventory)
    {
        var device=new FakeContentDevice();
        foreach(var p in inventory.Photos)device.Content[p.SourcePath]=()=>new(new(p.SourcePath,p.SizeBytes,p.PersistentObjectId),new ForwardOnlyStream(JpegFixture.Create()));
        return device;
    }

    [Fact]
    public void StableOrderAndFailedOpenConsumeBudget()
    {
        var inventory=Inventory("c.jpg","a.jpg","b.jpg");using var device=Content(inventory);
        device.Content[@"\Camera\a.jpg"]=()=>throw new IOException("file unavailable");
        var result=BoundedCaptureCollector.Run(device,inventory,2,default);
        Assert.Equal([@"\Camera\a.jpg",@"\Camera\b.jpg"],device.Opened);
        Assert.Equal(2,result.AttemptedCount);Assert.True(result.LimitReached);
        Assert.Equal(2,result.Capture.Files.Count);Assert.Equal("incomplete",result.Capture.CompletionStatus);
        Assert.Equal(new DateOnly(2026,10,15),result.Capture.Files[1].Observation.CaptureDay);
    }

    [Fact]
    public void UnsupportedUnknownAndCaseDuplicateSourcesAreNotOpened()
    {
        var inventory=Inventory("a.png","b.tif","c.jpg","D.jpg","d.JPG");
        inventory=inventory with {Photos=inventory.Photos.Select(p=>p.FileName=="c.jpg"?p with {SizeBytes=null}:p).ToArray()};
        using var device=Content(inventory);var result=BoundedCaptureCollector.Run(device,inventory,3,default);
        Assert.Empty(device.Opened);Assert.Empty(result.Capture.Files);Assert.False(result.LimitReached);
    }

    [Theory][InlineData(0)][InlineData(-1)]
    public void InvalidLimitDoesNotOpen(int limit)
    {var inventory=Inventory("a.jpg");using var device=Content(inventory);Assert.Throws<ArgumentOutOfRangeException>(()=>BoundedCaptureCollector.Run(device,inventory,limit,default));Assert.Empty(device.Opened);}

    [Theory][InlineData("path")][InlineData("size")][InlineData("id")]
    public void ChangedSourceConsumesAttemptWithoutReading(string changed)
    {
        var inventory=Inventory("a.jpg");using var device=Content(inventory);var stream=new ForwardOnlyStream(JpegFixture.Create());
        device.Content[@"\Camera\a.jpg"]=()=>new(new(changed=="path"?@"\Other\a.jpg":@"\Camera\a.jpg",changed=="size"?999ul:1000ul,changed=="id"?"different":"id-a.jpg"),stream);
        var result=BoundedCaptureCollector.Run(device,inventory,1,default);
        Assert.Equal(1,result.AttemptedCount);Assert.Equal(0,stream.Consumed);Assert.True(stream.Disposed);
        Assert.Equal("incomplete",Assert.Single(result.Capture.Files).Observation.Outcome);Assert.False(result.LimitReached);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void CancellationOrDisconnectRetainsFirstAndStops(bool cancel)
    {
        var inventory=Inventory("a.jpg","b.jpg","c.jpg");using var device=Content(inventory);using var cts=new CancellationTokenSource();
        device.Content[@"\Camera\b.jpg"]=()=>new(new(@"\Camera\b.jpg",1000,"id-b.jpg"),new ForwardOnlyStream(JpegFixture.Create())
            {OnRead=()=>{if(cancel)cts.Cancel();throw cancel?new OperationCanceledException(cts.Token):new DeviceDisconnectedException("unplugged");}});
        var result=BoundedCaptureCollector.Run(device,inventory,3,cts.Token);
        Assert.Equal(2,result.AttemptedCount);Assert.Equal(2,result.Capture.Files.Count);
        Assert.Equal(new DateOnly(2026,10,15),result.Capture.Files[0].Observation.CaptureDay);
        Assert.Equal(!cancel,result.Disconnected);Assert.Equal(cancel?"cancelled":"incomplete",result.Capture.CompletionStatus);
    }

    [Fact]
    public void ParserAndCleanupFailuresRetainBudgetAndCloseStreams()
    {
        var inventory=Inventory("a.jpg","b.jpg");using var device=Content(inventory);
        var a=new ForwardOnlyStream([1,2,3]);var b=new ForwardOnlyStream(JpegFixture.Create()){DisposeError=new IOException("close failed")};
        device.Content[@"\Camera\a.jpg"]=()=>new(new(@"\Camera\a.jpg",1000,"id-a.jpg"),a);
        device.Content[@"\Camera\b.jpg"]=()=>new(new(@"\Camera\b.jpg",1000,"id-b.jpg"),b);
        var result=BoundedCaptureCollector.Run(device,inventory,2,default);
        Assert.True(a.Disposed&&b.Disposed);Assert.Equal(2,result.AttemptedCount);Assert.False(result.LimitReached);
        Assert.Equal("incomplete",result.Capture.CompletionStatus);Assert.Contains(result.Capture.Errors,e=>e.Contains("close failed"));
    }

    [Fact]
    public void TotalApplicationBytesStayCappedAndSessionRemainsOwnedByCaller()
    {
        var inventory=Inventory("a.dng","b.heic","c.dng");using var device=Content(inventory);var streams=new List<ForwardOnlyStream>();
        foreach(var p in inventory.Photos){var stream=new ForwardOnlyStream(new byte[400000]);streams.Add(stream);device.Content[p.SourcePath]=()=>new(new(p.SourcePath,1000,p.PersistentObjectId),stream);}
        var result=BoundedCaptureCollector.Run(device,inventory,3,default);
        Assert.Equal(786432,result.ApplicationBytesRead);Assert.All(streams,s=>{Assert.Equal(262144,s.Consumed);Assert.True(s.Disposed);});Assert.False(device.Disposed);
    }

    [Fact]
    public void PartialInventoryCannotProduceCompleteCapture()
    {var inventory=Inventory("20100101.jpg") with {CompletionStatus="incomplete"};using var device=Content(inventory);var result=BoundedCaptureCollector.Run(device,inventory,1,default);Assert.Single(device.Opened);Assert.Equal("incomplete",result.Capture.CompletionStatus);}

    [Fact]
    public void ExistingInvalidExactTimePolicyStillAllowsAvailableDay()
    {
        var inventory=Inventory("a.jpg");using var device=Content(inventory);
        device.Content[@"\Camera\a.jpg"]=()=>new(new(@"\Camera\a.jpg",1000,"id-a.jpg"),new ForwardOnlyStream(JpegFixture.Create(offset:"invalid")));
        var result=BoundedCaptureCollector.Run(device,inventory,1,default);var observation=Assert.Single(result.Capture.Files).Observation;
        Assert.Equal("invalid",observation.Outcome);Assert.Equal("available",observation.CaptureDayStatus);
        Assert.Equal(new DateOnly(2026,10,15),observation.CaptureDay);Assert.Equal("complete",result.Capture.CompletionStatus);
    }
}
