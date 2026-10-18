using System.Globalization;
using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Reporting;
using PhotoImport.Services;

namespace PhotoImport.Cli;

public sealed class LivePreviewCli(
    TextWriter output,
    TextWriter error,
    Func<string, PreviewBundle>? reserve = null
)
{
    public int Run(
        string[] args,
        CancellationToken token,
        Func<IPhotoDeviceProvider> providerFactory
    )
    {
        PreviewBundle? bundle = null;
        var exit = 2;
        try
        {
            token.ThrowIfCancellationRequested();
            var options = Parse(args);
            var checkpoint = options.CheckpointPath is null
                ? null
                : PreviewInputReader.ReadCheckpoint(options.CheckpointPath, token);
            if (checkpoint is not null)
                ValidateCheckpoint(checkpoint);
            token.ThrowIfCancellationRequested();
            bundle = (reserve ?? PreviewBundle.Reserve)(options.OutputDirectory);
            IPhotoDeviceProvider? provider = null;
            try
            {
                provider = providerFactory();
            }
            catch (Exception ex)
                when (ex
                        is IOException
                            or UnauthorizedAccessException
                            or TypeInitializationException
                            or System.Runtime.InteropServices.COMException
                )
            {
                var manifest = new LivePreviewManifest
                {
                    RequestedRoots = options.Roots,
                    MaxPhotos = options.MaxPhotos,
                };
                manifest.Errors.Add("Device initialization failed: " + ex.Message);
                manifest.CompletionStatus = token.IsCancellationRequested
                    ? "cancelled"
                    : "incomplete";
                WriteError(manifest.Errors[0]);
                bundle.WriteManifest(manifest);
                exit = 3;
            }
            if (provider is not null)
                exit = LivePreviewRunner.Run(
                    provider,
                    options,
                    checkpoint,
                    bundle,
                    output,
                    error,
                    token
                );
        }
        catch (OperationCanceledException)
        {
            exit = 130;
        }
        catch (PreviewInputException ex)
        {
            WriteError(ex.Message);
            exit = 2;
        }
        catch (Exception ex) when (IsIoError(ex))
        {
            WriteError("Local preview I/O failed: " + ex.Message);
            exit = 5;
        }
        finally
        {
            try
            {
                bundle?.Dispose();
            }
            catch (Exception ex) when (IsIoError(ex))
            {
                WriteError("Report cleanup failed: " + ex.Message);
                exit = 5;
            }
        }
        return token.IsCancellationRequested ? 130 : exit;
    }

    private static LivePreviewOptions Parse(string[] args)
    {
        if (args.Length == 0 || args[0] != "preview")
            throw new PreviewInputException(
                "Use preview with explicit device, root(s), max-photos, boundary and output-dir."
            );
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        var roots = new List<string>();
        for (var i = 1; i < args.Length; i++)
        {
            var option = args[i];
            if (
                option
                    is not (
                        "--device"
                        or "--root"
                        or "--max-photos"
                        or "--cutoff-day"
                        or "--checkpoint"
                        or "--output-dir"
                    )
                || i + 1 == args.Length
                || args[i + 1].StartsWith("--", StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(args[i + 1])
            )
                throw new PreviewInputException("Invalid or incomplete preview option: " + option);
            var value = args[++i];
            if (option == "--root")
                roots.Add(value);
            else if (!values.TryAdd(option, value))
                throw new PreviewInputException("Duplicate option: " + option);
        }
        if (
            !values.TryGetValue("--device", out var device)
            || !values.TryGetValue("--output-dir", out var directory)
            || !values.TryGetValue("--max-photos", out var limit)
            || !int.TryParse(
                limit,
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out var maxPhotos
            )
            || maxPhotos <= 0
            || values.ContainsKey("--cutoff-day") == values.ContainsKey("--checkpoint")
        )
            throw new PreviewInputException(
                "Require --device, --root, positive --max-photos, exactly one cutoff/checkpoint and --output-dir."
            );
        DeviceFolderScope.Validate(roots);
        DateOnly? day = null;
        if (values.TryGetValue("--cutoff-day", out var cutoff))
        {
            if (
                !DateOnly.TryParseExact(
                    cutoff,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed
                )
            )
                throw new PreviewInputException("Cutoff must be a valid YYYY-MM-DD day.");
            day = parsed;
        }
        return new(
            device,
            roots,
            maxPhotos,
            day,
            values.GetValueOrDefault("--checkpoint"),
            Path.GetFullPath(directory)
        );
    }

    private static void ValidateCheckpoint(DayCheckpoint checkpoint)
    {
        if (
            checkpoint.SchemaVersion != 1
            || string.IsNullOrWhiteSpace(checkpoint.DeviceId)
            || checkpoint.BoundaryMode is not ("completedDay" or "tracked")
            || checkpoint.BoundaryMode == "completedDay" && checkpoint.BoundaryKeys.Count != 0
            || checkpoint.BoundaryKeys.Any(k =>
                k.Length != 64
                || k.Any(c => c is not (>= '0' and <= '9') and not (>= 'A' and <= 'F'))
            )
        )
            throw new PreviewInputException("Invalid checkpoint identity, mode or boundary keys.");
    }

    private void WriteError(string message)
    {
        try
        {
            error.WriteLine(message);
        }
        catch (Exception ex) when (IsIoError(ex)) { }
    }

    internal static bool IsIoError(Exception ex) =>
        ex
            is IOException
                or UnauthorizedAccessException
                or ArgumentException
                or NotSupportedException
                or System.Security.SecurityException;
}
