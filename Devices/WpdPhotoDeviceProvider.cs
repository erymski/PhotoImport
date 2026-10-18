using MediaDevices;
using PhotoImport.Models;

namespace PhotoImport.Devices;

public sealed class WpdPhotoDeviceProvider : IPhotoDeviceProvider, IDisposable
{
    private MediaDeviceManager? manager;

    public IReadOnlyList<DeviceInfo> Discover(CancellationToken cancellationToken) =>
        WpdOperation.Run(() => DiscoverCore(cancellationToken), cancellationToken);

    private IReadOnlyList<DeviceInfo> DiscoverCore(CancellationToken cancellationToken)
    {
        var result = new List<DeviceInfo>();
        using var resources = CaptureDevices(cancellationToken);
        foreach (var device in resources.Items)
        {
            {
                cancellationToken.ThrowIfCancellationRequested();
                var warnings = new List<string>
                {
                    "MediaDevices can suppress native enumeration errors; completed traversal is not proof of complete photo coverage. Verify against the phone.",
                };
                var identifiers = new Dictionary<string, string?>
                {
                    ["DeviceId"] = device.DeviceId,
                };
                string? friendly = device.FriendlyName,
                    manufacturer = device.Manufacturer,
                    model = null,
                    transport = null;
                try
                {
                    WpdOperation.Run(
                        () =>
                            device.Connect(
                                MediaDeviceAccess.GenericRead,
                                MediaDeviceShare.Read | MediaDeviceShare.Write
                            ),
                        cancellationToken
                    );
                    friendly = Property("FriendlyName", () => device.FriendlyName) ?? friendly;
                    model = Property("Model", () => device.Model);
                    transport = Property("Transport", () => device.Transport?.ToString());
                    identifiers["PnPDeviceID"] = Property("PnPDeviceID", () => device.PnPDeviceID);
                    identifiers["SerialNumber"] = Property(
                        "SerialNumber",
                        () => device.SerialNumber
                    );
                    identifiers["Protocol"] = Property("Protocol", () => device.Protocol);
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    warnings.Add(
                        $"Could not inspect device properties with read access: {ex.Message}. Unlock the phone and enable USB file transfer/trust."
                    );
                }
                finally
                {
                    try
                    {
                        if (device.IsConnected)
                            WpdOperation.Run(device.Disconnect, default);
                    }
                    catch (IOException ex)
                    {
                        warnings.Add($"Device cleanup warning: {ex.Message}");
                    }
                }
                result.Add(
                    new(device.DeviceId, friendly, manufacturer, model, transport, identifiers)
                    {
                        Warnings = warnings,
                    }
                );

                string? Property(string name, Func<string?> get)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try
                    {
                        var value = WpdOperation.Run(get, cancellationToken);
                        return string.IsNullOrWhiteSpace(value) ? null : value;
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        warnings.Add($"{name} unavailable: {ex.Message}");
                        return null;
                    }
                }
            }
        }
        return result;
    }

    public IPhotoDevice Open(DeviceInfo deviceInfo, CancellationToken cancellationToken)
    {
        using var resources = CaptureDevices(cancellationToken);
        foreach (var candidate in resources.Items)
        {
            if (!string.Equals(candidate.DeviceId, deviceInfo.Id, StringComparison.Ordinal))
                continue;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                WpdOperation.Run(
                    () =>
                        candidate.Connect(
                            MediaDeviceAccess.GenericRead,
                            MediaDeviceShare.Read | MediaDeviceShare.Write
                        ),
                    cancellationToken
                );
                resources.Retain(candidate);
                return new WpdPhotoDevice(candidate);
            }
            catch
            {
                try
                {
                    if (candidate.IsConnected)
                        WpdOperation.Run(candidate.Disconnect, default);
                }
                catch (IOException ex)
                {
                    Console.Error.WriteLine($"Device cleanup warning: {ex.Message}");
                }
                throw;
            }
        }
        throw new IOException(
            "The selected phone is no longer available. Reconnect it and run list-devices."
        );
    }

    private OwnedResources<MediaDevice> CaptureDevices(CancellationToken token) =>
        WpdOperation.Run(
            () =>
            {
                manager ??= MediaDeviceManager.Instance;
                return OwnedResources<MediaDevice>.Capture(
                    manager.GetDevices() ?? [],
                    token,
                    Console.Error.WriteLine
                );
            },
            token
        );

    public void Dispose()
    {
        try
        {
            if (manager is not null)
                WpdOperation.Run(manager.Dispose, default);
        }
        finally
        {
            manager = null;
        }
    }
}
