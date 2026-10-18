using System.Text.Json;
using PhotoImport.Importing;
using PhotoImport.Tests.Fakes;
namespace PhotoImport.Tests;

public class ImportReportingTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public void ArchivedFinishTimeIsCapturedAfterWork(bool noCandidates)
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var p=ImportRunnerTests.Setup(f,store);
        if(noCandidates)p.Device.Metadata.Children["camera"]=[];
        var start=new DateTimeOffset(2026,10,18,12,0,0,TimeSpan.Zero);
        var calls=0;
        var result=new ImportRunner(store,new(),()=>calls++==0?start:start.AddMinutes(2)).Run(p,null,default);
        Assert.Equal(0,result.ExitCode);
        var path=Directory.GetFiles(Path.Combine(f.State,"history")).Single();
        using var archive=JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(start.AddMinutes(2),archive.RootElement.GetProperty("result").GetProperty("finished").GetDateTimeOffset());
    }
    [Fact] public void DryRunReservesDuplicateBasenamesWithoutWriting()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var p=ImportRunnerTests.Setup(f,store);
        var nested=FakePhotoDevice.Entry("nested",ScopedMediaInventoryTests.Root+"\\nested",true);
        var file=FilenameImportPlannerTests.File("20260904_1.mp4",3) with {ObjectId="other",SourcePath=nested.Path+"\\20260904_1.mp4"};
        p.Device.Metadata.Children["camera"]=[..p.Device.Metadata.Children["camera"],nested];
        p.Device.Metadata.Children["nested"]=[FakePhotoDevice.Entry(file.ObjectId,file.SourcePath)];p.Device.Metadata.Metadata[file.ObjectId]=file;
        var preview=new ImportRunner(store,new()).DryRun(p,null,default);
        Assert.Equal(2,preview.ProposedNames.Distinct(StringComparer.OrdinalIgnoreCase).Count());
        Assert.Empty(Directory.GetFileSystemEntries(f.Destination));Assert.Empty(p.Device.Opened);
    }
    [Fact] public void CommitRecoveryArchiveAlsoCapturesFinishAfterWork()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var p=ImportRunnerTests.Setup(f,store);
        var broken=new ImportStateStore(f.State,point=>{if(point==ImportWritePoint.BeforeArchive)throw new IOException("interrupted");});
        Assert.True(new ImportRunner(broken,new()).Run(p,null,default).CheckpointCommitted);
        var start=new DateTimeOffset(2026,10,18,12,0,0,TimeSpan.Zero);var calls=0;
        Assert.Equal(0,new ImportRunner(store,new(),()=>calls++==0?start:start.AddMinutes(2)).Run(p,null,default).ExitCode);
        using var archive=JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(Path.Combine(f.State,"history")).Single()));
        Assert.Equal(start.AddMinutes(2),archive.RootElement.GetProperty("result").GetProperty("finished").GetDateTimeOffset());
    }
    [Fact] public void IgnoredNonmediaIsCountedWithoutReadingMetadataOrContent()
    {
        using var f=new ImportFixture();var store=new ImportStateStore(f.State);var p=ImportRunnerTests.Setup(f,store);
        var nonmedia=FakePhotoDevice.Entry("note",ScopedMediaInventoryTests.Root+"\\notes.txt");
        p.Device.Metadata.Children["camera"]=[..p.Device.Metadata.Children["camera"],nonmedia];
        p.Device.Metadata.Failures["note"]=new IOException("must not inspect nonmedia");
        var result=new ImportRunner(store,new()).Run(p,null,default);
        Assert.Equal(0,result.ExitCode);
        using var archive=JsonDocument.Parse(File.ReadAllText(Directory.GetFiles(Path.Combine(f.State,"history")).Single()));
        Assert.Equal(1,archive.RootElement.GetProperty("result").GetProperty("ignoredNonmediaCount").GetInt32());
        using var output=new StringWriter();
        Assert.Equal(0,new ImportCli(output,new StringWriter(),store,new()).Run(["sync","--dry-run"],default,()=>p));
        Assert.Contains("1 nonmedia",output.ToString());Assert.Single(p.Device.Opened);
    }
}
