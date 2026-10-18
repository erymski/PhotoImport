using System.Runtime.InteropServices;
using MediaDevices;

namespace PhotoImport.Devices;

// MediaDevices runs operations on a worker and can wrap even ordinary device errors.
public static class WpdOperation
{
    private static readonly uint[] TerminalSessionCodes =
    [0x800701b1,0x8007048f,0x80070079,0x8007001f,0x80070141,0x802a0002];
    public static T Run<T>(Func<T> operation, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try { return operation(); }
        catch (AggregateException ex)
        {
            var failures = ex.Flatten().InnerExceptions;
            throw Translate(failures.Count == 1 ? failures[0] : ex);
        }
        catch (Exception ex) when (ex is MediaDeviceException or NotConnectedException or COMException or NotSupportedException)
        { throw Translate(ex); }
    }

    public static void Run(Action operation, CancellationToken cancellationToken) =>
        Run(() => { operation(); return true; }, cancellationToken);

    private static Exception Translate(Exception error)
    {
        if (error is OperationCanceledException or DeviceDisconnectedException) return error;
        // MediaDeviceException exposes no native error-code property. Its documented diagnostic
        // prefix is the only available code evidence; never classify arbitrary path text.
        // Stop conservatively on observed transport timeouts/device failures and shutdown,
        // too: continuing a failed WPD session can produce thousands of cascading errors.
        var unavailable = error is COMException && TerminalSessionCodes.Contains(unchecked((uint)error.HResult))
            || error is MediaDeviceException &&
               TerminalSessionCodes.Any(code=>error.Message.StartsWith($"COM Error 0x{code:x8} ",StringComparison.OrdinalIgnoreCase));
        if (error is NotConnectedException || unavailable)
            return new DeviceDisconnectedException($"The phone's USB session cannot continue. Reconnect and unlock it, then retry. {error.Message}", error);
        if (error is IOException or UnauthorizedAccessException) return error;
        return new IOException($"WPD operation failed: {error.Message}", error);
    }
}
