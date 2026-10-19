using PhotoImport.Devices;
using PhotoImport.Importing;
using PhotoImport.Models;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public class IPhoneImportTests
{
    private const string Policy = "iphoneWindowsDayV1";
    private const string Root = @"\Internal Storage";

    private static ImportRegistration Registration(ImportFixture f) =>
        f.Registration with
        {
            Alias = "iPhone",
            Roots = [Root],
            DatePolicy = Policy,
            Identity = new("serial", "test-iphone", "iphone", "Apple Inc.", "Apple iPhone"),
            Checkpoint = new(new(2025, 12, 5), "completedDay", []),
        };

    private static PhotoMetadata Photo(string name, string? date, string persistent = "old") =>
        FilenameImportPlannerTests.File(name, 3) with
        {
            SourcePath = Root + @"\202512_a\" + name,
            PersistentObjectId = persistent,
            DateCreated = WpdMetadataMapper.ObserveTimestamp("dateCreated", date, false),
        };

    private sealed class Provider(FakeImportDevice device) : IPhotoDeviceProvider
    {
        public IReadOnlyList<DeviceInfo> Discover(CancellationToken token) =>
            [
                new(
                    "iphone",
                    "Apple iPhone",
                    "Apple Inc.",
                    "Apple iPhone",
                    "USB",
                    new Dictionary<string, string?> { ["SerialNumber"] = "test-iphone" }
                ),
            ];

        public IPhotoDevice Open(DeviceInfo info, CancellationToken token) => device;
    }

    private static (Provider Provider, FakeImportDevice Device) Setup(
        ImportFixture f,
        ImportStateStore store,
        params PhotoMetadata[] files
    )
    {
        using (store.AcquireLock())
            store.WriteConfiguration(new(1, "filenameImporter", [Registration(f)]));
        var device = new FakeImportDevice();
        device.Metadata.Roots.Add(FakePhotoDevice.Entry("storage", Root, true));
        var month = FakePhotoDevice.Entry("month", Root + @"\202512_a", true);
        device.Metadata.Children["storage"] = [month];
        device.Metadata.Children["month"] = files
            .Select(p => FakePhotoDevice.Entry(p.ObjectId, p.SourcePath))
            .ToList();
        foreach (var file in files)
        {
            device.Metadata.Metadata[file.ObjectId] = file;
            device.Contents[file.SourcePath] = [1, 2, 3];
        }
        return (new(device), device);
    }

    [Fact]
    public void WindowsDayIncludesDecember6AndIgnoresFilenameAndOtherDates()
    {
        using var f = new ImportFixture();
        var included = Photo("IMG_1234.JPG", "2025-12-06T00:00:00.0000000");
        var older = Photo("20261231_1.MOV", "2025-12-05T23:59:59.0000000");
        var missing = Photo("20261231_2.PNG", null) with { DateAuthored = included.DateCreated };
        var plan = FilenameImportPlanner.Build(
            Registration(f),
            FilenameImportPlannerTests.Inventory(included, older, missing)
        );
        Assert.True(plan.Complete);
        Assert.Equal(included.SourcePath, Assert.Single(plan.Candidates).Source.SourcePath);
        Assert.Single(plan.Rows, r => r.Decision == "older");
        Assert.Single(plan.Rows, r => r.Decision == "skipped");
    }

    [Theory]
    [InlineData("2025-12-06T00:10:00+14:00", "2025-12-06")]
    [InlineData("2025-02-30T12:00:00", null)]
    [InlineData("2025-12-06junk", null)]
    public void ValidatesTimestampAndPreservesReportedCalendarDay(
        string timestamp,
        string? expected
    )
    {
        using var f = new ImportFixture();
        var plan = FilenameImportPlanner.Build(
            Registration(f),
            FilenameImportPlannerTests.Inventory(Photo("IMG_1.JPG", timestamp))
        );
        Assert.Equal(expected, plan.Rows.Single().Day?.ToString("yyyy-MM-dd"));
    }

    [Fact]
    public void DryRunReadsNoContentAndChangedIdsDoNotRecopyTrackedFiles()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var photo = Photo("IMG_1.JPG", "2025-12-06T12:00:00.0000000");
        var (provider, device) = Setup(f, store, photo);
        var runner = new ImportRunner(store, new());
        Assert.Single(runner.DryRun(provider, null, default).Plan.Candidates);
        Assert.Empty(device.Opened);
        Assert.Equal(0, runner.Run(provider, null, default).ExitCode);
        device.Metadata.Metadata[photo.ObjectId] = photo with { PersistentObjectId = "new" };
        Assert.Equal(0, runner.Run(provider, null, default).Copied);
        Assert.Single(device.Opened);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ResumeRefreshesVolatileIdButRejectsChangedDate(bool changedDate)
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var photo = Photo("IMG_1.MOV", "2025-12-06T12:00:00.0000000");
        var (provider, device) = Setup(f, store, photo);
        device.OnOpen = _ => throw new IOException("disconnected");
        Assert.Equal(4, new ImportRunner(store, new()).Run(provider, null, default).ExitCode);
        device.OnOpen = null;
        device.Metadata.Metadata[photo.ObjectId] = photo with
        {
            PersistentObjectId = "reconnected",
            DateCreated = changedDate
                ? WpdMetadataMapper.ObserveTimestamp(
                    "dateCreated",
                    "2025-12-06T13:00:00.0000000",
                    false
                )
                : photo.DateCreated,
        };
        var result = new ImportRunner(store, new()).Run(provider, null, default);
        Assert.Equal(changedDate ? 4 : 0, result.ExitCode);
        Assert.Equal(!changedDate, result.CheckpointCommitted);
        Assert.Equal(changedDate ? 1 : 2, device.Opened.Count);
    }

    [Fact]
    public void IdChangeAfterInventoryStillBlocksContentTransfer()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var photo = Photo("IMG_1.PNG", "2025-12-06T12:00:00.0000000");
        var (provider, device) = Setup(f, store, photo);
        device.OnOpen = path =>
            new(new(path, 3, "changed-during-read"), new MemoryStream([1, 2, 3]));
        Assert.Equal(4, new ImportRunner(store, new()).Run(provider, null, default).ExitCode);
        Assert.Empty(Directory.GetFiles(f.Destination));
    }

    [Fact]
    public void InitExplicitWindowsPolicyRegistersAndSynchronizesIPhoneRoot()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var (provider, _) = Setup(f, store, Photo("IMG_1.JPG", "2025-12-06T12:00:00.0000000"));
        File.Delete(store.ConfigPath);
        var cli = new ImportCli(new StringWriter(), new StringWriter(), store, new());
        Assert.Equal(
            0,
            cli.Run(
                [
                    "init",
                    "--device",
                    "Apple iPhone",
                    "--alias",
                    "iPhone",
                    "--destination",
                    f.Destination,
                    "--cutoff-day",
                    "2025-12-05",
                    "--root",
                    Root,
                    "--date-policy",
                    Policy,
                ],
                default,
                () => provider
            )
        );
        using (store.AcquireReadLock())
            Assert.Equal(Policy, store.ReadConfiguration()!.Devices.Single().DatePolicy);
        Assert.Equal(0, cli.Run(["sync", "--device", "iPhone"], default, () => provider));
    }

    [Fact]
    public void WindowsPolicyDoesNotUseModifiedDateWhenCreatedIsUnavailable()
    {
        using var f = new ImportFixture();
        var file = Photo("IMG_1.JPG", null) with
        {
            DateModified = WpdMetadataMapper.ObserveTimestamp(
                "dateModified",
                "2026-10-19T12:00:00",
                false
            ),
        };
        var plan = FilenameImportPlanner.Build(
            Registration(f),
            FilenameImportPlannerTests.Inventory(file)
        );
        Assert.Empty(plan.Candidates);
        Assert.Equal("skipped", plan.Rows.Single().Decision);
    }

    [Fact]
    public void AndroidResumeStillRejectsChangedPersistentId()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var provider = ImportRunnerTests.Setup(f, store);
        var file = provider.Device.Metadata.Metadata.Values.Single() with
        {
            PersistentObjectId = "old",
        };
        provider.Device.Metadata.Metadata[file.ObjectId] = file;
        provider.Device.OnOpen = _ => throw new IOException("disconnected");
        Assert.Equal(4, new ImportRunner(store, new()).Run(provider, null, default).ExitCode);
        provider.Device.OnOpen = null;
        provider.Device.Metadata.Metadata[file.ObjectId] = file with { PersistentObjectId = "new" };
        Assert.Equal(4, new ImportRunner(store, new()).Run(provider, null, default).ExitCode);
        Assert.Single(provider.Device.Opened);
    }

    [Fact]
    public void AndroidResumePreservesKnownIdWhenInventoryIdIsMissing()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var provider = ImportRunnerTests.Setup(f, store);
        var file = provider.Device.Metadata.Metadata.Values.Single() with
        {
            PersistentObjectId = "old",
        };
        provider.Device.Metadata.Metadata[file.ObjectId] = file;
        provider.Device.OnOpen = _ => throw new IOException("disconnected");
        Assert.Equal(4, new ImportRunner(store, new()).Run(provider, null, default).ExitCode);
        provider.Device.Metadata.Metadata[file.ObjectId] = file with { PersistentObjectId = null };
        provider.Device.OnOpen = path =>
            new(new(path, file.SizeBytes, "changed"), new MemoryStream([1, 2, 3]));
        Assert.Equal(4, new ImportRunner(store, new()).Run(provider, null, default).ExitCode);
        Assert.Empty(Directory.GetFiles(f.Destination));
    }

    [Fact]
    public void AndroidPolicyStillRejectsIPhoneRoot()
    {
        Assert.Throws<ImportException>(() => ScopedMediaInventory.ValidateRoots([Root]));
    }
}
