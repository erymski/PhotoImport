using PhotoImport.Devices;
using PhotoImport.Models;

namespace PhotoImport.Tests.Fakes;

public sealed class FakePhotoDeviceProvider : IPhotoDeviceProvider
{
    public IReadOnlyList<DeviceInfo> Devices { get; set; } = [FakePhotoDevice.Info()];
    public FakePhotoDevice Device { get; set; } = MetadataProbeTests.SimpleDevice();
    public Exception? DiscoveryError { get; set; }
    public Exception? OpenError { get; set; }
    public DeviceInfo? OpenedDevice { get; private set; }
    public IReadOnlyList<DeviceInfo> Discover(CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (DiscoveryError is not null) throw DiscoveryError;
        return Devices;
    }
    public IPhotoDevice Open(DeviceInfo device, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (OpenError is not null) throw OpenError;
        OpenedDevice = device; return Device;
    }
}
