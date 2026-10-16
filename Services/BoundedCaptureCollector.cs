using PhotoImport.Devices;
using PhotoImport.Models;
using System.Diagnostics;

namespace PhotoImport.Services;

public static class BoundedCaptureCollector
{
    public static CaptureCollectionResult Run(IPhotoContentDevice content,ProbeReport inventory,int maxPhotos,CancellationToken token,Action<string>? progress=null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maxPhotos);
        var requested=new List<string>();
        var capture=new JpegExifReport{Device=inventory.Device,RequestedPaths=requested};
        var timer=Stopwatch.StartNew();
        var candidates=inventory.Photos.GroupBy(p=>p.SourcePath,StringComparer.OrdinalIgnoreCase)
            .Where(g=>g.Count()==1).Select(g=>g.Single())
            .Where(p=>p.SizeBytes is >0&&new[]{".jpg",".jpeg",".heic",".heif",".dng"}.Contains(Path.GetExtension(p.SourcePath),StringComparer.OrdinalIgnoreCase))
            .OrderBy(p=>p.SourcePath,StringComparer.Ordinal).ToArray();
        long bytes=0;var disconnected=false;
        if(inventory.CompletionStatus!="complete"||inventory.Errors.Count>0)capture.Errors.Add("Inventory is partial or contains errors.");
        foreach(var photo in candidates)
        {
            if(token.IsCancellationRequested||requested.Count==maxPhotos)break;
            progress?.Invoke(photo.SourcePath);
            if(token.IsCancellationRequested)break;
            requested.Add(photo.SourcePath);
            var watch=Stopwatch.StartNew();
            PhotoReadHandle? handle=null;PhotoSourceInfo? source=null;ReadObserver? observer=null;
            JpegExifObservation observation;
            try
            {
                handle=content.OpenPhotoRead(photo.SourcePath,token);source=handle.Source;
                if(source.CanonicalPath!=photo.SourcePath||source.SizeBytes!=photo.SizeBytes
                    ||!string.IsNullOrWhiteSpace(source.PersistentObjectId)&&!string.IsNullOrWhiteSpace(photo.PersistentObjectId)&&source.PersistentObjectId!=photo.PersistentObjectId)
                    throw new IOException("Source changed relative to scoped inventory.");
                observer=new(handle.Stream);
                observation=MediaExifReader.Read(observer,Path.GetExtension(photo.SourcePath),token);
                disconnected=observer.Disconnected;
            }
            catch(OperationCanceledException){observation=Failed("cancelled","Cancelled during capture.",observer?.BytesRead??0);}
            catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
            {
                disconnected=ex is DeviceDisconnectedException||observer?.Disconnected==true;
                observation=Failed(token.IsCancellationRequested?"cancelled":"incomplete",ex.Message,observer?.BytesRead??0);
            }
            finally
            {
                try{handle?.Dispose();}
                catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException)
                {capture.Errors.Add($"Stream cleanup failed for {photo.SourcePath}: {ex.Message}");disconnected|=ex is DeviceDisconnectedException;}
            }
            bytes+=observer?.BytesRead??0;
            capture.Files.Add(new(photo.SourcePath,watch.Elapsed.TotalSeconds,observation){Source=source});
            if(disconnected||token.IsCancellationRequested)break;
        }
        var limitReached=requested.Count==maxPhotos&&candidates.Length>requested.Count;
        capture.ElapsedSeconds=timer.Elapsed.TotalSeconds;
        capture.CompletionStatus=token.IsCancellationRequested?"cancelled":disconnected||limitReached||capture.Errors.Count>0
            ||capture.Files.Any(f=>f.Observation.CaptureDayStatus!="available")?"incomplete":"complete";
        return new(capture,requested.Count,bytes,limitReached,disconnected);
    }

    private static JpegExifObservation Failed(string outcome,string message,int bytes)=>new(bytes,outcome,new Dictionary<string,string?>(),null,null,[],message);

    // Observe transport failures before the existing readers turn them into per-file outcomes.
    private sealed class ReadObserver(Stream source):Stream
    {
        public int BytesRead{get;private set;}
        public bool Disconnected{get;private set;}
        public override int Read(byte[] buffer,int offset,int count)
        {
            try{var read=source.Read(buffer,offset,count);BytesRead+=read;return read;}
            catch(DeviceDisconnectedException){Disconnected=true;throw;}
        }
        public override bool CanRead=>true;
        public override bool CanSeek=>false;
        public override bool CanWrite=>false;
        public override long Length=>throw new NotSupportedException();
        public override long Position{get=>throw new NotSupportedException();set=>throw new NotSupportedException();}
        public override void Flush()=>throw new NotSupportedException();
        public override long Seek(long offset,SeekOrigin origin)=>throw new NotSupportedException();
        public override void SetLength(long value)=>throw new NotSupportedException();
        public override void Write(byte[] buffer,int offset,int count)=>throw new NotSupportedException();
    }
}
