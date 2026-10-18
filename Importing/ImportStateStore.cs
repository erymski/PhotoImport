using System.Text.Json;
using System.Text.Json.Serialization;
namespace PhotoImport.Importing;

public sealed class ImportStateStore(string stateDirectory,Action<ImportWritePoint>? fault=null)
{
    public string DirectoryPath {get;}=Path.GetFullPath(stateDirectory);
    public string ConfigPath=>Path.Combine(DirectoryPath,"config.json");
    private bool held,writer;
    internal static readonly JsonSerializerOptions Json=new(){PropertyNamingPolicy=JsonNamingPolicy.CamelCase,WriteIndented=true,
        UnmappedMemberHandling=JsonUnmappedMemberHandling.Disallow};
    public IDisposable AcquireLock()=>Lock(true);
    public IDisposable AcquireReadLock()=>Lock(false);
    private IDisposable Lock(bool write)
    {
        if(held)throw new IOException("This state store is already locked.");
        if(write)Directory.CreateDirectory(DirectoryPath);
        var stream=new FileStream(Path.Combine(DirectoryPath,"import.lock"),write?FileMode.OpenOrCreate:FileMode.Open,
            write?FileAccess.ReadWrite:FileAccess.Read,write?FileShare.None:FileShare.Read);
        held=true;writer=write;return new Lease(()=>{stream.Dispose();held=false;writer=false;});
    }
    private sealed class Lease(Action release):IDisposable
    {
        private Action? action=release;
        public void Dispose()=>Interlocked.Exchange(ref action,null)?.Invoke();
    }
    private void Require(bool write=false){if(!held||write&&!writer)throw new IOException("An appropriate importer lock is required.");}
    public ImportConfiguration? ReadConfiguration()
    {
        if(!File.Exists(ConfigPath))return null;Require();
        var value=Read<ImportConfiguration>(ConfigPath);Validate(value);return value;
    }
    public void WriteConfiguration(ImportConfiguration configuration)
    {Require(true);Validate(configuration);Write(ConfigPath,configuration,true);}
    private string PendingPath(Guid id)=>Path.Combine(DirectoryPath,"pending-"+id.ToString("N")+".json");
    public PendingImport? ReadPending(Guid registrationId)
    {
        Require();var path=PendingPath(registrationId);if(!File.Exists(path))return null;
        var value=Read<PendingImport>(path);ValidatePending(value);
        if(value.Registration.Id!=registrationId)throw new ImportException("Pending journal registration mismatch.");
        return value;
    }
    public void WritePending(PendingImport pending){Require(true);ValidatePending(pending);Write(PendingPath(pending.Registration.Id),pending,false);}
    public bool HasPending=>Directory.Exists(DirectoryPath)&&Directory.EnumerateFiles(DirectoryPath,"pending-*.json").Any();
    public void Archive(PendingImport pending,ImportRunResult result)
    {
        Require(true);fault?.Invoke(ImportWritePoint.BeforeArchive);
        var history=Path.Combine(DirectoryPath,"history");Directory.CreateDirectory(history);
        var archive=Path.Combine(history,pending.SessionId.ToString("N")+".json");
        Write(archive,new{session=pending,result},false);
        File.Delete(PendingPath(pending.Registration.Id));
    }
    public static void Validate(ImportConfiguration config)
    {
        if(config is null||config.SchemaVersion!=1||config.Kind!="filenameImporter"||config.Devices is null)throw new ImportException("Unsupported or malformed importer configuration.");
        var ids=new HashSet<Guid>();var aliases=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach(var r in config.Devices)
        {
            if(r is null||r.Id==Guid.Empty||string.IsNullOrWhiteSpace(r.Alias)||!ids.Add(r.Id)||!aliases.Add(r.Alias))throw new ImportException("Invalid or duplicate registration/alias.");
            if(string.IsNullOrWhiteSpace(r.Destination)||!Path.IsPathFullyQualified(r.Destination)||r.DatePolicy!="androidFilenameDayV1")throw new ImportException("Invalid registration destination/date policy.");
            if(r.Identity is null||r.Identity.Mode is not ("serial" or "deviceId")||string.IsNullOrWhiteSpace(r.Identity.DeviceId)
                ||r.Identity.Mode=="serial"&&string.IsNullOrWhiteSpace(r.Identity.SerialNumber))throw new ImportException("Invalid enrolled device identity.");
            if(r.Roots is null)throw new ImportException("Missing roots.");
            ScopedMediaInventory.ValidateRoots(r.Roots);FilenameImportPlanner.ValidateCheckpoint(r.Checkpoint);
        }
        for(var i=0;i<config.Devices.Count;i++)for(var j=0;j<i;j++)
        {
            var a=config.Devices[i].Identity;var b=config.Devices[j].Identity;
            if(a.DeviceId==b.DeviceId||a.Mode=="serial"&&b.Mode=="serial"&&a.SerialNumber==b.SerialNumber&&a.Manufacturer==b.Manufacturer&&a.Model==b.Model)
                throw new ImportException("Device identity is already registered.");
        }
    }
    private static void ValidatePending(PendingImport pending)
    {
        if(pending is null||pending.SchemaVersion!=1||pending.SessionId==Guid.Empty||pending.Registration is null||pending.Files is null
            ||pending.Stage is not ("copying" or "readyToCommit" or "committed"))throw new ImportException("Malformed recovery journal.");
        Validate(new(1,"filenameImporter",[pending.Registration]));
        if(pending.Stage!="copying"&&pending.NewCheckpoint is null)throw new ImportException("Commit journal lacks target checkpoint.");
        if(pending.NewCheckpoint is not null)FilenameImportPlanner.ValidateCheckpoint(pending.NewCheckpoint);
        var keys=new HashSet<string>(StringComparer.Ordinal);
        foreach(var file in pending.Files)
        {
            if(file is null||file.Candidate is null||file.Candidate.Source is null||!keys.Add(file.Candidate.Key)
                ||file.Candidate.Key!=FilenameImportPlanner.CreateKey(pending.Registration.Id,file.Candidate.Source,file.Candidate.Day)
                ||file.Stage is not ("planned" or "prepared" or "completed")||!Path.IsPathFullyQualified(file.FinalPath)||!Path.IsPathFullyQualified(file.PartialPath))
                throw new ImportException("Invalid recovery file entry.");
            if(file.Stage!="planned"&&(file.Length!=(long)file.Candidate.Source.SizeBytes!.Value||file.Sha256 is null||file.Sha256.Length!=64||file.Sha256.Any(c=>!char.IsAsciiHexDigitUpper(c))))
                throw new ImportException("Invalid recovery verification evidence.");
        }
    }
    private static T Read<T>(string path)
    {
        try{return JsonSerializer.Deserialize<T>(File.ReadAllText(path),Json)??throw new ImportException("Empty importer JSON.");}
        catch(JsonException ex){throw new ImportException("Malformed importer JSON: "+path,2,ex);}
    }
    private void Write<T>(string path,T value,bool config)
    {
        var temp=path+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var stream=new FileStream(temp,FileMode.CreateNew,FileAccess.Write,FileShare.None))
            {JsonSerializer.Serialize(stream,value,Json);stream.Flush(true);}
            fault?.Invoke(config?ImportWritePoint.BeforeConfigReplace:ImportWritePoint.BeforeJournalReplace);
            if(File.Exists(path))File.Replace(temp,path,config?path+".bak":null);
            else File.Move(temp,path,false);
            fault?.Invoke(config?ImportWritePoint.AfterConfigReplace:ImportWritePoint.AfterJournalReplace);
        }
        finally{if(File.Exists(temp))File.Delete(temp);}
    }
}
