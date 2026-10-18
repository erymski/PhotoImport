using System.Globalization;
using PhotoImport.Models;
using PhotoImport.Reporting;
using PhotoImport.Services;

namespace PhotoImport.Cli;

public sealed class DayPreviewCli(TextWriter output, TextWriter error)
{
    public int Run(string[] args, CancellationToken token)
    {
        string? inventoryPath = null,
            cutoffText = null,
            checkpointPath = null,
            outputPath = null;
        var capturePaths = new List<string>();
        if (args.Length == 0 || args[0] != "plan")
            return Usage(
                "Use plan --inventory <json> --capture-report <json> (--cutoff-day YYYY-MM-DD | --checkpoint <json>) [--output <new.json>]."
            );
        for (var i = 1; i < args.Length; i++)
        {
            var option = args[i];
            if (
                option
                    is not (
                        "--inventory"
                        or "--capture-report"
                        or "--cutoff-day"
                        or "--checkpoint"
                        or "--output"
                    )
                || i + 1 == args.Length
                || args[i + 1].StartsWith("--", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(args[i + 1])
            )
                return Usage("Invalid or incomplete option: " + option);
            var value = args[++i];
            switch (option)
            {
                case "--inventory":
                    if (inventoryPath is not null)
                        return Usage("Duplicate --inventory.");
                    inventoryPath = value;
                    break;
                case "--cutoff-day":
                    if (cutoffText is not null)
                        return Usage("Duplicate --cutoff-day.");
                    cutoffText = value;
                    break;
                case "--checkpoint":
                    if (checkpointPath is not null)
                        return Usage("Duplicate --checkpoint.");
                    checkpointPath = value;
                    break;
                case "--output":
                    if (outputPath is not null)
                        return Usage("Duplicate --output.");
                    outputPath = value;
                    break;
                default:
                    capturePaths.Add(value);
                    break;
            }
        }
        DateOnly cutoff = default;
        if (
            inventoryPath is null
            || capturePaths.Count == 0
            || (cutoffText is null) == (checkpointPath is null)
        )
            return Usage(
                "Require inventory, capture report(s), and exactly one cutoff day or checkpoint."
            );
        if (
            cutoffText is not null
            && !DateOnly.TryParseExact(
                cutoffText,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out cutoff
            )
        )
            return Usage("Cutoff must be a valid YYYY-MM-DD calendar day.");
        try
        {
            token.ThrowIfCancellationRequested();
            inventoryPath = Path.GetFullPath(inventoryPath);
            capturePaths = capturePaths.Select(Path.GetFullPath).ToList();
            if (checkpointPath is not null)
                checkpointPath = Path.GetFullPath(checkpointPath);
            if (outputPath is not null)
            {
                outputPath = Path.GetFullPath(outputPath);
                var inputs = capturePaths
                    .Append(inventoryPath)
                    .Concat(checkpointPath is null ? [] : new[] { checkpointPath });
                if (inputs.Contains(outputPath, StringComparer.OrdinalIgnoreCase))
                    return Usage("Output must not alias an input path.");
                if (File.Exists(outputPath) || Directory.Exists(outputPath))
                    throw new IOException("Output already exists; choose a new filename.");
                if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
                    throw new DirectoryNotFoundException("Output directory does not exist.");
            }
            var inventory = PreviewInputReader.ReadInventory(inventoryPath, token);
            var captures = capturePaths
                .Select(p => PreviewInputReader.ReadCapture(p, token))
                .ToArray();
            var checkpoint = checkpointPath is null
                ? new DayCheckpoint(1, inventory.Device.Id, cutoff, "completedDay", [])
                : PreviewInputReader.ReadCheckpoint(checkpointPath, token);
            var report = DayPreviewPlanner.Build(inventory, captures, checkpoint, token);
            var exit =
                token.IsCancellationRequested || report.CompletionStatus == "cancelled" ? 130
                : report.IsComplete ? 0
                : 4;
            PreviewReportWriter.WriteConsole(report, output);
            if (token.IsCancellationRequested)
            {
                exit = 130;
                report.CompletionStatus = "cancelled";
            }
            if (outputPath is not null)
            {
                try
                {
                    PreviewReportWriter.WriteJson(report, outputPath);
                }
                catch (Exception ex) when (IsIoError(ex))
                {
                    error.WriteLine("Preview output failed: " + ex.Message);
                    return token.IsCancellationRequested || exit == 130 ? 130 : 5;
                }
            }
            return token.IsCancellationRequested ? 130 : exit;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("Cancelled.");
            return 130;
        }
        catch (PreviewInputException ex)
        {
            return Usage(ex.Message);
        }
        catch (Exception ex) when (IsIoError(ex))
        {
            error.WriteLine("Local preview I/O failed: " + ex.Message);
            return token.IsCancellationRequested ? 130 : 5;
        }
    }

    private int Usage(string message)
    {
        error.WriteLine(message);
        return 2;
    }

    private static bool IsIoError(Exception ex) =>
        ex
            is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or System.Security.SecurityException;
}
