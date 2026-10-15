using System.Globalization;
using System.Text.Json;
using PhotoImport.Models;

namespace PhotoImport.Reporting;

public static class ProbeReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public static void WriteDevice(DeviceInfo device, TextWriter output)
    {
        output.WriteLine($"Device: {device.FriendlyName ?? "<missing>"} | Manufacturer: {device.Manufacturer ?? "<missing>"} | Model: {device.Model ?? "<missing>"} | Transport: {device.Transport ?? "<unknown>"}");
        output.WriteLine($"  Selection ID: {device.Id} | Identity stability: {device.IdentityStatus}");
        foreach (var id in device.Identifiers) output.WriteLine($"  {id.Key}: {id.Value ?? "<missing>"}");
        foreach (var warning in device.Warnings) output.WriteLine($"  Warning: {warning}");
    }

    public static void WriteConsole(ProbeReport report, TextWriter output)
    {
        WriteDevice(report.Device, output);
        output.WriteLine($"Status: {report.CompletionStatus} | Files: {report.PhotoCount} | Capture date candidates: {report.WithCaptureDateCount} | Missing: {report.WithoutCaptureDateCount}");
        output.WriteLine($"Authored: {report.WithAuthoredDateCount} | PTP: {report.WithPtpDateCount} | Elapsed: {report.EnumerationDuration.TotalSeconds.ToString("F3", CultureInfo.InvariantCulture)} s");
        output.WriteLine("Capture candidates and identities require real-phone verification. Projected timestamps do not prove source timezone.");
        output.WriteLine($"Roots searched: {(report.SearchedRoots.Count == 0 ? "<none>" : string.Join(", ", report.SearchedRoots))}");
        output.WriteLine("File | Bytes | dateAuthored | ptpCaptureDate | dateCreated | dateModified");
        foreach (var photo in report.Photos)
        {
            output.WriteLine($"{photo.FileName} | {photo.SizeBytes?.ToString(CultureInfo.InvariantCulture) ?? "<missing>"} | {Display(photo.DateAuthored)} | {Display(photo.PtpCaptureDate)} | {Display(photo.DateCreated)} | {Display(photo.DateModified)}");
            output.WriteLine($"  Path: {photo.SourcePath} | Object ID (session): {photo.ObjectId} | Persistent ID (unverified): {photo.PersistentObjectId ?? "<missing>"}");
            output.WriteLine($"  Content type: {photo.ContentType ?? "<missing>"} | Format: {photo.Format ?? "<missing>"}");
            foreach (var date in new[] { photo.DateAuthored, photo.PtpCaptureDate, photo.DateCreated, photo.DateModified })
                output.WriteLine($"  {date.Source}: {date.Availability}; offset {date.OffsetPresence}; raw {date.RawValue ?? "<missing>"}; type {date.RawType ?? "<missing>"}{(date.Error is null ? "" : "; error " + date.Error)}");
            foreach (var warning in photo.Warnings) output.WriteLine($"  Warning: {warning}");
        }
        foreach (var warning in report.Warnings) output.WriteLine($"Warning: {warning}");
        foreach (var error in report.Errors) output.WriteLine($"Error [{error.Operation}] {error.Path ?? error.ObjectId ?? "device"}: {error.Message}");
    }

    private static string Display(TimestampObservation date) => date.DisplayValue ?? $"<{date.Availability}>";

    public static void WriteJson(ProbeReport report, string path)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None);
        JsonSerializer.Serialize(stream, report, JsonOptions);
        stream.Flush(flushToDisk: true);
    }
}
