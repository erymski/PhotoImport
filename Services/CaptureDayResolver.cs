using System.Globalization;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using MetadataExtractor.Formats.Exif.Makernotes;
using MetadataExtractor.Formats.Icc;
using PhotoImport.Models;

namespace PhotoImport.Services;

public static class CaptureDayResolver
{
    public static CaptureDayResolution Resolve(
        IReadOnlyList<MetadataExtractor.Directory> directories
    )
    {
        var warnings = directories
            .Where(d => d is AppleMakernoteDirectory or IccDirectory)
            .SelectMany(d =>
                d.Errors.Select(e =>
                    $"{(d is IccDirectory ? "ICC profile" : "Apple maker-note")}: {e}"
                )
            )
            .ToArray();
        if (directories.Any(d => d.HasError && d is not (AppleMakernoteDirectory or IccDirectory)))
            return new("invalid", null, warnings, "Capture/container metadata has parser errors.");
        return FromOriginalDates(
            directories.OfType<ExifSubIfdDirectory>().Select(d => d.GetString(0x9003))
        ) with
        {
            Warnings = warnings,
        };
    }

    internal static CaptureDayResolution FromOriginalDates(IEnumerable<string?> values)
    {
        var days = new HashSet<DateOnly>();
        foreach (var value in values.Where(v => !string.IsNullOrWhiteSpace(v)))
        {
            if (
                value!.Length < 10
                || !DateOnly.TryParseExact(
                    value[..10],
                    "yyyy:MM:dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var day
                )
            )
                return new("invalid", null, [], "Invalid EXIF DateTimeOriginal calendar date.");
            days.Add(day);
        }
        return days.Count switch
        {
            0 => new("missing", null, [], "EXIF DateTimeOriginal is absent; no fallback used."),
            1 => new("available", days.Single(), [], null),
            _ => new("ambiguous", null, [], "Conflicting EXIF capture calendar days."),
        };
    }
}
