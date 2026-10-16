namespace PhotoImport.Devices;

internal sealed class WpdReadStream(Stream source, CancellationToken token) : Stream
{
    private bool disposed;
    public override int Read(byte[] buffer, int offset, int count)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        return WpdOperation.Run(() => source.Read(buffer, offset, count), token);
    }
    public override bool CanRead => !disposed;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing)
    {
        if (disposed) return;
        disposed = true;
        try { if (disposing) WpdOperation.Run(source.Dispose, default); }
        finally { base.Dispose(disposing); }
    }
}
