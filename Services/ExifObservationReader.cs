using System.Globalization;
using MetadataExtractor;
using MetadataExtractor.Formats.Exif;
using PhotoImport.Models;

namespace PhotoImport.Services;

internal static class ExifObservationReader
{
    public static JpegExifObservation FromDirectories(IReadOnlyList<MetadataExtractor.Directory> directories,
        int bytesRead, string source, IReadOnlyList<string>? scopeWarnings = null)
    {
        var day = CaptureDayResolver.Resolve(directories);
        var fields = new Dictionary<string, string?>();
        var warnings = (scopeWarnings ?? []).Concat(day.Warnings).ToList();
        var captures = directories.OfType<ExifSubIfdDirectory>()
            .Select(d => new ExifCaptureDirectory(d.GetString(0x9003), d.GetString(0x9291), d.GetString(0x9011)))
            .Where(d => d.DateTimeOriginal is not null || d.SubSecTimeOriginal is not null || d.OffsetTimeOriginal is not null).ToArray();
        JpegExifObservation Result(string outcome, string? error = null, string? clock = null, string? timestamp = null) =>
            new(bytesRead, outcome, fields, clock, timestamp, warnings, error) {
                CaptureDirectories = captures, CaptureDay = day.Day, CaptureDayStatus = day.Status, Source = source };
        var ambiguous = captures.Distinct().Count() > 1;
        if (ambiguous) warnings.Add("EXIF capture fields differ by directory; dates, offsets and subseconds cannot be combined across directories.");
        foreach (var (name, tag, ifd0) in new (string, int, bool)[] {
            ("dateTime",0x0132,true), ("offsetTime",0x9010,false), ("dateTimeOriginal",0x9003,false),
            ("dateTimeDigitized",0x9004,false), ("offsetTimeOriginal",0x9011,false), ("offsetTimeDigitized",0x9012,false),
            ("subSecTimeOriginal",0x9291,false), ("subSecTimeDigitized",0x9292,false) })
        {
            var values = directories.Where(d => ifd0 ? d is ExifIfd0Directory : d is ExifSubIfdDirectory)
                .Select(d => d.GetString(tag)).Where(v => v is not null).Distinct(StringComparer.Ordinal).ToArray();
            fields[name] = values.Length == 1 ? values[0] : null;
            if (values.Length > 1) { ambiguous = true; warnings.Add($"Conflicting {name} values: {string.Join("; ", values)}"); }
        }
        warnings.AddRange(directories.SelectMany(d => d.Errors.Select(e => $"{d.Name}: {e}")));
        if (ambiguous) return Result("ambiguous", "Conflicting EXIF exact-time fields; see independent capture-day result.");
        if (directories.Any(d => d.HasError)) return Result("invalid", "EXIF parser reported errors; see independent capture-day result.");
        var capture = captures.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(capture?.DateTimeOriginal)) return Result("missing", "EXIF DateTimeOriginal is absent; no DateTime or filename fallback.");
        if (!DateTime.TryParseExact(capture.DateTimeOriginal, "yyyy:MM:dd HH:mm:ss", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            return Result("invalid", "Invalid EXIF DateTimeOriginal exact time.");
        if (capture.SubSecTimeOriginal is { } sub)
        {
            if (sub.Length is < 1 or > 7 || sub.Any(c => c is < '0' or > '9')) return Result("invalid", "Invalid or unsupported EXIF SubSecTimeOriginal precision.");
            date = date.AddTicks(long.Parse(sub.PadRight(7, '0'), CultureInfo.InvariantCulture));
        }
        var offset = capture.OffsetTimeOriginal;
        if (offset is not null && !ValidOffset(offset)) return Result("invalid", "Invalid EXIF OffsetTimeOriginal.");
        var clock = date.ToString("yyyy-MM-dd'T'HH:mm:ss.FFFFFFF", CultureInfo.InvariantCulture);
        if (offset is null) warnings.Add("Capture offset is unknown; no UTC instant or PC timezone was inferred.");
        return Result("available", clock: clock, timestamp: offset is null ? null : clock + offset);
    }

    private static bool ValidOffset(string value)
    {
        if (value.Length != 6 || value[0] is not ('+' or '-') || value[3] != ':'
            || new[] { value[1],value[2],value[4],value[5] }.Any(c => c is < '0' or > '9')) return false;
        var hours = (value[1]-'0')*10 + value[2]-'0'; var minutes = (value[4]-'0')*10 + value[5]-'0';
        return hours <= 14 && minutes < 60 && (hours != 14 || minutes == 0);
    }
}
