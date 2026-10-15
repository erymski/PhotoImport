using System.Runtime.InteropServices;
using MediaDevices;

namespace PhotoImport.Devices;

// MediaDevices runs operations on a worker and can wrap even ordinary device errors.
public static class WpdOperation
{
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
        var removed = error is COMException && error.HResult is unchecked((int)0x800701b1) or unchecked((int)0x8007048f)
            || error is MediaDeviceException &&
               (error.Message.StartsWith("COM Error 0x800701b1 ", StringComparison.OrdinalIgnoreCase)
                || error.Message.StartsWith("COM Error 0x8007048f ", StringComparison.OrdinalIgnoreCase));
        if (error is NotConnectedException || removed)
            return new DeviceDisconnectedException($"The phone is no longer available. Reconnect and unlock it, then retry. {error.Message}", error);
        if (error is IOException or UnauthorizedAccessException) return error;
        return new IOException($"WPD operation failed: {error.Message}", error);
    }
}
