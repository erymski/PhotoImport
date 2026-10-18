using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Services;

namespace PhotoImport.Cli;

public sealed class JpegExifCli(IPhotoDeviceProvider provider, TextWriter output, TextWriter error)
{
    public int Run(string[] args, CancellationToken token)
    {
        string? selector = null,
            path = null;
        var photos = new List<string>();
        if (args.Length == 0 || args[0] != "exif")
            return Usage(
                "Use exif --device <id-or-unique-name> --photo <absolute-device-path.jpg> [--photo ...] --output <new.json>."
            );
        for (var i = 1; i < args.Length; i++)
        {
            var option = args[i];
            if (
                option is not ("--device" or "--photo" or "--output")
                || i + 1 == args.Length
                || args[i + 1].StartsWith("--", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(args[i + 1])
            )
                return Usage($"Invalid or incomplete option: {option}");
            var value = args[++i];
            switch (option)
            {
                case "--device":
                    if (selector is not null)
                        return Usage("Duplicate --device.");
                    selector = value;
                    break;
                case "--output":
                    if (path is not null)
                        return Usage("Duplicate --output.");
                    path = value;
                    break;
                case "--photo":
                    photos.Add(value);
                    break;
            }
        }
        if (
            selector is null
            || path is null
            || photos.Count is < 1 or > 3
            || photos.Any(p => !PhotoSourcePath.IsValid(p))
            || photos.Distinct(StringComparer.OrdinalIgnoreCase).Count() != photos.Count
        )
            return Usage(
                "Require --device, --output, and 1–3 distinct absolute device JPEG/HEIC/DNG paths. No recursive scanning is performed."
            );
        try
        {
            // Reserve the output before any USB access; CreateNew also closes the race.
            using var destination = new FileStream(
                Path.GetFullPath(path),
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None
            );
            var report = new JpegExifReport { RequestedPaths = photos };
            var timer = Stopwatch.StartNew();
            foreach (var warning in report.Warnings)
                output.WriteLine(warning);
            var exit = Inspect(report, selector, token);
            if (token.IsCancellationRequested)
                exit = 130;
            report.CompletionStatus = exit switch
            {
                0 => "complete",
                130 => "cancelled",
                3 => "deviceError",
                _ => "incomplete",
            };
            report.ElapsedSeconds = timer.Elapsed.TotalSeconds;
            try
            {
                JsonSerializer.Serialize(
                    destination,
                    report,
                    new JsonSerializerOptions
                    {
                        WriteIndented = true,
                        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    }
                );
                destination.Flush(flushToDisk: true);
            }
            catch (Exception ex) when (IsOutputError(ex))
            {
                error.WriteLine($"EXIF report output failed: {ex.Message}");
                return exit == 130 ? 130 : 5;
            }
            output.WriteLine(
                $"EXIF status: {report.CompletionStatus}; {report.Files.Count}/{photos.Count} selected photos; total {report.ElapsedSeconds.ToString("F3", CultureInfo.InvariantCulture)} s. Report: {path}"
            );
            return exit;
        }
        catch (Exception ex) when (IsOutputError(ex))
        {
            error.WriteLine($"EXIF report output failed: {ex.Message}");
            return 5;
        }
    }

    private int Inspect(JpegExifReport report, string selector, CancellationToken token)
    {
        IPhotoDevice? session = null;
        try
        {
            token.ThrowIfCancellationRequested();
            var devices = provider.Discover(token);
            var matches = devices
                .Where(d => string.Equals(d.Id, selector, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
                matches = devices
                    .Where(d =>
                        string.Equals(d.FriendlyName, selector, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToArray();
            if (matches.Length > 1)
                return Usage(
                    "Ambiguous device name; choose an exact ID: "
                        + string.Join(", ", matches.Select(d => d.Id))
                );
            if (matches.Length == 0)
            {
                report.Errors.Add("Selected phone is not connected.");
                error.WriteLine(report.Errors[^1]);
                return 3;
            }
            report.Device = matches[0];
            session = provider.Open(matches[0], token);
            if (session is not IPhotoContentDevice content)
            {
                report.Errors.Add(
                    "Backend does not support the provenance-bearing photo read capability."
                );
                error.WriteLine(report.Errors[^1]);
                return 3;
            }
            var exit = 0;
            foreach (var photo in report.RequestedPaths)
            {
                token.ThrowIfCancellationRequested();
                output.WriteLine($"Reading selected photo prefix: {photo}");
                var timer = Stopwatch.StartNew();
                PhotoReadHandle? handle = null;
                PhotoSourceInfo? sourceInfo = null;
                JpegExifObservation observation;
                try
                {
                    handle = content.OpenPhotoRead(photo, token);
                    sourceInfo = handle.Source;
                    if (
                        !string.Equals(
                            photo,
                            sourceInfo.CanonicalPath,
                            StringComparison.OrdinalIgnoreCase
                        )
                    )
                        throw new IOException(
                            "Selected path resolved to a different canonical source."
                        );
                    if (sourceInfo.SizeBytes is null or 0)
                        report.Warnings.Add(
                            $"Source size is unknown for {photo}; offline planning will be unresolved."
                        );
                    observation = MediaExifReader.Read(
                        handle.Stream,
                        Path.GetExtension(sourceInfo.CanonicalPath),
                        token
                    );
                }
                catch (OperationCanceledException)
                {
                    observation = Failed("cancelled", "Cancelled before content read.");
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    observation = Failed("incomplete", ex.Message);
                }
                finally
                {
                    try
                    {
                        handle?.Dispose();
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        report.Errors.Add($"Stream cleanup warning for {photo}: {ex.Message}");
                    }
                }
                report.Files.Add(
                    new(sourceInfo?.CanonicalPath ?? photo, timer.Elapsed.TotalSeconds, observation)
                    {
                        Source = sourceInfo,
                    }
                );
                var raw = observation.RawFields;
                output.WriteLine(
                    $"  day={observation.CaptureDay?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? "<unresolved>"}; {observation.Outcome}; bytes {observation.BytesRead}; DateTime={raw.GetValueOrDefault("dateTime") ?? "<missing>"}; DateTimeOriginal={raw.GetValueOrDefault("dateTimeOriginal") ?? "<missing>"}; capture={observation.CaptureTimestamp ?? observation.CaptureClock ?? "<missing>"}"
                );
                if (observation.Outcome == "cancelled" || token.IsCancellationRequested)
                    return 130;
                if (observation.CaptureDayStatus != "available")
                    exit = 4;
                if (observation.Outcome == "incomplete")
                    break; // Avoid more USB calls after transport/truncation failures.
            }
            return report.Errors.Count > 0 ? 4 : exit;
        }
        catch (OperationCanceledException)
        {
            report.Errors.Add("Cancelled.");
            return 130;
        }
        catch (Exception ex)
            when (ex is IOException or UnauthorizedAccessException or TypeInitializationException)
        {
            report.Errors.Add($"Device operation failed: {ex.Message}");
            error.WriteLine(report.Errors[^1]);
            return 3;
        }
        finally
        {
            try
            {
                session?.Dispose();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                report.Warnings.Add($"Device cleanup warning: {ex.Message}");
                error.WriteLine(report.Warnings[^1]);
            }
        }
    }

    private static JpegExifObservation Failed(string outcome, string message) =>
        new(0, outcome, new Dictionary<string, string?>(), null, null, [], message);

    private int Usage(string message)
    {
        error.WriteLine(message);
        return 2;
    }

    private static bool IsOutputError(Exception ex) =>
        ex
            is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or System.Security.SecurityException;
}
