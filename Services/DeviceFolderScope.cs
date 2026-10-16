using PhotoImport.Devices;
using PhotoImport.Models;

namespace PhotoImport.Services;

public static class DeviceFolderScope
{
    public static void Validate(IReadOnlyList<string> paths)
    {
        if(paths.Count==0)throw new PreviewInputException("At least one device folder is required.");
        foreach(var path in paths)
            if(string.IsNullOrWhiteSpace(path)||!path.StartsWith('\\')||path.StartsWith("\\\\",StringComparison.Ordinal)
                ||path.IndexOfAny(['/','*','?',':','\0'])>=0||path.Split('\\').Skip(1).Any(p=>p.Length==0||p is "." or ".."))
                throw new PreviewInputException("Invalid absolute device folder: "+path);
        for(var i=0;i<paths.Count;i++)for(var j=0;j<i;j++)
            if(IsWithin(paths[i],paths[j])||IsWithin(paths[j],paths[i]))throw new PreviewInputException("Duplicate or nested device folders.");
    }

    public static IReadOnlyList<DeviceEntry> Resolve(IPhotoDevice device,IReadOnlyList<string> paths,CancellationToken token)
    {
        Validate(paths);
        var storage=device.GetStorageRoots(token);
        var listings=new Dictionary<string,IReadOnlyList<DeviceEntry>>(StringComparer.Ordinal);
        var resolved=new List<DeviceEntry>();
        foreach(var path in paths)
        {
            token.ThrowIfCancellationRequested();
            var matches=storage.Where(e=>IsWithin(path,e.Path)).ToArray();
            if(matches.Length!=1)throw new IOException("Missing or ambiguous storage for folder: "+path);
            var current=matches[0];
            while(!current.Path.Equals(path,StringComparison.OrdinalIgnoreCase))
            {
                token.ThrowIfCancellationRequested();
                if(!current.IsDirectory)throw new IOException("Folder resolves to a file: "+path);
                var key=current.ObjectId+"\0"+current.Path;
                if(!listings.TryGetValue(key,out var children))
                {
                    children=device.GetChildren(current,token).ToArray();
                    if(children.Any(e=>!IsDirectChild(e.Path,current.Path)))throw new IOException("Invalid child path while resolving "+path);
                    listings.Add(key,children);
                }
                matches=children.Where(e=>IsWithin(path,e.Path)).ToArray();
                if(matches.Length!=1)throw new IOException("Missing or ambiguous device folder: "+path);
                current=matches[0];
            }
            if(!current.IsDirectory)throw new IOException("Folder resolves to a file: "+path);
            resolved.Add(current);
        }
        Validate(resolved.Select(e=>e.Path).ToArray());
        return resolved;
    }

    internal static bool IsWithin(string path,string root)=>path.Equals(root,StringComparison.OrdinalIgnoreCase)
        ||path.StartsWith(root.TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase);
    internal static bool IsDirectChild(string path,string parent)
    {
        var prefix=parent.TrimEnd('\\')+"\\";
        return path.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)&&path.Length>prefix.Length
            &&path.AsSpan(prefix.Length).IndexOf('\\')<0&&path.AsSpan(prefix.Length) is not "." and not "..";
    }
}
