using PhotoImport.Models;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace PhotoImport.Reporting;

public sealed class PreviewBundle:IDisposable
{
    private static readonly JsonSerializerOptions Options=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true};
    private readonly Dictionary<string,Stream> streams=[];
    private readonly HashSet<string> written=[];
    private bool disposed;
    private PreviewBundle(){}

    public static PreviewBundle Reserve(string outputDirectory)=>Reserve(outputDirectory,path=>new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None));
    internal static PreviewBundle Reserve(string outputDirectory,Func<string,Stream> open)
    {
        var path=Path.GetFullPath(outputDirectory).TrimEnd(Path.DirectorySeparatorChar,Path.AltDirectorySeparatorChar);
        if(!CreateDirectory(path,IntPtr.Zero))
        {var error=Marshal.GetLastWin32Error();throw new IOException("Cannot reserve new report directory: "+new Win32Exception(error).Message,new Win32Exception(error));}
        var bundle=new PreviewBundle();
        try
        {
            foreach(var name in new[]{"inventory.json","capture.json","preview.json","run.json"})bundle.streams.Add(name,open(Path.Combine(path,name)));
            return bundle;
        }
        catch
        {
            try{bundle.Dispose();}catch(IOException){} // Preserve the reservation failure.
            throw;
        }
    }
    public void WriteInventory(ProbeReport report)=>Write("inventory.json",report);
    public void WriteCapture(JpegExifReport report)=>Write("capture.json",report);
    public void WritePreview(PreviewReport report)=>Write("preview.json",report);
    public void WriteManifest(LivePreviewManifest report)=>Write("run.json",report);
    private void Write<T>(string name,T report)
    {
        ObjectDisposedException.ThrowIf(disposed,this);
        if(!written.Add(name))throw new InvalidOperationException("A report export has already been attempted: "+name);
        var stream=streams[name];JsonSerializer.Serialize(stream,report,Options);
        if(stream is FileStream file)file.Flush(flushToDisk:true);else stream.Flush();
    }
    public void Dispose()
    {
        if(disposed)return;disposed=true;
        var errors=new List<Exception>();
        foreach(var stream in streams.Values)
            try{stream.Dispose();}catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){errors.Add(ex);}
        if(errors.Count>0)throw new IOException("Report handle cleanup failed.",new AggregateException(errors));
    }

    [DllImport("kernel32.dll",EntryPoint="CreateDirectoryW",CharSet=CharSet.Unicode,SetLastError=true)]
    [return:MarshalAs(UnmanagedType.Bool)]
    private static extern bool CreateDirectory(string path,IntPtr securityAttributes);
}
