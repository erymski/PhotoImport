using PhotoImport.Importing;
namespace PhotoImport.Devices;
public interface IMediaContentDevice
{
    PhotoReadHandle OpenMediaRead(string sourcePath,CancellationToken token);
}
public static class MediaSourcePath
{
    public static bool IsValid(string path)=>!string.IsNullOrWhiteSpace(path)&&path.StartsWith('\\')&&!path.StartsWith("\\\\",StringComparison.Ordinal)
        &&path.IndexOfAny(['/','*','?',':','\0'])<0&&path.Split('\\').Skip(1).All(p=>p.Length>0&&p is not "." and not "..")
        &&FilenameDayPolicy.Classify(path) is not null;
}
