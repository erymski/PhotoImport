using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Services;
namespace PhotoImport.Importing;
public static class ScopedMediaInventory
{
    private static readonly string[] Allowed=["\\Internal storage\\DCIM\\Camera","\\Internal storage\\DCIM\\Screenshots","\\Internal storage\\Samsung\\Pictures\\Camera Roll"];
    public static void ValidateRoots(IReadOnlyList<string> roots)
    {
        DeviceFolderScope.Validate(roots);
        if(roots.Any(r=>!Allowed.Any(a=>DeviceFolderScope.IsWithin(r,a))))throw new ImportException("Importer roots must be approved camera/screenshot folders or their subfolders.");
    }
    public static ImportInventory Read(IPhotoDevice device,IReadOnlyList<string> roots,CancellationToken token)
    {
        ValidateRoots(roots);
        IReadOnlyList<DeviceEntry> resolved;
        try{resolved=DeviceFolderScope.Resolve(device,roots,token);}
        catch(IOException ex){throw new ImportException("Cannot resolve selected device folders: "+ex.Message,3,ex);}
        var files=new List<PhotoMetadata>();var errors=new List<string>();
        var paths=new HashSet<string>(StringComparer.OrdinalIgnoreCase);var ids=new HashSet<string>(StringComparer.Ordinal);
        var stack=new Stack<DeviceEntry>(resolved);
        try
        {
            while(stack.TryPop(out var directory))
            {
                token.ThrowIfCancellationRequested();
                if(directory.Name.Equals(".thumbnails",StringComparison.OrdinalIgnoreCase)||directory.Name.Equals(".trash",StringComparison.OrdinalIgnoreCase))continue;
                if(!paths.Add(directory.Path)||!string.IsNullOrEmpty(directory.ObjectId)&&!ids.Add(directory.ObjectId))
                {errors.Add("Repeated directory/cycle: "+directory.Path);continue;}
                foreach(var entry in device.GetChildren(directory,token))
                {
                    if(!DeviceFolderScope.IsDirectChild(entry.Path,directory.Path)){errors.Add("Noncanonical child: "+entry.Path);continue;}
                    if(entry.IsDirectory){stack.Push(entry);continue;}
                    if(!paths.Add(entry.Path)){errors.Add("Duplicate file path: "+entry.Path);continue;}
                    if(FilenameDayPolicy.Classify(entry.Name) is null)continue;
                    var source=device.ReadPhotoMetadata(entry,token);
                    if(source.SourcePath!=entry.Path||source.FileName!=Path.GetFileName(entry.Path))errors.Add("Metadata/path association differs: "+entry.Path);
                    else files.Add(source);
                }
            }
        }
        catch(OperationCanceledException){throw;}
        catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException){errors.Add(ex.Message);}
        return new(files,errors,["MediaDevices may suppress native enumeration errors; API traversal is not an independent physical census."],errors.Count==0);
    }
}
