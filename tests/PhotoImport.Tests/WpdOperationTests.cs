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

    [Theory]
    [InlineData(unchecked((int)0x80070079),"0x80070079")]
    [InlineData(unchecked((int)0x8007001f),"0x8007001F")]
    [InlineData(unchecked((int)0x80070141),"0x80070141")]
    [InlineData(unchecked((int)0x802a0002),"0x802A0002")]
    public void ObservedSessionFailureStopsBothNativeAndWorkerCalls(int hresult,string code)
    {
        Assert.Throws<DeviceDisconnectedException>(()=>WpdOperation.Run<int>(()=>throw new COMException("session failed",hresult),default));
        var error=new AggregateException(new MediaDeviceException($"COM Error {code} in IPortableDeviceResources.GetStream: session failed"));
        var translated=Assert.Throws<DeviceDisconnectedException>(()=>WpdOperation.Run<int>(()=>throw error,default));
        Assert.Contains(code,translated.Message);Assert.Same(error.InnerException,translated.InnerException);
    }

    [Theory]
    [InlineData("Failed to read path \\Camera\\COM Error 0x80070079 in filename.jpg")]
    [InlineData("COM Error 0x800700790 in unknown code")]
    [InlineData("COM Error 0x80070005 in GetStream with 0x80070141 in filename")]
    public void CodesInPathsOrLongerCodesDoNotBecomeTerminal(string message)
    {
        var failure=Assert.Throws<IOException>(()=>WpdOperation.Run<int>(()=>throw new MediaDeviceException(message),default));
        Assert.IsNotType<DeviceDisconnectedException>(failure);
    }
}
