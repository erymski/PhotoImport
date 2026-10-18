using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;
using PhotoImport.Models;

namespace PhotoImport.Reporting;

public sealed class PreviewBundle : IDisposable
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };
    private readonly Dictionary<string, Stream> streams = [];
    private readonly HashSet<string> written = [];
    private readonly string directory;
    private bool disposed;

    private PreviewBundle(string directory) => this.directory = directory;

    public static PreviewBundle Reserve(string outputDirectory) =>
        Reserve(
            outputDirectory,
            path => new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)
        );

    internal static PreviewBundle Reserve(string outputDirectory, Func<string, Stream> open)
    {
        var path = Path.GetFullPath(outputDirectory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!CreateDirectory(path, IntPtr.Zero))
        {
            var error = Marshal.GetLastWin32Error();
            throw new IOException(
                "Cannot reserve new report directory: " + new Win32Exception(error).Message,
                new Win32Exception(error)
            );
        }
        var bundle = new PreviewBundle(path);
        try
        {
            foreach (
                var name in new[] { "inventory.json", "capture.json", "preview.json", "run.json" }
            )
                bundle.streams.Add(name, open(Path.Combine(path, name)));
            return bundle;
        }
        catch
        {
            try
            {
                bundle.Dispose();
            }
            catch (IOException) { } // Preserve the reservation failure.
            throw;
        }
    }

    public void WriteInventory(ProbeReport report) => Write("inventory.json", report);

    public void WriteCapture(JpegExifReport report) => Write("capture.json", report);

    public void WritePreview(PreviewReport report) => Write("preview.json", report);

    public void WriteManifest(LivePreviewManifest report) => Write("run.json", report);

    private void Write<T>(string name, T report)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!written.Add(name))
            throw new InvalidOperationException(
                "A report export has already been attempted: " + name
            );
        var stream = streams[name];
        JsonSerializer.Serialize(stream, report, Options);
        if (stream is FileStream file)
            file.Flush(flushToDisk: true);
        else
            stream.Flush();
    }

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        var errors = new List<Exception>();
        foreach (var stream in streams.Values)
            try
            {
                stream.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add(ex);
            }
        if (errors.Count > 0)
            throw new IOException(
                "Report handle cleanup failed: " + string.Join("; ", errors.Select(e => e.Message)),
                new AggregateException(errors)
            );
    }

    // Only this run's exclusively reserved reports may be corrected after finalization.
    internal void CorrectFinalStatus(
        LivePreviewManifest manifest,
        JpegExifReport? capture,
        PreviewReport? preview,
        CancellationToken token,
        Action<string> onError
    )
    {
        if (!disposed)
            throw new InvalidOperationException(
                "Close report handles before correcting final status."
            );
        for (var pass = 0; pass < 2; pass++)
        {
            var cancelled = token.IsCancellationRequested;
            manifest.CompletionStatus = cancelled ? "cancelled" : "incomplete";
            if (capture is not null)
            {
                capture.CompletionStatus = manifest.CompletionStatus;
                foreach (var error in manifest.Errors)
                    if (!capture.Errors.Contains(error))
                        capture.Errors.Add(error);
                Rewrite("capture.json", capture);
            }
            if (preview is not null)
            {
                preview.CompletionStatus = manifest.CompletionStatus;
                if (
                    !cancelled
                    && !preview.InputProblems.Contains(
                        "Report output did not finalize successfully."
                    )
                )
                    preview.InputProblems.Add("Report output did not finalize successfully.");
                Rewrite("preview.json", preview);
            }
            Rewrite("run.json", manifest); // Last: includes earlier correction errors.
            if (cancelled || !token.IsCancellationRequested)
                break;
        }

        void Rewrite<T>(string name, T report)
        {
            if (!written.Contains(name))
                return;
            var temporary = Path.Combine(
                directory,
                name + "." + Guid.NewGuid().ToString("N") + ".tmp"
            );
            try
            {
                using (
                    var stream = new FileStream(
                        temporary,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None
                    )
                )
                {
                    JsonSerializer.Serialize(stream, report, Options);
                    stream.Flush(flushToDisk: true);
                }
                File.Move(temporary, Path.Combine(directory, name), overwrite: true);
            }
            catch (Exception ex)
                when (ex
                        is IOException
                            or UnauthorizedAccessException
                            or System.Security.SecurityException
                )
            {
                onError($"Final status correction failed for {name}: {ex.Message}");
            }
        }
    }

    [DllImport(
        "kernel32.dll",
        EntryPoint = "CreateDirectoryW",
        CharSet = CharSet.Unicode,
        SetLastError = true
    )]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectory(string path, IntPtr securityAttributes);
}
