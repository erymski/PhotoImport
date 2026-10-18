using System.Text;

namespace PhotoImport.Tests.Fakes;

internal static class JpegFixture
{
    // Hand-built little-endian TIFF: IFD0 DateTime + pointer to an EXIF sub-IFD.
    public static byte[] Create(string original = "2026:10:15 19:17:14", string? offset = "+02:00",
        string? subseconds = "762", bool conflict = false, ushort? orphanTag = null)
    {
        using var tiff = new MemoryStream();
        using var writer = new BinaryWriter(tiff, Encoding.ASCII, leaveOpen: true);
        writer.Write((byte)'I'); writer.Write((byte)'I'); writer.Write((ushort)42); writer.Write((uint)8);
        var sub = new List<(ushort Tag, string Value)> { (0x9003, original), (0x9004, original) };
        if (offset is not null) sub.Add((0x9011, offset));
        if (subseconds is not null) sub.Add((0x9291, subseconds));
        const uint subOffset = 38; // 8-byte TIFF header + 2 + 2*12 + 4 IFD0 bytes.
        var secondDirectory = conflict || orphanTag is not null;
        var secondTag = orphanTag ?? 0x9003;
        var secondValue = orphanTag switch { 0x9011 => "+03:00", 0x9291 => "123", _ => "2020:01:01 00:00:00" };
        var nextIfd = subOffset + 2 + (uint)sub.Count * 12 + 4;
        var dataOffset = nextIfd + (secondDirectory ? 36u : 0u);
        writer.Write((ushort)2);
        Entry(writer, 0x0132, "2026:10:15 19:17:17", dataOffset);
        writer.Write((ushort)0x8769); writer.Write((ushort)4); writer.Write((uint)1); writer.Write(subOffset);
        writer.Write(secondDirectory ? nextIfd : 0u);
        writer.Write((ushort)sub.Count);
        var nextOffset = dataOffset + 20;
        foreach (var (tag, value) in sub)
        {
            Entry(writer, tag, value, nextOffset);
            if (value.Length + 1 > 4) nextOffset += (uint)value.Length + 1;
        }
        writer.Write((uint)0);
        if (secondDirectory)
        {
            writer.Write((ushort)1);
            writer.Write((ushort)0x014A); writer.Write((ushort)4); writer.Write((uint)1); writer.Write(nextIfd + 18);
            writer.Write((uint)0);
            writer.Write((ushort)1);
            Entry(writer, secondTag, secondValue, nextOffset);
            writer.Write((uint)0);
        }
        writer.Write(Encoding.ASCII.GetBytes("2026:10:15 19:17:17\0"));
        foreach (var (_, value) in sub.Where(v => v.Value.Length + 1 > 4))
            writer.Write(Encoding.ASCII.GetBytes(value + "\0"));
        if (secondDirectory && secondValue.Length + 1 > 4) writer.Write(Encoding.ASCII.GetBytes(secondValue + "\0"));
        var payload = "Exif\0\0"u8.ToArray().Concat(tiff.ToArray()).ToArray();
        return new byte[] { 0xFF, 0xD8 }.Concat(Segment(0xE1, payload)).Concat(new byte[] { 0xFF, 0xDA, 1, 2, 3, 4 }).ToArray();
    }

    private static void Entry(BinaryWriter writer, ushort tag, string value, uint offset)
    {
        writer.Write(tag); writer.Write((ushort)2); writer.Write((uint)value.Length + 1);
        if (value.Length + 1 > 4) writer.Write(offset);
        else { var inline = new byte[4]; Encoding.ASCII.GetBytes(value).CopyTo(inline, 0); writer.Write(inline); }
    }

    public static byte[] Segment(byte marker, byte[] payload)
    {
        var length = payload.Length + 2;
        return new byte[] { 0xFF, marker, (byte)(length >> 8), (byte)length }.Concat(payload).ToArray();
    }
}

internal sealed class ForwardOnlyStream(byte[] bytes, int chunk = int.MaxValue) : Stream
{
    public int Consumed { get; private set; }
    public bool Disposed { get; private set; }
    public Action? OnRead { get; set; }
    public Exception? ReadError { get; set; }
    public Exception? DisposeError { get; set; }
    public override int Read(byte[] buffer, int offset, int count)
    {
        OnRead?.Invoke();
        if (ReadError is not null) throw ReadError;
        var available = Math.Min(Math.Min(count, chunk), bytes.Length - Consumed);
        bytes.AsSpan(Consumed, available).CopyTo(buffer.AsSpan(offset, available));
        Consumed += available; return available;
    }
    public override bool CanRead => true;
    public override bool CanSeek => false;
    public override bool CanWrite => false;
    public override long Length => throw new NotSupportedException();
    public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
    public override void Flush() => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();
    public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    protected override void Dispose(bool disposing) { Disposed = true; if (DisposeError is not null) throw DisposeError; base.Dispose(disposing); }
}
