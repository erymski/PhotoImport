using System.Text.Json;
using System.Text.Json.Nodes;
using PhotoImport.Cli;
using PhotoImport.Models;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public sealed class DayPreviewCliTests : IDisposable
{
    private readonly string temp = Path.Combine(Path.GetTempPath(),"PhotoImport-preview-"+Guid.NewGuid());
    private readonly StringWriter output=new(),error=new();
    private string InventoryPath=>Path.Combine(temp,"inventory.json");
    private string CapturePath=>Path.Combine(temp,"capture.json");
    private string OutputPath=>Path.Combine(temp,"preview.json");
    private static readonly JsonSerializerOptions JsonOptions=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase};
    public DayPreviewCliTests()
    {
        Directory.CreateDirectory(temp);
        File.WriteAllText(InventoryPath,JsonSerializer.Serialize(PreviewFixture.Inventory(PreviewFixture.Photo("\\new.jpg",30),PreviewFixture.Photo("\\old.jpg",20)),JsonOptions));
        var capture=new JpegExifReport{Device=FakePhotoDevice.Info(),RequestedPaths=["\\new.jpg","\\old.jpg"],CompletionStatus="complete"};
        capture.Files.Add(PreviewFixture.Capture("\\new.jpg","2026:10:11 00:00:00",30));
        capture.Files.Add(PreviewFixture.Capture("\\old.jpg","2026:10:10 00:00:00",20));
        File.WriteAllText(CapturePath,JsonSerializer.Serialize(capture,JsonOptions));
    }
    private string[] Args()=>["plan","--inventory",InventoryPath,"--capture-report",CapturePath,"--cutoff-day","2026-10-10","--output",OutputPath];
    private int Run(string[] args,CancellationToken token=default)=>Program.Run(args,token,()=>throw new InvalidOperationException("Offline command created USB provider"),output,error);
    private static void Edit(string path,Action<JsonObject> edit){var root=JsonNode.Parse(File.ReadAllText(path))!.AsObject();edit(root);File.WriteAllText(path,root.ToJsonString());}

    [Fact]
    public void OfflineCommandProducesRealPlanAndLeavesEveryInputUnchanged()
    {
        var inventory=File.ReadAllBytes(InventoryPath);var captures=File.ReadAllBytes(CapturePath);
        Assert.Equal(0,Run(Args()));
        using var json=JsonDocument.Parse(File.ReadAllText(OutputPath));var root=json.RootElement;
        Assert.True(root.GetProperty("isComplete").GetBoolean());Assert.Equal(1,root.GetProperty("selectedCount").GetInt32());
        Assert.Equal(30,root.GetProperty("selectedKnownBytes").GetInt32());
        Assert.Equal("eligible",root.GetProperty("rows")[0].GetProperty("decision").GetString());
        Assert.Equal("alreadyImported",root.GetProperty("rows")[1].GetProperty("decision").GetString());
        Assert.Equal(inventory,File.ReadAllBytes(InventoryPath));Assert.Equal(captures,File.ReadAllBytes(CapturePath));
        Assert.Contains("Offline",output.ToString());
    }

    [Theory]
    [InlineData("missingInventory")]
    [InlineData("twoCutoffs")]
    [InlineData("invalidDate")]
    [InlineData("duplicateOutput")]
    [InlineData("unknown")]
    public void MalformedArgumentsDoNotConstructProvider(string problem)
    {
        var args=Args().ToList();
        if(problem=="missingInventory") args.RemoveRange(1,2);
        if(problem=="twoCutoffs") args.AddRange(["--checkpoint","state.json"]);
        if(problem=="invalidDate") args[6]="2026-10-10T00:00:00";
        if(problem=="duplicateOutput") args.AddRange(["--output","other.json"]);
        if(problem=="unknown") args.Add("--unknown");
        Assert.Equal(2,Run(args.ToArray()));Assert.False(File.Exists(OutputPath));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("brokenJson")]
    [InlineData("missingPhotos")]
    [InlineData("missingDeviceId")]
    [InlineData("nullPhoto")]
    [InlineData("negativeSize")]
    [InlineData("overflowSize")]
    [InlineData("duplicateProperty")]
    [InlineData("missingDateObservation")]
    public void InvalidInventoryIsInputError(string problem)
    {
        if(problem=="null") File.WriteAllText(InventoryPath,"null");
        else if(problem=="brokenJson") File.WriteAllText(InventoryPath,"{");
        else if(problem=="duplicateProperty") File.WriteAllText(InventoryPath,File.ReadAllText(InventoryPath).Replace("\"completionStatus\":\"complete\"","\"completionStatus\":\"complete\",\"completionStatus\":\"complete\""));
        else Edit(InventoryPath,r=>{
            if(problem=="missingPhotos")r.Remove("photos");
            if(problem=="missingDeviceId")r["device"]!.AsObject().Remove("id");
            if(problem=="nullPhoto")r["photos"]![0]=null;
            if(problem=="negativeSize")r["photos"]![0]!["sizeBytes"]=-1;
            if(problem=="overflowSize")r["photos"]![0]!["sizeBytes"]=JsonNode.Parse("18446744073709551616");
            if(problem=="missingDateObservation")r["photos"]![0]!.AsObject().Remove("dateAuthored");
        });
        Assert.Equal(2,Run(Args()));Assert.False(File.Exists(OutputPath));
    }

    [Theory]
    [InlineData("schema")]
    [InlineData("missingDayStatus")]
    [InlineData("missingDirectories")]
    [InlineData("missingSource")]
    [InlineData("differentDevice")]
    public void InvalidCaptureSchemaIsInputError(string problem)
    {
        Edit(CapturePath,r=>{
            if(problem=="schema")r["schemaVersion"]=42;
            if(problem=="missingDayStatus")r["files"]![0]!["observation"]!.AsObject().Remove("captureDayStatus");
            if(problem=="missingDirectories")r["files"]![0]!["observation"]!.AsObject().Remove("captureDirectories");
            if(problem=="missingSource")r["files"]![0]!.AsObject().Remove("source");
            if(problem=="differentDevice")r["device"]!["id"]="another";
        });
        Assert.Equal(2,Run(Args()));
    }

    [Theory]
    [InlineData("legacy")]
    [InlineData("partialInventory")]
    [InlineData("rawMismatch")]
    [InlineData("unknownSize")]
    public void UnresolvedEvidenceStillExportsIncompletePreview(string problem)
    {
        if(problem=="legacy")Edit(CapturePath,r=>r.Remove("schemaVersion"));
        if(problem=="partialInventory")Edit(InventoryPath,r=>r["completionStatus"]="incomplete");
        if(problem=="rawMismatch")Edit(CapturePath,r=>r["files"]![0]!["observation"]!["captureDay"]="2026-10-12");
        if(problem=="unknownSize")Edit(InventoryPath,r=>r["photos"]![0]!["sizeBytes"]=null);
        Assert.Equal(4,Run(Args()));
        using var json=JsonDocument.Parse(File.ReadAllText(OutputPath));Assert.False(json.RootElement.GetProperty("isComplete").GetBoolean());
        Assert.Equal(2,json.RootElement.GetProperty("rows").GetArrayLength());
    }

    [Fact]
    public void MissingLocalFileIsIoFailureAndCollisionNeverOverwrites()
    {
        File.WriteAllText(OutputPath,"keep");Assert.Equal(5,Run(Args()));Assert.Equal("keep",File.ReadAllText(OutputPath));
        File.Delete(OutputPath);File.Delete(CapturePath);Assert.Equal(5,Run(Args()));
    }

    [Fact]
    public void InputOutputAliasIsRejectedBeforeReadingOrWriting()
    {
        var bytes=File.ReadAllBytes(InventoryPath);var args=Args();args[^1]=InventoryPath.ToUpperInvariant();
        Assert.Equal(2,Run(args));Assert.Equal(bytes,File.ReadAllBytes(InventoryPath));
    }

    [Fact]
    public void RepeatedCaptureReportsCoalesceAndCheckpointIsNeverWritten()
    {
        var checkpointPath=Path.Combine(temp,"checkpoint.json");
        File.WriteAllText(checkpointPath,JsonSerializer.Serialize(PreviewFixture.Checkpoint("tracked"),JsonOptions));
        var checkpointBytes=File.ReadAllBytes(checkpointPath);var args=Args();args[5]="--checkpoint";args[6]=checkpointPath;
        Assert.Equal(0,Run([..args,"--capture-report",CapturePath]));
        using var json=JsonDocument.Parse(File.ReadAllText(OutputPath));Assert.Equal(2,json.RootElement.GetProperty("selectedCount").GetInt32());
        Assert.Equal(checkpointBytes,File.ReadAllBytes(checkpointPath));
    }

    [Fact]
    public void CancellationDoesNotCreateProviderOrOutput()
    {
        Assert.Equal(130,Run(Args(),new CancellationToken(true)));Assert.False(File.Exists(OutputPath));
    }

    [Fact]
    public void CancellationDuringOutputKeepsExit130WhenExportLosesARace()
    {
        using var cts=new CancellationTokenSource();
        using var writer=new InterruptingWriter(()=>{cts.Cancel();File.WriteAllText(OutputPath,"racing writer");});
        var exit=Program.Run(Args(),cts.Token,()=>throw new Exception("No USB"),writer,error);
        Assert.Equal(130,exit);Assert.Equal("racing writer",File.ReadAllText(OutputPath));
    }

    private sealed class InterruptingWriter(Action interrupt):StringWriter
    {
        private bool called;
        public override void WriteLine(string? value){base.WriteLine(value);if(!called){called=true;interrupt();}}
    }

    public void Dispose()=>Directory.Delete(temp,true);
}
