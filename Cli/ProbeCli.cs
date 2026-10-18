using MediaDevices;
using PhotoImport.Devices;
using PhotoImport.Reporting;
using PhotoImport.Services;

namespace PhotoImport.Cli;

public sealed class ProbeCli(IPhotoDeviceProvider provider, TextWriter output, TextWriter error)
{
    public int Run(string[] args, CancellationToken cancellationToken)
    {
        if (args.Length == 0 || args is ["--help"] or ["-h"])
        {
            output.WriteLine("phone-photo-sync list-devices");
            output.WriteLine(
                "phone-photo-sync probe --device <device-id-or-unique-name> [--output metadata.json]"
            );
            output.WriteLine(
                "phone-photo-sync exif --device <device-id-or-unique-name> --photo <absolute-device-path.jpg> [--photo ...] --output <new.json>"
            );
            output.WriteLine(
                "phone-photo-sync plan --inventory <metadata.json> --capture-report <capture.json> (--cutoff-day YYYY-MM-DD | --checkpoint <checkpoint.json>) [--output <new.json>]"
            );
            output.WriteLine(
                "phone-photo-sync preview --device <id-or-unique-name> --root <absolute-device-folder> [--root ...] --max-photos <positive-integer> (--cutoff-day YYYY-MM-DD | --checkpoint <json>) --output-dir <new-directory>"
            );
            output.WriteLine(
                "preview inventories explicit folders and attempts bounded capture reads: max 256 KiB per photo; driver read-ahead may transfer more. Saves reports only; unread/unknown dates stay unresolved."
            );
            output.WriteLine(
                "plan is offline: local JSON only, with no device discovery, media reads, downloads, destination lookup, or state updates."
            );
            output.WriteLine(
                "Unlock the phone. For Android, enable USB file transfer; for iPhone, install Apple drivers and confirm USB trust."
            );
            output.WriteLine(
                "probe is metadata only: no photo downloads or sync-state changes. JSON output must be a new file."
            );
            output.WriteLine(
                "exif explicitly reads media content for 1–3 selected JPEG/HEIC/DNG files: max 256 KiB per photo in the application; USB driver read-ahead may transfer more. Saves parsed metadata only."
            );
            return 0;
        }
        string? selector = null,
            outputPath = null;
        var list = args is ["list-devices"];
        if (!list)
        {
            if (args[0] != "probe")
                return Usage("Unknown command or options. Use --help.");
            for (var i = 1; i < args.Length; i++)
            {
                var option = args[i];
                if (
                    option is not ("--device" or "--output")
                    || i + 1 == args.Length
                    || args[i + 1].StartsWith("--", StringComparison.Ordinal)
                )
                    return Usage($"Invalid or incomplete option: {option}");
                var value = args[++i];
                if (string.IsNullOrWhiteSpace(value))
                    return Usage($"A value is required for {option}.");
                if (option == "--device")
                {
                    if (selector is not null)
                        return Usage("Duplicate --device option.");
                    selector = value;
                }
                else
                {
                    if (outputPath is not null)
                        return Usage("Duplicate --output option.");
                    outputPath = value;
                }
            }
            if (selector is null)
                return Usage("probe requires --device <device-id-or-unique-name>.");
        }
        if (outputPath is not null)
        {
            try
            {
                outputPath = Path.GetFullPath(outputPath);
                if (File.Exists(outputPath) || Directory.Exists(outputPath))
                    throw new IOException("Output already exists; choose a new JSON filename.");
                if (!Directory.Exists(Path.GetDirectoryName(outputPath)))
                    throw new DirectoryNotFoundException("The output directory does not exist.");
            }
            catch (Exception ex) when (IsOutputError(ex))
            {
                error.WriteLine($"Report output failed: {ex.Message}");
                return 5;
            }
        }
        IPhotoDevice? session = null;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var devices = provider.Discover(cancellationToken);
            if (list)
            {
                if (devices.Count == 0)
                    output.WriteLine(
                        "No portable devices found. Connect and unlock a phone, then enable USB file transfer/trust."
                    );
                foreach (var device in devices)
                    ProbeReportWriter.WriteDevice(device, output);
                return 0;
            }
            var matches = devices
                .Where(d => string.Equals(d.Id, selector, StringComparison.Ordinal))
                .ToList();
            if (matches.Count == 0)
                matches = devices
                    .Where(d =>
                        string.Equals(d.FriendlyName, selector, StringComparison.OrdinalIgnoreCase)
                    )
                    .ToList();
            if (matches.Count > 1)
                return Usage(
                    "Ambiguous device name; choose an exact ID: "
                        + string.Join(", ", matches.Select(d => d.Id))
                );
            if (matches.Count == 0)
            {
                error.WriteLine(
                    "Device not found. Run list-devices, unlock the phone, and enable USB file transfer/trust."
                );
                return 3;
            }
            session = provider.Open(matches[0], cancellationToken);
            var report = MetadataProbe.Run(session, matches[0], cancellationToken);
            ProbeReportWriter.WriteConsole(report, output);
            var exit = report.CompletionStatus switch
            {
                "complete" => 0,
                "cancelled" => 130,
                _ => 4,
            };
            if (outputPath is not null)
            {
                try
                {
                    ProbeReportWriter.WriteJson(report, outputPath);
                    output.WriteLine($"Report exported: {outputPath}");
                }
                catch (Exception ex) when (IsOutputError(ex))
                {
                    error.WriteLine($"Report output failed: {ex.Message}");
                    if (exit != 130)
                        return 5;
                }
            }
            return exit;
        }
        catch (OperationCanceledException)
        {
            error.WriteLine("Cancelled.");
            return 130;
        }
        catch (Exception ex)
            when (ex
                    is IOException
                        or UnauthorizedAccessException
                        or MediaDeviceException
                        or NotConnectedException
                        or System.Runtime.InteropServices.COMException
                        or TypeInitializationException
            )
        {
            error.WriteLine($"Device operation failed: {ex.Message}");
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
                error.WriteLine($"Device cleanup warning: {ex.Message}");
            }
        }
    }

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
