using System.ComponentModel;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PhotoImport.Importing;

// Keep destination and owned staging directories from being renamed/replaced
// while a synchronous USB request or filesystem operation is in flight.
internal sealed class ImportDirectoryLease : IDisposable
{
    private readonly List<SafeFileHandle> handles = [];

    public static ImportDirectoryLease Acquire(string destination, Guid sessionId)
    {
        var lease = new ImportDirectoryLease();
        try
        {
            lease.Pin(destination);
            var staging = Path.Combine(destination, ".phone-photo-sync");
            if (!Directory.Exists(staging))
                Directory.CreateDirectory(staging);
            lease.Pin(staging);
            var session = Path.Combine(staging, sessionId.ToString("N"));
            if (!Directory.Exists(session))
                Directory.CreateDirectory(session);
            lease.Pin(session);
            return lease;
        }
        catch
        {
            lease.Dispose();
            throw;
        }
    }

    private void Pin(string path)
    {
        // OPEN_REPARSE_POINT inspects the directory itself, rather than its target.
        // Share read/write, but deliberately deny delete (which also denies rename).
        var handle = CreateFileW(path, 0x80000000, 3, IntPtr.Zero, 3, 0x02200000, IntPtr.Zero);
        if (handle.IsInvalid)
        {
            handle.Dispose();
            throw new IOException(
                "Cannot hold importer directory: " + path,
                new Win32Exception(Marshal.GetLastWin32Error())
            );
        }
        handles.Add(handle);
        if (
            !GetFileInformationByHandleEx(
                handle,
                9,
                out var info,
                (uint)Marshal.SizeOf<AttributeTagInfo>()
            )
        )
            throw new IOException(
                "Cannot inspect importer directory: " + path,
                new Win32Exception(Marshal.GetLastWin32Error())
            );
        if (
            (info.Attributes & (uint)FileAttributes.ReparsePoint) != 0
            || (info.Attributes & (uint)FileAttributes.Directory) == 0
        )
            throw new IOException("Importer directories must be ordinary directories: " + path);
    }

    public void Dispose()
    {
        for (var i = handles.Count - 1; i >= 0; i--)
            handles[i].Dispose();
        handles.Clear();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct AttributeTagInfo
    {
        public uint Attributes;
        public uint ReparseTag;
    }

    [DllImport(
        "kernel32.dll",
        CharSet = CharSet.Unicode,
        SetLastError = true,
        ExactSpelling = true
    )]
    private static extern SafeFileHandle CreateFileW(
        string name,
        uint access,
        uint share,
        IntPtr security,
        uint creation,
        uint flags,
        IntPtr template
    );

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetFileInformationByHandleEx(
        SafeFileHandle handle,
        int infoClass,
        out AttributeTagInfo info,
        uint size
    );
}
