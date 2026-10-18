using System.Globalization;
using System.Text.RegularExpressions;

namespace PhotoImport.Importing;

public static class FilenameDayPolicy
{
    private static readonly HashSet<string> Photos = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg",
        ".jpeg",
        ".png",
        ".heic",
        ".heif",
        ".dng",
        ".tif",
        ".tiff",
    };
    private static readonly HashSet<string> Videos = new(StringComparer.OrdinalIgnoreCase)
    {
        ".mp4",
        ".mov",
        ".m4v",
        ".3gp",
        ".3g2",
        ".avi",
        ".mkv",
        ".webm",
        ".mts",
        ".m2ts",
    };
    private static readonly Regex Prefix = new(
        @"^(?:Screenshot_(?<day>[0-9]{8})(?=$|[-_])|WP_(?<day>[0-9]{8})(?=$|_)|(?<day>[0-9]{8})(?=$|_|\())",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant
    );

    public static string? Classify(string filename) =>
        Photos.Contains(Path.GetExtension(filename)) ? "photo"
        : Videos.Contains(Path.GetExtension(filename)) ? "video"
        : null;

    public static bool TryGetDay(string filename, out DateOnly day)
    {
        day = default;
        var match = Prefix.Match(Path.GetFileNameWithoutExtension(filename));
        return match.Success
            && DateOnly.TryParseExact(
                match.Groups["day"].Value,
                "yyyyMMdd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out day
            );
    }
}
