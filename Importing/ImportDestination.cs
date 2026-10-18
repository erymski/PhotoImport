using System.Text.RegularExpressions;
namespace PhotoImport.Importing;
public sealed class ImportDestination(string destination)
{
    public string Root {get;}=Path.TrimEndingDirectorySeparator(Path.GetFullPath(destination));
    public static void ValidateBasename(string name)
    {
        if(string.IsNullOrWhiteSpace(name)||name!=Path.GetFileName(name)||name.IndexOfAny(Path.GetInvalidFileNameChars())>=0
            ||name.EndsWith(' ')||name.EndsWith('.')||name.Length>200||name is "." or ".."
            ||Regex.IsMatch(name.Split('.')[0],@"^(CON|PRN|AUX|NUL|COM[1-9¹²³]|LPT[1-9¹²³])$",RegexOptions.IgnoreCase|RegexOptions.CultureInvariant))
            throw new ImportException("Unsafe Windows basename: "+name,4);
    }
    public string ChooseFinalPath(ImportCandidate candidate)
    {
        var name=candidate.Source.FileName;ValidateBasename(name);
        var path=Path.Combine(Root,name);if(!File.Exists(path)&&!Directory.Exists(path))return path;
        var stem=Path.GetFileNameWithoutExtension(name);var extension=Path.GetExtension(name);
        for(var i=0;i<10000;i++)
        {
            path=Path.Combine(Root,stem+"_"+candidate.Key[..12]+(i==0?"":"_"+i)+extension);
            if(!File.Exists(path)&&!Directory.Exists(path))return path;
        }
        throw new IOException("No free destination filename.");
    }
    public string CreatePartialPath(Guid sessionId,string key)=>Path.Combine(Root,".phone-photo-sync",sessionId.ToString("N"),key+".partial");
    public void ValidateOwnedPaths(PendingImport session,PendingImportFile file)
    {
        if(!Directory.Exists(Root))throw new IOException("Configured destination is missing.");
        ValidateBasename(Path.GetFileName(file.FinalPath));
        if(!string.Equals(Path.GetDirectoryName(Path.GetFullPath(file.FinalPath)),Root,StringComparison.OrdinalIgnoreCase)
            ||!string.Equals(file.PartialPath,CreatePartialPath(session.SessionId,file.Candidate.Key),StringComparison.OrdinalIgnoreCase))
            throw new ImportException("Journal contains paths outside owned destination/staging.");
        RejectReparse(Path.Combine(Root,".phone-photo-sync"));
        RejectReparse(Path.GetDirectoryName(file.PartialPath)!);RejectReparse(file.PartialPath);RejectReparse(file.FinalPath);
    }
    internal static void RejectReparse(string path)
    {
        if((File.Exists(path)||Directory.Exists(path))&&(File.GetAttributes(path)&FileAttributes.ReparsePoint)!=0)
            throw new IOException("Refusing importer path redirected by a reparse point: "+path);
    }
}
