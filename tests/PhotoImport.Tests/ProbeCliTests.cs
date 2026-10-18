using System.Text.Json;
using PhotoImport.Cli;
using PhotoImport.Devices;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public sealed class ProbeCliTests : IDisposable
{
    private readonly string temp = Path.Combine(
        Path.GetTempPath(),
        "PhotoImport-tests-" + Guid.NewGuid()
    );
    private readonly FakePhotoDeviceProvider provider = new();
    private readonly StringWriter output = new();
    private readonly StringWriter error = new();

    public ProbeCliTests() => Directory.CreateDirectory(temp);

    private int Run(params string[] args) =>
        new ProbeCli(provider, output, error).Run(args, default);

    [Theory]
    [InlineData("unknown")]
    [InlineData("probe")]
    [InlineData("probe", "--device")]
    [InlineData("probe", "--device", " ")]
    [InlineData("probe", "--device", "phone", "--device", "phone")]
    [InlineData("probe", "--device", "phone", "--output")]
    [InlineData("probe", "--device", "phone", "--output", "a", "--output", "b")]
    [InlineData("probe", "--device", "phone", "--bogus")]
    [InlineData("list-devices", "--output", "a")]
    public void InvalidArgumentsReturn2WithoutDeviceAccess(params string[] args)
    {
        provider.DiscoveryError = new Exception("Should not discover");
        Assert.Equal(2, Run(args));
        Assert.NotEmpty(error.ToString());
    }

    [Fact]
    public void HelpReturns0WithoutDiscovery()
    {
        provider.DiscoveryError = new Exception("Should not discover");
        Assert.Equal(0, Run("--help"));
        Assert.Contains("probe --device", output.ToString());
        Assert.Contains("trust", output.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ExactIdWinsBeforeFriendlyName()
    {
        provider.Devices =
        [
            FakePhotoDevice.Info("other", "phone"),
            FakePhotoDevice.Info("phone", "Selected"),
        ];
        Assert.Equal(0, Run("probe", "--device", "phone"));
        Assert.Equal("Selected", provider.OpenedDevice?.FriendlyName);
    }

    [Fact]
    public void UniqueNameSelectsDevice()
    {
        provider.Devices = [FakePhotoDevice.Info("id", "My Phone")];
        Assert.Equal(0, Run("probe", "--device", "my phone"));
        Assert.Equal("id", provider.OpenedDevice?.Id);
    }

    [Fact]
    public void DuplicateNameShowsIdsAndReturns2()
    {
        provider.Devices = [FakePhotoDevice.Info("id-1"), FakePhotoDevice.Info("id-2")];
        Assert.Equal(2, Run("probe", "--device", "Phone"));
        Assert.Null(provider.OpenedDevice);
        Assert.Contains("id-1", error.ToString());
        Assert.Contains("id-2", error.ToString());
    }

    [Fact]
    public void AbsentDeviceReturns3() => Assert.Equal(3, Run("probe", "--device", "not-attached"));

    [Fact]
    public void EmptyDiscoveryListsNoDevicesAndReturns0()
    {
        provider.Devices = [];
        Assert.Equal(0, Run("list-devices"));
        Assert.Contains("No portable devices", output.ToString());
    }

    [Fact]
    public void DiscoveryAndConnectionFailuresReturn3()
    {
        provider.DiscoveryError = new IOException("Discovery failed");
        Assert.Equal(3, Run("list-devices"));
        Assert.Contains("Discovery failed", error.ToString());
        provider.DiscoveryError = null;
        provider.OpenError = new IOException("Trust required");
        Assert.Equal(3, Run("probe", "--device", "phone"));
        Assert.Contains("Trust required", error.ToString());
    }

    [Fact]
    public void ExistingOutputFileRemainsByteIdentical()
    {
        var path = Path.Combine(temp, "existing.json");
        File.WriteAllText(path, "keep this");
        Assert.Equal(5, Run("probe", "--device", "phone", "--output", path));
        Assert.Equal("keep this", File.ReadAllText(path));
    }

    [Fact]
    public void PartialReportExportsAndReturns4()
    {
        provider.Device.Failures["root"] = new UnauthorizedAccessException("Denied");
        var path = Path.Combine(temp, "partial.json");
        Assert.Equal(4, Run("probe", "--device", "phone", "--output", path));
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("incomplete", json.RootElement.GetProperty("completionStatus").GetString());
        Assert.Equal(
            "Denied",
            json.RootElement.GetProperty("errors")[0].GetProperty("message").GetString()
        );
        Assert.True(provider.Device.IsDisposed);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CancelledProbePreservesPartialAndExit130(bool failExport)
    {
        using var cts = new CancellationTokenSource();
        var path = Path.Combine(temp, "cancelled.json");
        provider.Device.OnRead = _ =>
        {
            cts.Cancel();
            if (failExport)
                File.WriteAllText(path, "racing writer");
        };
        var exit = new ProbeCli(provider, output, error).Run(
            ["probe", "--device", "phone", "--output", path],
            cts.Token
        );
        Assert.Equal(130, exit);
        Assert.True(provider.Device.IsDisposed);
        if (failExport)
        {
            Assert.NotEmpty(error.ToString());
            Assert.Equal("racing writer", File.ReadAllText(path));
        }
        else
        {
            using var json = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("cancelled", json.RootElement.GetProperty("completionStatus").GetString());
            Assert.Equal(1, json.RootElement.GetProperty("photoCount").GetInt32());
        }
    }

    [Fact]
    public void WrappedBackendDisconnectExportsPartialInsteadOfCrashing()
    {
        provider.Device.Children["root"] =
        [
            FakePhotoDevice.Entry("p", "\\DCIM\\a.jpg"),
            FakePhotoDevice.Entry("q", "\\DCIM\\b.jpg"),
        ];
        provider.Device.OnRead = entry =>
        {
            if (entry.ObjectId == "q")
                WpdOperation.Run<int>(
                    () =>
                        throw new AggregateException(
                            new MediaDevices.MediaDeviceException(
                                "COM Error 0x800701b1 in GetValues"
                            )
                        ),
                    default
                );
        };
        var path = Path.Combine(temp, "disconnected.json");
        Assert.Equal(4, Run("probe", "--device", "phone", "--output", path));
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal(1, json.RootElement.GetProperty("photoCount").GetInt32());
        Assert.Equal("incomplete", json.RootElement.GetProperty("completionStatus").GetString());
        Assert.True(provider.Device.IsDisposed);
    }

    [Fact]
    public void CleanupFailureCannotOverrideCancellationOrEraseReport()
    {
        using var cts = new CancellationTokenSource();
        provider.Device.OnRead = _ => cts.Cancel();
        provider.Device.DisposeError = new IOException("USB disconnected during cleanup");
        var path = Path.Combine(temp, "cleanup.json");
        var result = new ProbeCli(provider, output, error).Run(
            ["probe", "--device", "phone", "--output", path],
            cts.Token
        );
        Assert.Equal(130, result);
        Assert.Contains("cleanup", error.ToString(), StringComparison.OrdinalIgnoreCase);
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        Assert.Equal("cancelled", json.RootElement.GetProperty("completionStatus").GetString());
    }

    [Fact]
    public void SuccessfulProbeDisposesDeviceWithoutNeedingCaptureDates()
    {
        Assert.Equal(0, Run("probe", "--device", "phone"));
        Assert.True(provider.Device.IsDisposed);
        Assert.Contains("Missing: 1", output.ToString());
    }

    [Theory]
    [InlineData("missing-folder\\out.json")]
    [InlineData("invalid\0.json")]
    public void InvalidOutputReturns5(string relative)
    {
        Assert.Equal(
            5,
            Run("probe", "--device", "phone", "--output", Path.Combine(temp, relative))
        );
    }

    [Fact]
    public void OutputRaceDoesNotOverwriteAndReturns5()
    {
        var path = Path.Combine(temp, "race.json");
        provider.Device.OnRead = _ => File.WriteAllText(path, "other writer");
        Assert.Equal(5, Run("probe", "--device", "phone", "--output", path));
        Assert.Equal("other writer", File.ReadAllText(path));
        Assert.True(provider.Device.IsDisposed);
    }

    public void Dispose() => Directory.Delete(temp, true);
}
