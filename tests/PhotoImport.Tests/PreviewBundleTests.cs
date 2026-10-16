using PhotoImport.Models;
using PhotoImport.Reporting;
using PhotoImport.Services;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public sealed class PreviewBundleTests:IDisposable
{
    private readonly string parent=Path.Combine(Path.GetTempPath(),"photo-preview-"+Guid.NewGuid().ToString("N"));
    private string Target=>Path.Combine(parent,"new bundle ü");
    public PreviewBundleTests()=>Directory.CreateDirectory(parent);
    public void Dispose()=>Directory.Delete(parent,true);

    [Fact]
    public void ExistingEmptyDirectoryAndExistingFileAreRefusedUnchanged()
    {
        Directory.CreateDirectory(Target);Assert.Throws<IOException>(()=>PreviewBundle.Reserve(Target));Assert.Empty(Directory.GetFileSystemEntries(Target));
        var file=Path.Combine(parent,"existing");File.WriteAllText(file,"keep");Assert.Throws<IOException>(()=>PreviewBundle.Reserve(file));Assert.Equal("keep",File.ReadAllText(file));
    }

    [Fact]
    public async Task ConcurrentReservationsHaveExactlyOneWinner()
    {
        async Task<PreviewBundle?> TryReserve()=>await Task.Run(()=>{try{return PreviewBundle.Reserve(Target);}catch(IOException){return null;}});
        var results=await Task.WhenAll(TryReserve(),TryReserve());
        Assert.Single(results,x=>x is not null);foreach(var result in results)result?.Dispose();
        Assert.Equal(4,Directory.GetFiles(Target).Length);
    }

    [Fact]
    public void MissingParentDoesNotCreateDirectories()
    {var missing=Path.Combine(parent,"missing","new");Assert.Throws<IOException>(()=>PreviewBundle.Reserve(missing));Assert.False(Directory.Exists(Path.GetDirectoryName(missing)));}

    [Fact]
    public void RealBundleReopensThroughOfflineReaders()
    {
        var inventory=BoundedCaptureCollectorTests.Inventory("a.jpg");using var device=BoundedCaptureCollectorTests.Content(inventory);
        var capture=BoundedCaptureCollector.Run(device,inventory,1,default).Capture;
        var checkpoint=new DayCheckpoint(1,"phone",new(2026,10,10),"completedDay",[]);
        var report=DayPreviewPlanner.Build(inventory,[new(2,inventory.Device,"complete",capture.Files)],checkpoint,default);
        using(var bundle=PreviewBundle.Reserve(Target))
        {
            bundle.WriteInventory(inventory);bundle.WriteCapture(capture);bundle.WritePreview(report);
            bundle.WriteManifest(new(){RequestedRoots=[@"\Camera"],ResolvedRoots=[@"\Camera"],MaxPhotos=1,AttemptedCount=1,CompletionStatus="complete"});
            Assert.Throws<InvalidOperationException>(()=>bundle.WriteInventory(inventory));
        }
        var readInventory=PreviewInputReader.ReadInventory(Path.Combine(Target,"inventory.json"),default);
        var readCapture=PreviewInputReader.ReadCapture(Path.Combine(Target,"capture.json"),default);
        var replan=DayPreviewPlanner.Build(readInventory,[readCapture],checkpoint,default);
        Assert.True(replan.IsComplete);Assert.Equal(1,replan.SelectedCount);Assert.Equal(1000,replan.SelectedKnownBytes);
        Assert.Equal(@"\Camera\a.jpg",Assert.Single(replan.Rows).SourcePath);
    }

    [Fact]
    public void ReservationFailureClosesAllEarlierHandlesAndPreservesPrimaryError()
    {
        var streams=new List<BrokenStream>();
        var ex=Assert.Throws<IOException>(()=>PreviewBundle.Reserve(Target,_=>
        {if(streams.Count==2)throw new IOException("reserve failure");var stream=new BrokenStream{FailDispose=streams.Count==0};streams.Add(stream);return stream;}));
        Assert.Contains("reserve failure",ex.Message);Assert.All(streams,s=>Assert.True(s.Closed));Assert.True(Directory.Exists(Target));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void WriteOrFlushFailureDoesNotPreventOtherExports(bool flush)
    {
        var streams=new Dictionary<string,BrokenStream>();
        using var bundle=PreviewBundle.Reserve(Target,path=>{var stream=new BrokenStream();streams[Path.GetFileName(path)]=stream;return stream;});
        Assert.Equal(4,streams.Count);
        streams["capture.json"].FailWrite=!flush;streams["capture.json"].FailFlush=flush;
        bundle.WriteInventory(BoundedCaptureCollectorTests.Inventory());
        Assert.Throws<IOException>(()=>bundle.WriteCapture(new(){Device=FakePhotoDevice.Info(),RequestedPaths=[]}));
        bundle.WriteManifest(new(){MaxPhotos=int.MaxValue});
        Assert.True(streams["inventory.json"].Length>0);Assert.True(streams["run.json"].Length>0);
    }

    [Fact]
    public void DisposalContinuesAfterAHandleCloseFails()
    {
        var streams=new List<BrokenStream>();var bundle=PreviewBundle.Reserve(Target,_=>{var s=new BrokenStream{FailDispose=streams.Count==0};streams.Add(s);return s;});
        Assert.Throws<IOException>(bundle.Dispose);Assert.All(streams,s=>Assert.True(s.Closed));bundle.Dispose();
    }

    private sealed class BrokenStream:MemoryStream
    {
        public bool FailWrite,FailFlush,FailDispose,Closed;
        public override void Write(byte[] buffer,int offset,int count){if(FailWrite)throw new IOException("write failed");base.Write(buffer,offset,count);}
        public override void Write(ReadOnlySpan<byte> buffer){if(FailWrite)throw new IOException("write failed");base.Write(buffer);}
        public override void Flush(){if(FailFlush)throw new IOException("flush failed");base.Flush();}
        protected override void Dispose(bool disposing){Closed=true;base.Dispose(disposing);if(FailDispose)throw new IOException("close failed");}
    }
}
