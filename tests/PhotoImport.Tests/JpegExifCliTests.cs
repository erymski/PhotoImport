using System.Text.Json;
using PhotoImport.Cli;
using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public sealed class JpegExifCliTests : IDisposable
{
    private readonly string temp = Path.Combine(Path.GetTempPath(), "PhotoImport-exif-" + Guid.NewGuid());
    private readonly Provider provider = new();
    private readonly StringWriter output = new(), error = new();
    public JpegExifCliTests() => Directory.CreateDirectory(temp);
    private string ReportPath => Path.Combine(temp, "report.json");
    private string[] Args(params string[] photos) => ["exif", "--device", "phone", .. photos.SelectMany(p => new[] { "--photo", p }), "--output", ReportPath];
    private int Run(string[] args, CancellationToken token = default) => new JpegExifCli(provider, output, error).Run(args, token);

    [Theory]
    [InlineData("exif")]
    [InlineData("exif", "--device", "phone", "--photo")]
    [InlineData("exif", "--device", "phone", "--output", "unused")]
    [InlineData("exif", "--device", "phone", "--device", "phone", "--photo", "\\a.jpg", "--output", "unused")]
    [InlineData("exif", "--device", "phone", "--photo", "\\a.jpg", "--output", "unused", "--output", "unused2")]
    public void IncompleteOrDuplicateArgumentsDoNotAccessDevice(params string[] args)
    {
        Assert.Equal(2, Run(args)); Assert.False(provider.Discovered); Assert.Empty(provider.Device.Opened);
    }

    [Theory]
    [InlineData("relative.jpg")]
    [InlineData("\\a.heic")]
    [InlineData("\\folder\\..\\a.jpg")]
    [InlineData("\\folder\\*.jpg")]
    [InlineData("\\\\server\\a.jpg")]
    public void InvalidOrNonJpegPathsDoNotAccessDevice(string path)
    {
        Assert.Equal(2, Run(Args(path))); Assert.False(provider.Discovered);
    }

    [Fact]
    public void TooManyOrDuplicateSelectionsDoNotAccessDevice()
    {
        Assert.Equal(2, Run(Args("\\a.jpg", "\\b.jpg", "\\c.jpg", "\\d.jpg")));
        Assert.Equal(2, Run(Args("\\a.jpg", "\\A.JPG")));
        Assert.False(provider.Discovered);
    }

    [Fact]
    public void OutputCollisionAndInvalidOutputCauseNoContentReads()
    {
        File.WriteAllText(ReportPath, "keep");
        Assert.Equal(5, Run(Args("\\a.jpg")));
        Assert.Equal("keep", File.ReadAllText(ReportPath));
        Assert.False(provider.Discovered);
        var args = Args("\\a.jpg"); args[^1] = "bad\0.json";
        Assert.Equal(5, Run(args)); Assert.Empty(provider.Device.Opened);
    }

    [Fact]
    public void ExplicitSelectionExportsOnlyDateEvidenceAndCounts()
    {
        var stream = new ForwardOnlyStream(JpegFixture.Create(), 7);
        provider.Device.Streams["\\a.jpg"] = stream;
        Assert.Equal(0, Run(Args("\\a.jpg")));
        using var json = JsonDocument.Parse(File.ReadAllText(ReportPath));
        var root = json.RootElement;
        Assert.Equal("complete", root.GetProperty("completionStatus").GetString());
        Assert.Equal(262144, root.GetProperty("applicationReadLimitPerPhoto").GetInt32());
        Assert.False(root.GetProperty("driverReadAheadBounded").GetBoolean());
        var file = root.GetProperty("files")[0];
        var observation = file.GetProperty("observation");
        Assert.Equal("2026:10:15 19:17:17", observation.GetProperty("rawFields").GetProperty("dateTime").GetString());
        Assert.Equal("2026-10-15T19:17:14.762+02:00", observation.GetProperty("captureTimestamp").GetString());
        Assert.Contains("EXIF", observation.GetProperty("source").GetString());
        Assert.Equal(stream.Consumed, observation.GetProperty("bytesRead").GetInt32());
        Assert.True(file.GetProperty("elapsedSeconds").GetDouble() >= 0);
        Assert.True(root.GetProperty("elapsedSeconds").GetDouble() >= 0);
        Assert.False(observation.TryGetProperty("imageBytes", out _));
        Assert.True(stream.Disposed); Assert.True(provider.Device.Disposed);
        Assert.Equal(new[] { "\\a.jpg" }, provider.Device.Opened);
        Assert.Contains("content", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void EarlierResultSurvivesLaterReadFailure()
    {
        provider.Device.Streams["\\a.jpg"] = new ForwardOnlyStream(JpegFixture.Create());
        var failed = new ForwardOnlyStream([]) { ReadError = new IOException("USB read failed") };
        provider.Device.Streams["\\b.jpg"] = failed;
        Assert.Equal(4, Run(Args("\\a.jpg", "\\b.jpg")));
        using var json = JsonDocument.Parse(File.ReadAllText(ReportPath));
        Assert.Equal(2, json.RootElement.GetProperty("files").GetArrayLength());
        Assert.Equal("available", json.RootElement.GetProperty("files")[0].GetProperty("observation").GetProperty("outcome").GetString());
        Assert.Equal("USB read failed", json.RootElement.GetProperty("files")[1].GetProperty("observation").GetProperty("error").GetString());
        Assert.True(failed.Disposed); Assert.True(provider.Device.Disposed);
    }

    [Fact]
    public void CancellationAndCleanupFailureRetainEarlierResultsAndExit130()
    {
        using var cts = new CancellationTokenSource();
        provider.Device.Streams["\\a.jpg"] = new ForwardOnlyStream(JpegFixture.Create());
        var cancelled = new ForwardOnlyStream(JpegFixture.Create(), 1) { OnRead = cts.Cancel, DisposeError = new IOException("stream cleanup failed") };
        provider.Device.Streams["\\b.jpg"] = cancelled;
        provider.Device.DisposeError = new IOException("session cleanup failed");
        Assert.Equal(130, Run(Args("\\a.jpg", "\\b.jpg"), cts.Token));
        using var json = JsonDocument.Parse(File.ReadAllText(ReportPath));
        Assert.Equal("cancelled", json.RootElement.GetProperty("completionStatus").GetString());
        Assert.Equal(2, json.RootElement.GetProperty("files").GetArrayLength());
        Assert.Contains("cleanup failed", File.ReadAllText(ReportPath));
        Assert.True(cancelled.Disposed); Assert.True(provider.Device.Disposed);
    }

    [Fact]
    public void AmbiguousNamesReportIdsAndDoNotOpenContent()
    {
        provider.Devices = [FakePhotoDevice.Info("id-1"), FakePhotoDevice.Info("id-2")];
        Assert.Equal(2, Run(Args("\\a.jpg")));
        Assert.Contains("id-1", error.ToString()); Assert.Contains("id-2", error.ToString());
        Assert.Empty(provider.Device.Opened);
    }

    [Fact]
    public void ExactIdWinsOverFriendlyName()
    {
        provider.Devices = [FakePhotoDevice.Info("other", "phone"), FakePhotoDevice.Info("phone", "Correct")];
        provider.Device.Streams["\\a.jpg"] = new ForwardOnlyStream(JpegFixture.Create());
        Assert.Equal(0, Run(Args("\\a.jpg"))); Assert.Equal("phone", provider.Selected?.Id);
    }

    [Fact]
    public void UnsupportedBackendAndMissingDeviceReturn3()
    {
        provider.Session = MetadataProbeTests.SimpleDevice();
        Assert.Equal(3, Run(Args("\\a.jpg")));
        File.Delete(ReportPath);
        provider.Devices = [];
        Assert.Equal(3, Run(Args("\\a.jpg")));
    }

    [Fact]
    public void NormalProbeNeverOpensContentCapability()
    {
        Assert.Equal(0, new ProbeCli(provider, output, error).Run(["probe", "--device", "phone"], default));
        Assert.Empty(provider.Device.Opened);
    }

    [Fact]
    public void MissingCaptureDateReturns4WithoutUsingGeneralDateTime()
    {
        provider.Device.Streams["\\a.jpg"] = new ForwardOnlyStream(JpegFixture.Create(original: ""));
        Assert.Equal(4, Run(Args("\\a.jpg")));
    }

    private sealed class Provider : IPhotoDeviceProvider
    {
        public ContentDevice Device { get; } = new();
        public IPhotoDevice? Session { get; set; }
        public bool Discovered { get; private set; }
        public DeviceInfo? Selected { get; private set; }
        public IReadOnlyList<DeviceInfo> Devices { get; set; } = [FakePhotoDevice.Info()];
        public IReadOnlyList<DeviceInfo> Discover(CancellationToken token) { Discovered = true; token.ThrowIfCancellationRequested(); return Devices; }
        public IPhotoDevice Open(DeviceInfo info, CancellationToken token) { Selected = info; return Session ?? Device; }
    }

    private sealed class ContentDevice : IPhotoDevice, IJpegContentDevice
    {
        private readonly FakePhotoDevice metadata = MetadataProbeTests.SimpleDevice();
        public Dictionary<string, Stream> Streams { get; } = [];
        public List<string> Opened { get; } = [];
        public bool Disposed { get; private set; }
        public Exception? DisposeError { get; set; }
        public Stream OpenJpegRead(string path, CancellationToken token) { Opened.Add(path); return Streams[path]; }
        public IReadOnlyList<DeviceEntry> GetStorageRoots(CancellationToken token) => metadata.GetStorageRoots(token);
        public IEnumerable<DeviceEntry> GetChildren(DeviceEntry directory, CancellationToken token) => metadata.GetChildren(directory, token);
        public PhotoMetadata ReadPhotoMetadata(DeviceEntry file, CancellationToken token) => metadata.ReadPhotoMetadata(file, token);
        public void Dispose() { Disposed = true; if (DisposeError is not null) throw DisposeError; }
    }

    public void Dispose() => Directory.Delete(temp, true);
}
