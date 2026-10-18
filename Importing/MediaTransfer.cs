using System.Security.Cryptography;
using PhotoImport.Devices;
namespace PhotoImport.Importing;
public enum TransferPoint {AfterPrepared,AfterRename,AfterCompleted}
public sealed class MediaTransfer(Func<string,long>? freeBytes=null,Action<TransferPoint>? fault=null)
{
    public const long ReserveBytes=2L*1024*1024*1024;
    public long AvailableBytes(string destination)=>(freeBytes??(p=>new DriveInfo(Path.GetPathRoot(p)!).AvailableFreeSpace))(destination);
    public void CheckSpace(string destination,long bytes)
    {if(bytes<0||AvailableBytes(destination)<ReserveBytes||AvailableBytes(destination)-ReserveBytes<bytes)throw new IOException("Insufficient free space (2 GiB reserve required).");}
    private static PendingImport Replace(PendingImport session,PendingImportFile file)=>session with {Files=session.Files.Select(f=>f.Candidate.Key==file.Candidate.Key?file:f).ToArray()};
    public PendingImport TryRecover(PendingImport session,PendingImportFile file,Action<PendingImport> persist,CancellationToken token)
    {
        var destination=new ImportDestination(session.Registration.Destination);destination.ValidateOwnedPaths(session,file);
        if(file.Stage=="planned")return session;
        using var lease=ImportDirectoryLease.Acquire(destination.Root,session.SessionId);
        if(Matches(file.FinalPath,file,token))
        {var recovered=Replace(session,file with {Stage="completed"});persist(recovered);return recovered;}
        if(file.Stage=="prepared"&&Matches(file.PartialPath,file,token))return Finalize(session,file,destination,persist,token);
        var reset=file with {Stage="planned",Length=0,Sha256=null,FinalPath=destination.ChooseFinalPath(file.Candidate)};
        var changed=Replace(session,reset);persist(changed);return changed;
    }
    private static bool Matches(string path,PendingImportFile file,CancellationToken token)
    {
        if(!File.Exists(path)||new FileInfo(path).Length!=file.Length)return false;
        ImportDestination.RejectReparse(path);
        using var stream=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
        using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);var buffer=new byte[128*1024];int count;
        while((count=stream.Read(buffer))>0){token.ThrowIfCancellationRequested();hash.AppendData(buffer,0,count);}
        token.ThrowIfCancellationRequested();return Convert.ToHexString(hash.GetHashAndReset())==file.Sha256;
    }
    public PendingImport Copy(IMediaContentDevice device,PendingImport session,PendingImportFile file,Action<PendingImport> persist,CancellationToken token)
    {
        token.ThrowIfCancellationRequested();var destination=new ImportDestination(session.Registration.Destination);
        destination.ValidateOwnedPaths(session,file);CheckSpace(destination.Root,checked((long)file.Candidate.Source.SizeBytes!.Value));
        using var lease=ImportDirectoryLease.Acquire(destination.Root,session.SessionId);
        destination.ValidateOwnedPaths(session,file);
        if(File.Exists(file.PartialPath))File.Delete(file.PartialPath); // validated journal-owned incomplete file only
        PhotoReadHandle handle;
        try{handle=device.OpenMediaRead(file.Candidate.Source.SourcePath,token);}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){throw new ImportException("Cannot open source: "+file.Candidate.Source.SourcePath+": "+ex.Message,4,ex);}
        using(handle)
        {
            var expected=file.Candidate.Source;var actual=handle.Source;
            if(actual.CanonicalPath!=expected.SourcePath||actual.SizeBytes!=expected.SizeBytes
                ||!string.IsNullOrWhiteSpace(actual.PersistentObjectId)&&!string.IsNullOrWhiteSpace(expected.PersistentObjectId)&&actual.PersistentObjectId!=expected.PersistentObjectId)
                throw new ImportException("Source changed since inventory: "+expected.SourcePath,4);
            using(var output=new FileStream(file.PartialPath,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            using(var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer=new byte[128*1024];long received=0;
                while(true)
                {
                    token.ThrowIfCancellationRequested();int count;
                    try{count=handle.Stream.Read(buffer,0,buffer.Length);}
                    catch(IOException ex){throw new ImportException("USB read failed: "+expected.SourcePath,4,ex);}
                    if(count==0)break;
                    received=checked(received+count);
                    if((ulong)received>expected.SizeBytes)throw new ImportException("Received more than reported source size.",4);
                    CheckSpace(destination.Root,count);output.Write(buffer,0,count);hash.AppendData(buffer,0,count);
                }
                output.Flush(true);
                if((ulong)received!=expected.SizeBytes||output.Length!=received)throw new ImportException("Received/saved length differs from reported source size.",4);
                file=file with {Stage="prepared",Length=received,Sha256=Convert.ToHexString(hash.GetHashAndReset())};
            }
        }
        session=Replace(session,file);persist(session);fault?.Invoke(TransferPoint.AfterPrepared);
        return Finalize(session,file,destination,persist,token);
    }
    private PendingImport Finalize(PendingImport session,PendingImportFile file,ImportDestination destination,Action<PendingImport> persist,CancellationToken token)
    {
        for(var attempts=0;attempts<10000;attempts++)
        {
            token.ThrowIfCancellationRequested();destination.ValidateOwnedPaths(session,file);
            if(File.Exists(file.FinalPath)||Directory.Exists(file.FinalPath))
            {file=file with {FinalPath=destination.ChooseFinalPath(file.Candidate)};session=Replace(session,file);persist(session);}
            try{File.Move(file.PartialPath,file.FinalPath,false);break;}
            catch(IOException) when(File.Exists(file.FinalPath)||Directory.Exists(file.FinalPath)){continue;}
        }
        if(File.Exists(file.PartialPath))throw new IOException("Could not reserve final filename.");
        fault?.Invoke(TransferPoint.AfterRename);
        session=Replace(session,file with {Stage="completed"});persist(session);fault?.Invoke(TransferPoint.AfterCompleted);return session;
    }
}
