using PhotoImport.Importing;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public class ImportCliTests
{
    [Fact]
    public void InitRegistersOnceAndEverydayLaunchCopies()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        // Setup a fake phone, removing only the fixture config before exercising real init.
        var provider = ImportRunnerTests.Setup(f, store);
        File.Delete(store.ConfigPath);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var cli = new ImportCli(output, error, store, new());
        string[] args =
        [
            "init",
            "--device",
            "Phone",
            "--alias",
            "S21",
            "--destination",
            f.Destination,
            "--cutoff-day",
            "2026-09-03",
            "--root",
            ScopedMediaInventoryTests.Root,
        ];
        Assert.Equal(0, cli.Run(args, default, () => provider));
        Assert.Empty(provider.Device.Opened);
        Assert.Equal(2, cli.Run(args, default, () => provider));
        Assert.Equal(0, cli.Run([], default, () => provider));
        Assert.Single(provider.Device.Opened);
        Assert.Contains("Checkpoint", output.ToString());
    }

    [Fact]
    public void DryRunAndStatusAreReadOnlyAndUnknownNamesVisible()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var provider = ImportRunnerTests.Setup(f, store);
        var unknown = FilenameImportPlannerTests.File("IMG_1234.jpg", 3);
        provider.Device.Metadata.Children["camera"] =
        [
            .. provider.Device.Metadata.Children["camera"],
            FakePhotoDevice.Entry(unknown.ObjectId, unknown.SourcePath),
        ];
        provider.Device.Metadata.Metadata[unknown.ObjectId] = unknown;
        var before = File.ReadAllText(store.ConfigPath);
        using var output = new StringWriter();
        var cli = new ImportCli(output, new StringWriter(), store, new());
        Assert.Equal(0, cli.Run(["sync", "--dry-run"], default, () => provider));
        Assert.Contains("IMG_1234", output.ToString());
        Assert.Empty(provider.Device.Opened);
        Assert.Equal(before, File.ReadAllText(store.ConfigPath));
        Assert.Empty(Directory.GetFiles(f.Destination));
        Assert.Equal(
            0,
            cli.Run(["status"], default, () => throw new Exception("must remain offline"))
        );
    }

    [Theory]
    [InlineData("--unknown")]
    [InlineData("--device")]
    public void InvalidOptionsFailBeforeDiscovery(string option)
    {
        using var f = new ImportFixture();
        var cli = new ImportCli(new StringWriter(), new StringWriter(), new(f.State), new());
        Assert.Equal(
            2,
            cli.Run(["sync", option], default, () => throw new Exception("unexpected discovery"))
        );
    }
}

public class ImportProgramRoutingTests
{
    [Fact]
    public void NoConfigurationShowsFirstUseWithoutDeviceDiscoveryOrFiles()
    {
        using var f = new ImportFixture();
        using var output = new StringWriter();
        Assert.Equal(
            2,
            Program.Run(
                [],
                default,
                () => throw new Exception("unexpected discovery"),
                output,
                new StringWriter(),
                f.State
            )
        );
        Assert.Contains("init", output.ToString());
        Assert.False(Directory.Exists(f.State));
    }

    [Fact]
    public void HelpDoesNotDiscoverPhones()
    {
        using var f = new ImportFixture();
        using var output = new StringWriter();
        Assert.Equal(
            0,
            Program.Run(
                ["--help"],
                default,
                () => throw new Exception("unexpected discovery"),
                output,
                new StringWriter(),
                f.State
            )
        );
        Assert.Contains("sync", output.ToString());
        Assert.Contains("probe", output.ToString());
    }
}
