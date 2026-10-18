using PhotoImport.Importing;
using PhotoImport.Devices;
using PhotoImport.Tests.Fakes;
namespace PhotoImport.Tests;
public class ImportDestinationTests
{
    [Fact] public void PreservesBasenameAndExistingCollisions()
    {
        using var f=new ImportFixture();var c=Candidate();var dest=new ImportDestination(f.Destination);
        Assert.Equal(c.Source.FileName,Path.GetFileName(dest.ChooseFinalPath(c)));
        File.WriteAllText(Path.Combine(f.Destination,c.Source.FileName),"unrelated");
        Assert.NotEqual(c.Source.FileName,Path.GetFileName(dest.ChooseFinalPath(c)));
        Assert.Equal("unrelated",File.ReadAllText(Path.Combine(f.Destination,c.Source.FileName)));
    }
    [Theory][InlineData("CON.jpg")][InlineData("a.jpg:stream")][InlineData("..\\a.jpg")]
    public void RejectsUnsafeBasenames(string name)
    {
        using var f=new ImportFixture();Assert.ThrowsAny<Exception>(()=>new ImportDestination(f.Destination).ChooseFinalPath(Candidate() with {Source=FilenameImportPlannerTests.File(name) with {FileName=name}}));
    }
    internal static ImportCandidate Candidate()=>FilenameImportPlanner.Build(FilenameImportPlannerTests.Registration(),FilenameImportPlannerTests.Inventory(FilenameImportPlannerTests.File("20260904_1.mp4",3))).Candidates.Single();
}
public class MediaTransferTests
{
    internal static PendingImport Session(ImportFixture f)
    {
        var candidate=ImportDestinationTests.Candidate();var id=Guid.NewGuid();var dest=new ImportDestination(f.Destination);
        return new(1,id,f.Registration,[new(candidate,dest.ChooseFinalPath(candidate),dest.CreatePartialPath(id,candidate.Key))]);
    }
    internal static FakeImportDevice Device(ImportCandidate c)
    {
        var d=new FakeImportDevice();d.Metadata.Metadata[c.Source.ObjectId]=c.Source;d.Contents[c.Source.SourcePath]=[1,2,3];return d;
    }
    [Fact] public void CopiesToEofWithVerifiedLengthsAndHash()
    {
        using var f=new ImportFixture();var session=Session(f);var device=Device(session.Files[0].Candidate);PendingImport? persisted=null;
        var result=new MediaTransfer().Copy(device,session,session.Files[0],p=>persisted=p,default);
        Assert.Equal("completed",result.Files[0].Stage);Assert.Equal(3,result.Files[0].Length);
        Assert.Equal(new byte[]{1,2,3},File.ReadAllBytes(result.Files[0].FinalPath));Assert.Equal("completed",persisted!.Files[0].Stage);
        Assert.False(File.Exists(result.Files[0].PartialPath));
    }
    [Fact] public void LengthMismatchNeverFinalizes()
    {
        using var f=new ImportFixture();var session=Session(f);var device=Device(session.Files[0].Candidate);
        device.Contents[session.Files[0].Candidate.Source.SourcePath]=[1];
        Assert.Throws<ImportException>(()=>new MediaTransfer().Copy(device,session,session.Files[0],_=>{},default));
        Assert.False(File.Exists(session.Files[0].FinalPath));
    }
    [Theory][InlineData(TransferPoint.AfterPrepared)][InlineData(TransferPoint.AfterRename)]
    public void PreparedCrashRecoversWithoutAnotherUsbRead(TransferPoint point)
    {
        using var f=new ImportFixture();var session=Session(f);var d=Device(session.Files[0].Candidate);var durable=session;
        var broken=new MediaTransfer(fault:p=>{if(p==point)throw new IOException("power loss");});
        Assert.Throws<IOException>(()=>broken.Copy(d,session,session.Files[0],p=>durable=p,default));
        var recovered=new MediaTransfer().TryRecover(durable,durable.Files[0],p=>durable=p,default);
        Assert.Equal("completed",recovered.Files[0].Stage);Assert.Single(d.Opened);
    }
    [Fact] public void CollisionAfterPreparedPreservesRacingFile()
    {
        using var f=new ImportFixture();var session=Session(f);var d=Device(session.Files[0].Candidate);
        var transfer=new MediaTransfer(fault:p=>{if(p==TransferPoint.AfterPrepared)File.WriteAllText(session.Files[0].FinalPath,"racer");});
        var result=transfer.Copy(d,session,session.Files[0],_=>{},default);
        Assert.Equal("racer",File.ReadAllText(session.Files[0].FinalPath));Assert.NotEqual(session.Files[0].FinalPath,result.Files[0].FinalPath);
    }
    [Fact] public void LowSpaceAndCancelledReadsNeverOpenSource()
    {
        using var f=new ImportFixture();var session=Session(f);var d=Device(session.Files[0].Candidate);
        Assert.Throws<IOException>(()=>new MediaTransfer(_=>2L*1024*1024*1024).Copy(d,session,session.Files[0],_=>{},default));
        Assert.Throws<OperationCanceledException>(()=>new MediaTransfer().Copy(d,session,session.Files[0],_=>{},new(true)));
        Assert.Empty(d.Opened);
    }
    [Fact] public void ChangedSourceAndChangedLocalHashCannotBeReused()
    {
        using var f=new ImportFixture();var session=Session(f);var d=Device(session.Files[0].Candidate);
        d.OnOpen=path=>new(new(path,99,null),new MemoryStream([1,2,3]));
        Assert.Throws<ImportException>(()=>new MediaTransfer().Copy(d,session,session.Files[0],_=>{},default));
        d.OnOpen=null;var complete=new MediaTransfer().Copy(d,session,session.Files[0],_=>{},default);
        File.WriteAllBytes(complete.Files[0].FinalPath,[4,5,6]);
        var recovered=new MediaTransfer().TryRecover(complete,complete.Files[0],_=>{},default);
        Assert.Equal("planned",recovered.Files[0].Stage);Assert.NotEqual(complete.Files[0].FinalPath,recovered.Files[0].FinalPath);
    }
    [Fact] public void StagingJunctionIsRejectedWithoutTouchingTarget()
    {
        using var f=new ImportFixture();var session=Session(f);var d=Device(session.Files[0].Candidate);
        var target=Path.Combine(f.Root,"unrelated");Directory.CreateDirectory(target);File.WriteAllText(Path.Combine(target,"keep.txt"),"keep");
        var link=Path.Combine(f.Destination,".phone-photo-sync");
        var start=new System.Diagnostics.ProcessStartInfo("cmd.exe"){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true};
        start.ArgumentList.Add("/c");start.ArgumentList.Add("mklink");start.ArgumentList.Add("/J");start.ArgumentList.Add(link);start.ArgumentList.Add(target);
        using var process=System.Diagnostics.Process.Start(start)!;process.WaitForExit();Assert.Equal(0,process.ExitCode);
        Assert.Throws<IOException>(()=>new MediaTransfer().Copy(d,session,session.Files[0],_=>{},default));
        Assert.Equal("keep",File.ReadAllText(Path.Combine(target,"keep.txt")));Assert.Empty(d.Opened);
    }
}
