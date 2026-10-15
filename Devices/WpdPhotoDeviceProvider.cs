using MediaDevices;
using PhotoImport.Models;

namespace PhotoImport.Devices;

public sealed class WpdPhotoDeviceProvider : IPhotoDeviceProvider, IDisposable
{
    private MediaDeviceManager? manager;

    public IReadOnlyList<DeviceInfo> Discover(CancellationToken cancellationToken)
    {
        var result = new List<DeviceInfo>();
        foreach (var device in GetDevices())
        {
            using (device)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var warnings = new List<string>();
                var identifiers = new Dictionary<string, string?> { ["DeviceId"] = device.DeviceId };
                string? friendly = device.FriendlyName, manufacturer = device.Manufacturer, model = null, transport = null;
                try
                {
                    device.Connect(MediaDeviceAccess.GenericRead, MediaDeviceShare.Read | MediaDeviceShare.Write);
                    friendly = Property("FriendlyName", () => device.FriendlyName) ?? friendly;
                    model = Property("Model", () => device.Model);
                    transport = Property("Transport", () => device.Transport?.ToString());
                    identifiers["PnPDeviceID"] = Property("PnPDeviceID", () => device.PnPDeviceID);
                    identifiers["SerialNumber"] = Property("SerialNumber", () => device.SerialNumber);
                    identifiers["Protocol"] = Property("Protocol", () => device.Protocol);
                }
                catch (Exception ex) when (ex is MediaDeviceException or NotConnectedException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
                { warnings.Add($"Could not inspect device properties with read access: {ex.Message}. Unlock the phone and enable USB file transfer/trust."); }
                finally { if (device.IsConnected) device.Disconnect(); }
                result.Add(new(device.DeviceId, friendly, manufacturer, model, transport, identifiers) { Warnings = warnings });

                string? Property(string name, Func<string?> get)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    try { var value = get(); return string.IsNullOrWhiteSpace(value) ? null : value; }
                    catch (Exception ex) when (ex is MediaDeviceException or NotConnectedException or NotSupportedException or System.Runtime.InteropServices.COMException)
                    { warnings.Add($"{name} unavailable: {ex.Message}"); return null; }
                }
            }
        }
        return result;
    }

    public IPhotoDevice Open(DeviceInfo deviceInfo, CancellationToken cancellationToken)
    {
        foreach (var candidate in GetDevices())
        {
            if (!string.Equals(candidate.DeviceId, deviceInfo.Id, StringComparison.Ordinal)) { candidate.Dispose(); continue; }
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                candidate.Connect(MediaDeviceAccess.GenericRead, MediaDeviceShare.Read | MediaDeviceShare.Write);
                return new WpdPhotoDevice(candidate);
            }
            catch
            {
                try { if (candidate.IsConnected) candidate.Disconnect(); }
                finally { candidate.Dispose(); }
                throw;
            }
        }
        throw new IOException("The selected phone is no longer available. Reconnect it and run list-devices.");
    }

    private IEnumerable<MediaDevice> GetDevices()
    {
        manager ??= MediaDeviceManager.Instance;
        return manager.GetDevices() ?? [];
    }

    public void Dispose() { manager?.Dispose(); manager = null; }
}
