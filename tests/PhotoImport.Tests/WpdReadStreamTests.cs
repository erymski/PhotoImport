using MediaDevices;
using PhotoImport.Devices;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public sealed class WpdReadStreamTests
{
    [Fact]
    public void ReadsAndDisposesSourceWhileRejectingMutationAndSeek()
    {
        var source = new ForwardOnlyStream([1, 2, 3]);
        using (var stream = new WpdReadStream(source, default))
        {
            var buffer = new byte[2];
            Assert.Equal(2, stream.Read(buffer, 0, 2));
            Assert.Equal(new byte[] { 1, 2 }, buffer);
            Assert.False(stream.CanWrite);
            Assert.False(stream.CanSeek);
            Assert.Throws<NotSupportedException>(() => stream.Write(buffer, 0, 2));
            Assert.Throws<NotSupportedException>(() => stream.SetLength(0));
            Assert.Throws<NotSupportedException>(() => stream.Seek(0, SeekOrigin.Begin));
        }
        Assert.True(source.Disposed);
    }

    [Fact]
    public void WrappedUsbReadFailureBecomesDisconnect()
    {
        using var stream = new WpdReadStream(
            new ForwardOnlyStream([])
            {
                ReadError = new AggregateException(
                    new MediaDeviceException("COM Error 0x800701b1 in Read")
                ),
            },
            default
        );
        var error = Assert.Throws<DeviceDisconnectedException>(() =>
            stream.Read(new byte[4], 0, 4)
        );
        Assert.Contains("0x800701b1", error.Message);
    }

    [Fact]
    public void CancellationDoesNotPreventStreamCleanup()
    {
        using var cts = new CancellationTokenSource();
        var source = new ForwardOnlyStream([1]);
        using (var stream = new WpdReadStream(source, cts.Token))
        {
            cts.Cancel();
            Assert.Throws<OperationCanceledException>(() => stream.Read(new byte[1], 0, 1));
        }
        Assert.True(source.Disposed);
        Assert.Equal(0, source.Consumed);
    }
}
