using System.Globalization;
using System.Text.Json;
using PhotoImport.Models;

namespace PhotoImport.Reporting;

public static class PreviewReportWriter
{
    public static void WriteConsole(PreviewReport report, TextWriter output)
    {
        output.WriteLine(
            $"Offline snapshot preview: {report.CompletionStatus}; cutoff {report.Checkpoint.CutoffDay:yyyy-MM-dd} ({report.Checkpoint.BoundaryMode}). No files copied or state changed."
        );
        output.WriteLine($"Scope: {string.Join(", ", report.InventoryScope)}");
        output.WriteLine(
            $"Processed {report.ProcessedCount}/{report.TotalPhotoCount}; selected {report.SelectedCount}; known bytes {report.SelectedKnownBytes.ToString(CultureInfo.InvariantCulture)}; unknown sizes {report.UnknownSizeCount}; unresolved {report.UnresolvedCount}."
        );
        foreach (var row in report.Rows)
            output.WriteLine(
                $"{row.Decision}: {row.SourcePath} | {row.CaptureDay?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "<unknown day>"} | {row.Reason}"
            );
        foreach (var problem in report.InputProblems)
            output.WriteLine("Input problem: " + problem);
        foreach (var warning in report.Warnings.Distinct(StringComparer.Ordinal))
            output.WriteLine("Warning: " + warning);
    }

    public static void WriteJson(PreviewReport report, string path)
    {
        using var stream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None
        );
        JsonSerializer.Serialize(
            stream,
            report,
            new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            }
        );
        stream.Flush(flushToDisk: true);
    }
}
