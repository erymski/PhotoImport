using MediaDevices;
using PhotoImport.Devices;
using System.Runtime.InteropServices;

namespace PhotoImport.Tests;

public class WpdOperationTests
{
    [Theory]
    [InlineData("0x800701b1")]
    [InlineData("0x8007048f")]
    public void WorkerWrappedDisconnectBecomesTerminalDeviceError(string code)
    {
        var workerError = new AggregateException("Exception inside ThreadSafeWorker",
            new MediaDeviceException($"COM Error {code} in IPortableDeviceProperties.GetValues for object"));
        var error = Assert.Throws<DeviceDisconnectedException>(() => WpdOperation.Run<int>(() => throw workerError, default));
        Assert.Contains(code, error.Message);
    }

    [Fact]
    public void WorkerWrappedPropertyErrorBecomesRecoverableIoError()
    {
        var workerError = new AggregateException(new MediaDeviceException("COM Error 0x80070005: access denied"));
        var error = Assert.Throws<IOException>(() => WpdOperation.Run<int>(() => throw workerError, default));
        Assert.IsNotType<DeviceDisconnectedException>(error); Assert.Contains("access denied", error.ToString());
    }

    [Fact]
    public void NativeDisconnectHResultBecomesTerminalDeviceError()
    {
        var error = new COMException("Device no longer exists", unchecked((int)0x800701b1));
        Assert.Throws<DeviceDisconnectedException>(() => WpdOperation.Run<int>(() => throw error, default));
    }

    [Fact]
    public void WorkerWrappedCancellationIsPreserved()
    {
        var error = new AggregateException(new OperationCanceledException());
        Assert.Throws<OperationCanceledException>(() => WpdOperation.Run<int>(() => throw error, default));
    }
}
