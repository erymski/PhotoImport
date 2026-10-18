using System.Text.Json;
using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Reporting;
using PhotoImport.Services;
using PhotoImport.Tests.Fakes;
using static PhotoImport.Tests.Fakes.FakePhotoDevice;

namespace PhotoImport.Tests;

public sealed class LivePreviewCliTests : IDisposable
{
    private readonly string parent = Path.Combine(
        Path.GetTempPath(),
        "live-preview-" + Guid.NewGuid().ToString("N")
    );
    private string Destination => Path.Combine(parent, "reports");
    private string CheckpointPath => Path.Combine(parent, "checkpoint.json");
    private readonly StringWriter output = new(),
        error = new();

    public LivePreviewCliTests() => Directory.CreateDirectory(parent);

    public void Dispose() => Directory.Delete(parent, true);

    private string[] Args(string limit = "3") =>
        [
            "preview",
            "--device",
            "Phone",
            "--root",
            @"\Camera",
            "--max-photos",
            limit,
            "--cutoff-day",
            "2026-10-10",
            "--output-dir",
            Destination,
        ];

    private int Run(
        string[] args,
        Provider provider,
        CancellationToken token = default,
        TextWriter? writer = null
    ) => Program.Run(args, token, () => provider, writer ?? output, error);

    private JsonDocument Read(string file) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(Destination, file)));

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("2147483648")]
    [InlineData("many")]
    public void InvalidLimitNeverConstructsProvider(string limit)
    {
        Assert.Equal(
            2,
            Program.Run(Args(limit), default, () => throw new Exception("No USB"), output, error)
        );
        Assert.False(Directory.Exists(Destination));
    }

    [Theory]
    [InlineData("--device")]
    [InlineData("--root")]
    [InlineData("--max-photos")]
    [InlineData("--cutoff-day")]
    [InlineData("--output-dir")]
    public void MissingRequiredOptionNeverConstructsProvider(string missing)
    {
        var args = Args().ToList();
        var index = args.IndexOf(missing);
        args.RemoveRange(index, 2);
        Assert.Equal(
            2,
            Program.Run(args.ToArray(), default, () => throw new Exception("No USB"), output, error)
        );
    }

    [Theory]
    [InlineData("--unknown", "x")]
    [InlineData("--device", "Phone")]
    [InlineData("--max-photos", "2")]
    [InlineData("--root", @"\camera\nested")]
    public void UnknownDuplicateAndOverlappingOptionsNeverConstructProvider(
        string option,
        string value
    )
    {
        Assert.Equal(
            2,
            Program.Run(
                [.. Args(), option, value],
                default,
                () => throw new Exception("No USB"),
                output,
                error
            )
        );
    }

    [Fact]
    public void ExistingOutputIsRefusedBeforeProviderConstruction()
    {
        Directory.CreateDirectory(Destination);
        Assert.Equal(
            5,
            Program.Run(Args(), default, () => throw new Exception("No USB"), output, error)
        );
        Assert.Empty(Directory.GetFiles(Destination));
    }

    [Fact]
    public void InvalidCheckpointKeysFailLocallyWithoutProvider()
    {
        File.WriteAllText(
            CheckpointPath,
            """{"schemaVersion":1,"deviceId":"phone","cutoffDay":"2026-10-10","boundaryMode":"tracked","boundaryKeys":["bad"]}"""
        );
        Assert.Equal(
            2,
            Program.Run(
                CheckpointArgs(),
                default,
                () => throw new Exception("No USB"),
                output,
                error
            )
        );
    }

    private string[] CheckpointArgs() =>
        [
            "preview",
            "--device",
            "Phone",
            "--root",
            @"\Camera",
            "--max-photos",
            "3",
            "--checkpoint",
            CheckpointPath,
            "--output-dir",
            Destination,
        ];

    [Fact]
    public void CompleteFolderProducesLiteralDecisionsAndOfflineReadableReports()
    {
        using var provider = Phone(
            "2026:10:09 12:00:00",
            "2026:10:10 12:00:00",
            "2026:10:11 12:00:00"
        );
        provider.Device.OnOpen = _ => Assert.Contains("786432", output.ToString());
        Assert.Equal(0, Run(Args(), provider));
        Assert.True(provider.Device.Disposed);
        Assert.True(provider.Disposed);
        using var preview = Read("preview.json");
        var rows = preview.RootElement.GetProperty("rows").EnumerateArray().ToArray();
        Assert.Equal(
            new[] { "older", "alreadyImported", "eligible" },
            rows.Select(r => r.GetProperty("decision").GetString())
        );
        Assert.Equal(1, preview.RootElement.GetProperty("selectedCount").GetInt32());
        var inventory = PreviewInputReader.ReadInventory(
            Path.Combine(Destination, "inventory.json"),
            default
        );
        var captures = PreviewInputReader.ReadCapture(
            Path.Combine(Destination, "capture.json"),
            default
        );
        var replan = DayPreviewPlanner.Build(
            inventory,
            [captures],
            new(1, "phone", new(2026, 10, 10), "completedDay", []),
            default
        );
        Assert.Equal(
            @"\Camera\c.jpg",
            Assert.Single(replan.Rows, r => r.Decision == "eligible").SourcePath
        );
        Assert.Contains("786432", output.ToString());
    }

    [Fact]
    public void TrackedDayIncludesNewPhotoAndDoesNotChangeCheckpoint()
    {
        File.WriteAllText(
            CheckpointPath,
            """{"schemaVersion":1,"deviceId":"phone","cutoffDay":"2026-10-10","boundaryMode":"tracked","boundaryKeys":[]}"""
        );
        var bytes = File.ReadAllBytes(CheckpointPath);
        using var provider = Phone("2026:10:10 12:00:00");
        Assert.Equal(0, Run(CheckpointArgs(), provider));
        using var preview = Read("preview.json");
        Assert.Equal(1, preview.RootElement.GetProperty("selectedCount").GetInt32());
        Assert.Equal(bytes, File.ReadAllBytes(CheckpointPath));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void EmptyAndUnsupportedFoldersHaveHonestCompleteness(bool png)
    {
        using var provider = Phone();
        if (png)
            provider.Device.Metadata.Children["root"] = [Entry("p", @"\Camera\p.png")];
        Assert.Equal(png ? 4 : 0, Run(Args(), provider));
        Assert.Empty(provider.Device.Opened);
        var capture = PreviewInputReader.ReadCapture(
            Path.Combine(Destination, "capture.json"),
            default
        );
        Assert.Empty(capture.Files);
        using var preview = Read("preview.json");
        Assert.Equal(png ? 1 : 0, preview.RootElement.GetProperty("unresolvedCount").GetInt32());
    }

    [Fact]
    public void ReachedLimitKeepsAllInventoryRowsVisible()
    {
        using var provider = Phone(
            "2026:10:11 12:00:00",
            "2026:10:12 12:00:00",
            "2026:10:13 12:00:00"
        );
        Assert.Equal(4, Run(Args("1"), provider));
        Assert.Single(provider.Device.Opened);
        using var preview = Read("preview.json");
        Assert.Equal(3, preview.RootElement.GetProperty("processedCount").GetInt32());
        Assert.Equal(2, preview.RootElement.GetProperty("unresolvedCount").GetInt32());
        using var manifest = Read("run.json");
        Assert.True(manifest.RootElement.GetProperty("limitReached").GetBoolean());
        Assert.Equal(1, manifest.RootElement.GetProperty("attemptedCount").GetInt32());
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("ambiguous")]
    [InlineData("root")]
    [InlineData("capability")]
    public void DeviceAndRootFailuresNeverOpenContent(string failure)
    {
        using var provider = Phone("2026:10:11 12:00:00");
        if (failure == "missing")
            provider.Devices = [];
        if (failure == "ambiguous")
            provider.Devices = [Info("one"), Info("two")];
        if (failure == "root")
            provider.Device.Metadata.Roots.Clear();
        if (failure == "capability")
            provider.MetadataOnly = true;
        Assert.Equal(3, Run(Args(), provider));
        Assert.Empty(provider.Device.Opened);
        using var manifest = Read("run.json");
        Assert.NotEmpty(manifest.RootElement.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public void WrongCheckpointDeviceFailsWithoutContent()
    {
        File.WriteAllText(
            CheckpointPath,
            """{"schemaVersion":1,"deviceId":"different","cutoffDay":"2026-10-10","boundaryMode":"tracked","boundaryKeys":[]}"""
        );
        using var provider = Phone("2026:10:11 12:00:00");
        Assert.Equal(2, Run(CheckpointArgs(), provider));
        Assert.Empty(provider.Device.Opened);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PartialEnumerationRetainsRowsAndDisconnectStopsCapture(bool disconnect)
    {
        using var provider = Phone("2026:10:11 12:00:00");
        provider.Device.Metadata.ChildrenIterator = _ => Children();
        IEnumerable<DeviceEntry> Children()
        {
            yield return Entry("a", @"\Camera\a.jpg");
            if (disconnect)
                throw new DeviceDisconnectedException("gone");
            throw new IOException("folder error");
        }
        Assert.Equal(disconnect ? 3 : 4, Run(Args(), provider));
        var inventory = PreviewInputReader.ReadInventory(
            Path.Combine(Destination, "inventory.json"),
            default
        );
        Assert.Single(inventory.Photos);
        Assert.Equal(disconnect ? 0 : 1, provider.Device.Opened.Count);
    }

    [Theory]
    [InlineData("session")]
    [InlineData("provider")]
    public void CleanupFailurePreventsCompleteReport(string owner)
    {
        using var provider = Phone("2026:10:11 12:00:00");
        if (owner == "session")
            provider.Device.DisposeError = new IOException("session cleanup");
        else
            provider.DisposeError = new IOException("provider cleanup");
        Assert.Equal(4, Run(Args(), provider));
        using var preview = Read("preview.json");
        Assert.False(preview.RootElement.GetProperty("isComplete").GetBoolean());
        using var manifest = Read("run.json");
        Assert.Equal(
            "incomplete",
            manifest.RootElement.GetProperty("completionStatus").GetString()
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void DisconnectOrCancellationPreservesEarlierCapture(bool cancel)
    {
        using var provider = Phone(
            "2026:10:11 12:00:00",
            "2026:10:12 12:00:00",
            "2026:10:13 12:00:00"
        );
        using var cts = new CancellationTokenSource();
        provider.Device.OnOpen = path =>
        {
            if (path.EndsWith("b.jpg"))
            {
                if (cancel)
                {
                    cts.Cancel();
                    throw new OperationCanceledException(cts.Token);
                }
                throw new DeviceDisconnectedException("unplugged");
            }
        };
        Assert.Equal(cancel ? 130 : 3, Run(Args(), provider, cts.Token));
        var capture = PreviewInputReader.ReadCapture(
            Path.Combine(Destination, "capture.json"),
            default
        );
        Assert.Equal(2, capture.Files.Count);
        Assert.Equal(new DateOnly(2026, 10, 11), capture.Files[0].Observation.CaptureDay);
        Assert.Equal(2, provider.Device.Opened.Count);
    }

    [Fact]
    public void CancellationDuringConsoleFailureKeepsExit130AndEarlierCapture()
    {
        using var provider = Phone("2026:10:11 12:00:00", "2026:10:12 12:00:00");
        using var cts = new CancellationTokenSource();
        using var writer = new InterruptingWriter(line =>
        {
            if (line?.Contains(@"\Camera\b.jpg") == true)
            {
                cts.Cancel();
                throw new IOException("console failed");
            }
        });
        provider.Device.DisposeError = new IOException("cleanup also failed");
        Assert.Equal(130, Run(Args(), provider, cts.Token, writer));
        var capture = PreviewInputReader.ReadCapture(
            Path.Combine(Destination, "capture.json"),
            default
        );
        Assert.Single(capture.Files);
    }

    [Fact]
    public void PreCancellationDoesNotConstructProvider()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        Assert.Equal(
            130,
            Program.Run(Args(), cts.Token, () => throw new Exception("No USB"), output, error)
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExportFailureKeepsOtherReportsAndCancellationPriority(bool cancel)
    {
        using var provider = Phone("2026:10:11 12:00:00");
        using var cts = new CancellationTokenSource();
        using (
            var bundle = PreviewBundle.Reserve(
                Destination,
                path =>
                    Path.GetFileName(path) == "capture.json"
                        ? new FailingExportStream(() =>
                        {
                            if (cancel)
                                cts.Cancel();
                        })
                        : new FileStream(path, FileMode.CreateNew)
            )
        )
        {
            var options = new LivePreviewOptions(
                "Phone",
                [@"\Camera"],
                3,
                new(2026, 10, 10),
                null,
                Destination
            );
            Assert.Equal(
                cancel ? 130 : 5,
                LivePreviewRunner.Run(provider, options, null, bundle, output, error, cts.Token)
            );
        }
        Assert.True(File.Exists(Path.Combine(Destination, "inventory.json")));
        using var manifest = Read("run.json");
        Assert.Equal(
            cancel ? "cancelled" : "incomplete",
            manifest.RootElement.GetProperty("completionStatus").GetString()
        );
        Assert.NotEmpty(manifest.RootElement.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public void CancellationDuringEnumerationSavesPartialInventoryWithoutContent()
    {
        using var provider = Phone("2026:10:11 12:00:00", "2026:10:12 12:00:00");
        using var cts = new CancellationTokenSource();
        provider.Device.Metadata.OnRead = _ => cts.Cancel();
        Assert.Equal(130, Run(Args(), provider, cts.Token));
        Assert.Empty(provider.Device.Opened);
        var inventory = PreviewInputReader.ReadInventory(
            Path.Combine(Destination, "inventory.json"),
            default
        );
        Assert.Single(inventory.Photos);
        Assert.Equal("cancelled", inventory.CompletionStatus);
    }

    [Theory]
    [InlineData("2026-2-3")]
    [InlineData("2026-02-30")]
    public void InvalidDayFailsBeforeProvider(string date)
    {
        var args = Args();
        args[Array.IndexOf(args, "--cutoff-day") + 1] = date;
        Assert.Equal(
            2,
            Program.Run(args, default, () => throw new Exception("No USB"), output, error)
        );
    }

    [Fact]
    public void ProviderFactoryFailureIsReportedAsDeviceFailure()
    {
        Assert.Equal(
            3,
            Program.Run(
                Args(),
                default,
                () =>
                    throw new TypeInitializationException(
                        "driver",
                        new IOException("driver unavailable")
                    ),
                output,
                error
            )
        );
        using var manifest = Read("run.json");
        Assert.NotEmpty(manifest.RootElement.GetProperty("errors").EnumerateArray());
    }

    [Fact]
    public void MultipleRootsAreResolvedBeforeCollectingAllTheirRows()
    {
        using var provider = Phone("2026:10:11 12:00:00");
        var second = Entry("other", @"\Pictures", true);
        provider.Device.Metadata.Roots.Add(second);
        provider.Device.Metadata.Children["other"] = [Entry("png", @"\Pictures\unknown.png")];
        Assert.Equal(4, Run([.. Args(), "--root", @"\Pictures"], provider));
        var inventory = PreviewInputReader.ReadInventory(
            Path.Combine(Destination, "inventory.json"),
            default
        );
        Assert.Equal(2, inventory.Photos.Count);
        Assert.Equal([@"\Camera", @"\Pictures"], inventory.SearchedRoots);
    }

    [Fact]
    public void ConsoleIoFailureReturnsFiveAndCannotClaimComplete()
    {
        using var provider = Phone("2026:10:11 12:00:00");
        using var writer = new InterruptingWriter(_ => throw new IOException("closed output"));
        Assert.Equal(5, Run(Args(), provider, writer: writer));
        using var manifest = Read("run.json");
        Assert.Equal(
            "incomplete",
            manifest.RootElement.GetProperty("completionStatus").GetString()
        );
    }

    [Fact]
    public void SharedObjectIdCaseAliasesStayVisibleAndAreNotOpened()
    {
        using var provider = Phone("2026:10:11 12:00:00");
        provider.Device.Metadata.Children["root"] =
        [
            Entry("same", @"\Camera\a.jpg"),
            Entry("same", @"\Camera\A.JPG"),
        ];
        provider.Device.Metadata.OnRead = e =>
            provider.Device.Metadata.Metadata[e.ObjectId] = Photo(e) with { SizeBytes = 1000 };
        Assert.Equal(4, Run(Args(), provider));
        Assert.Empty(provider.Device.Opened);
        using var preview = Read("preview.json");
        Assert.Equal(2, preview.RootElement.GetProperty("unresolvedCount").GetInt32());
        Assert.Equal(2, preview.RootElement.GetProperty("totalPhotoCount").GetInt32());
    }

    [Theory]
    [InlineData("0x80070079")]
    [InlineData("0x8007001f")]
    [InlineData("0x80070141")]
    [InlineData("0x802a0002")]
    public void RealBackendSessionFailureStopsAfterFailedAttemptAndRetainsEvidence(string code)
    {
        using var provider = Phone(
            "2026:10:11 12:00:00",
            "2026:10:12 12:00:00",
            "2026:10:13 12:00:00"
        );
        provider.Device.Content[@"\Camera\b.jpg"] = () =>
            WpdOperation.Run<PhotoReadHandle>(
                () =>
                    throw new AggregateException(
                        new MediaDevices.MediaDeviceException(
                            $"COM Error {code} in IPortableDeviceResources.GetStream: session failed"
                        )
                    ),
                default
            );
        Assert.Equal(3, Run(Args(), provider));
        Assert.Equal([@"\Camera\a.jpg", @"\Camera\b.jpg"], provider.Device.Opened);
        var capture = PreviewInputReader.ReadCapture(
            Path.Combine(Destination, "capture.json"),
            default
        );
        Assert.Equal(2, capture.Files.Count);
        Assert.Equal(new DateOnly(2026, 10, 11), capture.Files[0].Observation.CaptureDay);
        using var manifest = Read("run.json");
        Assert.Equal(2, manifest.RootElement.GetProperty("attemptedCount").GetInt32());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FinalFlushCancellationOrReportCloseFailurePersistsUnsuccessfulStatus(bool cancel)
    {
        using var provider = Phone();
        using var cts = new CancellationTokenSource();
        var cli = new PhotoImport.Cli.LivePreviewCli(
            output,
            error,
            path =>
                PreviewBundle.Reserve(
                    path,
                    file => new FinalizationStream(
                        new FileStream(file, FileMode.CreateNew),
                        onFlush: cancel && Path.GetFileName(file) == "run.json" ? cts.Cancel : null,
                        failClose: !cancel && Path.GetFileName(file) == "inventory.json"
                    )
                )
        );
        Assert.Equal(cancel ? 130 : 5, cli.Run(Args(), cts.Token, () => provider));
        using var manifest = Read("run.json");
        using var preview = Read("preview.json");
        Assert.Equal(
            cancel ? "cancelled" : "incomplete",
            manifest.RootElement.GetProperty("completionStatus").GetString()
        );
        Assert.False(preview.RootElement.GetProperty("isComplete").GetBoolean());
        if (!cancel)
            Assert.Contains(
                manifest.RootElement.GetProperty("errors").EnumerateArray(),
                e => e.GetString()!.Contains("report cleanup failure")
            );
    }

    private sealed class FinalizationStream(Stream inner, Action? onFlush, bool failClose) : Stream
    {
        public override void Write(byte[] buffer, int offset, int count) =>
            inner.Write(buffer, offset, count);

        public override void Write(ReadOnlySpan<byte> buffer) => inner.Write(buffer);

        public override void Flush()
        {
            inner.Flush();
            onFlush?.Invoke();
        }

        protected override void Dispose(bool disposing)
        {
            inner.Dispose();
            if (failClose)
                throw new IOException("report cleanup failure");
            base.Dispose(disposing);
        }

        public override bool CanRead => false;
        public override bool CanSeek => false;
        public override bool CanWrite => true;
        public override long Length => throw new NotSupportedException();
        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();
    }

    private sealed class FailingExportStream(Action fail) : MemoryStream
    {
        public override void Write(byte[] buffer, int offset, int count)
        {
            fail();
            throw new IOException("export failed");
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            fail();
            throw new IOException("export failed");
        }
    }

    private sealed class InterruptingWriter(Action<string?> action) : StringWriter
    {
        public override void WriteLine(string? value)
        {
            action(value);
            base.WriteLine(value);
        }
    }

    internal sealed class Provider : IPhotoDeviceProvider, IDisposable
    {
        public FakeContentDevice Device { get; init; } = new();
        public IReadOnlyList<DeviceInfo> Devices { get; set; } = [Info()];
        public bool MetadataOnly,
            Disposed;
        public Exception? DisposeError;

        public IReadOnlyList<DeviceInfo> Discover(CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Devices;
        }

        public IPhotoDevice Open(DeviceInfo device, CancellationToken token) =>
            MetadataOnly ? Device.Metadata : Device;

        public void Dispose()
        {
            if (Disposed)
                return;
            Disposed = true;
            if (DisposeError is not null)
                throw DisposeError;
        }
    }

    internal static Provider Phone(params string[] dates)
    {
        var inventory = BoundedCaptureCollectorTests.Inventory(
            dates.Select((_, i) => $"{(char)('a' + i)}.jpg").ToArray()
        );
        var device = BoundedCaptureCollectorTests.Content(inventory);
        device.Metadata.Roots.Add(Entry("root", @"\Camera", true));
        device.Metadata.Children["root"] = inventory
            .Photos.Select(p => Entry(p.ObjectId, p.SourcePath))
            .ToArray();
        for (var i = 0; i < dates.Length; i++)
        {
            var photo = inventory.Photos[i];
            var data = JpegFixture.Create(dates[i]);
            device.Metadata.Metadata[photo.ObjectId] = photo;
            device.Content[photo.SourcePath] = () =>
                new(
                    new(photo.SourcePath, photo.SizeBytes, photo.PersistentObjectId),
                    new ForwardOnlyStream(data)
                );
        }
        return new() { Device = device };
    }
}
