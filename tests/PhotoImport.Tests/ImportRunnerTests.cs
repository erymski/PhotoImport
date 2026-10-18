using PhotoImport.Importing;
using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Tests.Fakes;
namespace PhotoImport.Tests;
public class ImportRunnerTests
{
    internal sealed class Provider(FakeImportDevice device):IPhotoDeviceProvider
    {
        public FakeImportDevice Device=>device;
        public IReadOnlyList<DeviceInfo> Discover(CancellationToken token)=>[FakePhotoDevice.Info()];
        public IPhotoDevice Open(DeviceInfo info,CancellationToken token)=>device;
    }
    internal static Provider Setup(ImportFixture f,ImportStateStore store)
    {
        using(var held=store.AcquireLock())store.WriteConfiguration(f.Config);
        var device=new FakeImportDevice();var root=FakePhotoDevice.Entry("camera",ScopedMediaInventoryTests.Root,true);
        var file=FilenameImportPlannerTests.File("20260904_1.mp4",3);device.Metadata.Roots.Add(root);
        device.Metadata.Children["camera"]=[FakePhotoDevice.Entry(file.ObjectId,file.SourcePath)];
        device.Metadata.Metadata[file.ObjectId]=file;device.Contents[file.SourcePath]=[1,2,3];return new(device);
    }
    internal static ImportCheckpoint Checkpoint(ImportStateStore store){using var held=store.AcquireReadLock();return store.ReadConfiguration()!.Devices.Single().Checkpoint;}
    [Fact] public void CopiesThenTracksDay_SecondRunDoesNotRecopy()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var provider=Setup(f,store);var runner=new ImportRunner(store,new());
        var first=runner.Run(provider,null,default);Assert.Equal(0,first.ExitCode);Assert.True(first.CheckpointCommitted);Assert.Equal(1,first.Copied);
        Assert.Equal(new DateOnly(2026,9,4),Checkpoint(store).Day);Assert.Equal("tracked",Checkpoint(store).Mode);Assert.Single(Checkpoint(store).BoundaryKeys);
        Assert.Equal(0,runner.Run(provider,null,default).Copied);Assert.Single(provider.Device.Opened);
    }
    [Fact] public void TransferFailurePreservesCheckpointAndPendingJournal()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var provider=Setup(f,store);
        provider.Device.Contents.Values.First()[0]=5;provider.Device.OnOpen=path=>throw new IOException("unplugged");
        var result=new ImportRunner(store,new()).Run(provider,null,default);
        Assert.Equal(4,result.ExitCode);Assert.False(result.CheckpointCommitted);Assert.Equal(new DateOnly(2026,9,3),Checkpoint(store).Day);
        using var held=store.AcquireReadLock();Assert.NotNull(store.ReadPending(f.Registration.Id));
    }
    [Fact] public void DryRunCreatesNoFilesAndOpensNoContent()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var provider=Setup(f,store);
        var before=Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories).Order().ToArray();
        var preview=new ImportRunner(store,new()).DryRun(provider,null,default);
        Assert.Single(preview.Plan.Candidates);Assert.Empty(provider.Device.Opened);
        Assert.Equal(before,Directory.GetFiles(f.Root,"*",SearchOption.AllDirectories).Order().ToArray());
    }
    [Fact] public void LaterSameDayCaptureCopiesWithoutRecopyingKnownBoundary()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var p=Setup(f,store);var runner=new ImportRunner(store,new());
        Assert.Equal(0,runner.Run(p,null,default).ExitCode);
        var extra=FilenameImportPlannerTests.File("20260904_2.jpg",3);
        p.Device.Metadata.Children["camera"]=[..p.Device.Metadata.Children["camera"],FakePhotoDevice.Entry(extra.ObjectId,extra.SourcePath)];
        p.Device.Metadata.Metadata[extra.ObjectId]=extra;p.Device.Contents[extra.SourcePath]=[4,5,6];
        Assert.Equal(1,runner.Run(p,null,default).Copied);Assert.Equal(2,Checkpoint(store).BoundaryKeys.Count);Assert.Equal(2,p.Device.Opened.Count);
    }
}
public class ImportCommitRecoveryTests
{
    [Theory][InlineData(ImportWritePoint.AfterConfigReplace)][InlineData(ImportWritePoint.BeforeArchive)]
    public void ConfigAlreadyCommitted_ArchivesWithoutOpeningContent(ImportWritePoint point)
    {
        using var f=new ImportFixture();var normal=new ImportStateStore(f.State);var provider=ImportRunnerTests.Setup(f,normal);
        var broken=new ImportStateStore(f.State,p=>{if(p==point)throw new IOException("power loss");});
        var first=new ImportRunner(broken,new()).Run(provider,null,default);
        Assert.True(first.CheckpointCommitted);Assert.Equal(5,first.ExitCode);Assert.Single(provider.Device.Opened);
        var second=new ImportRunner(normal,new()).Run(provider,null,default);
        Assert.Equal(0,second.ExitCode);Assert.Single(provider.Device.Opened);
        using var held=normal.AcquireReadLock();Assert.Null(normal.ReadPending(f.Registration.Id));
    }
    [Fact] public void PreparedCrashRetriesWithoutAnotherTransfer()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var provider=ImportRunnerTests.Setup(f,store);
        var first=new ImportRunner(store,new(fault:p=>{if(p==TransferPoint.AfterRename)throw new IOException("crash");})).Run(provider,null,default);
        Assert.False(first.CheckpointCommitted);Assert.Equal(5,first.ExitCode);
        var second=new ImportRunner(store,new()).Run(provider,null,default);
        Assert.Equal(0,second.ExitCode);Assert.True(second.CheckpointCommitted);Assert.Single(provider.Device.Opened);
    }
    [Fact] public void MissingCompletedSourceCanCommit_ButMissingUnfinishedSourceBlocks()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var p=ImportRunnerTests.Setup(f,store);
        new ImportRunner(store,new(fault:point=>{if(point==TransferPoint.AfterRename)throw new IOException("crash");})).Run(p,null,default);
        p.Device.Metadata.Children["camera"]=[];
        Assert.Equal(0,new ImportRunner(store,new()).Run(p,null,default).ExitCode);Assert.Single(p.Device.Opened);
        // Separate unfinished session whose source disappeared.
        using var other=new ImportFixture();var otherStore=new ImportStateStore(other.State);var q=ImportRunnerTests.Setup(other,otherStore);
        q.Device.OnOpen=_=>throw new IOException("disconnect");new ImportRunner(otherStore,new()).Run(q,null,default);
        q.Device.Metadata.Children["camera"]=[];
        Assert.Equal(4,new ImportRunner(otherStore,new()).Run(q,null,default).ExitCode);
        Assert.Equal(new DateOnly(2026,9,3),ImportRunnerTests.Checkpoint(otherStore).Day);
    }
    [Fact] public void BeforeConfigFailureLeavesBaseCheckpoint_AndChangedConfigRefusesResume()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var p=ImportRunnerTests.Setup(f,store);
        var broken=new ImportStateStore(f.State,point=>{if(point==ImportWritePoint.BeforeConfigReplace)throw new IOException("disk full");});
        Assert.False(new ImportRunner(broken,new()).Run(p,null,default).CheckpointCommitted);
        Assert.Equal(new DateOnly(2026,9,3),ImportRunnerTests.Checkpoint(store).Day);
        using(var held=store.AcquireLock())store.WriteConfiguration(f.Config with {Devices=[f.Registration with {Checkpoint=new(new(2026,9,2),"completedDay",[])}]});
        Assert.Equal(2,new ImportRunner(store,new()).Run(p,null,default).ExitCode);Assert.Single(p.Device.Opened);
    }
    [Fact] public void CancellationAtCommitBoundaryReportsCommittedState()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var p=ImportRunnerTests.Setup(f,store);using var cts=new CancellationTokenSource();
        var instrumented=new ImportStateStore(f.State,point=>{if(point==ImportWritePoint.AfterConfigReplace)cts.Cancel();});
        var result=new ImportRunner(instrumented,new()).Run(p,null,cts.Token);
        Assert.Equal(0,result.ExitCode);Assert.True(result.CheckpointCommitted);
    }
}
