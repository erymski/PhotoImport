using System.Globalization;
using PhotoImport.Devices;
using PhotoImport.Services;

namespace PhotoImport.Importing;

public sealed class ImportCli(
    TextWriter output,
    TextWriter error,
    ImportStateStore store,
    MediaTransfer transfer
)
{
    public int Run(
        string[] args,
        CancellationToken token,
        Func<IPhotoDeviceProvider> providerFactory
    )
    {
        IPhotoDeviceProvider? provider = null;
        try
        {
            var command = args.FirstOrDefault() ?? "sync";
            if (command is "--help" or "-h")
            {
                WriteHelp(output);
                return 0;
            }
            if (command is not ("init" or "sync" or "status"))
                throw new ImportException("Unknown importer command.");
            var options = new Dictionary<string, string>(StringComparer.Ordinal);
            var roots = new List<string>();
            var dry = false;
            for (var i = 1; i < args.Length; i++)
            {
                var option = args[i];
                if (command == "sync" && option == "--dry-run")
                {
                    if (dry)
                        throw new ImportException("Duplicate --dry-run.");
                    dry = true;
                    continue;
                }
                var allowed =
                    command == "init"
                        ? new[] { "--device", "--alias", "--destination", "--cutoff-day", "--root" }
                    : command == "sync" ? ["--device"]
                    : Array.Empty<string>();
                if (
                    !allowed.Contains(option)
                    || i + 1 >= args.Length
                    || args[i + 1].StartsWith("--", StringComparison.Ordinal)
                    || string.IsNullOrWhiteSpace(args[i + 1])
                )
                    throw new ImportException("Invalid/incomplete option: " + option);
                var value = args[++i];
                if (option == "--root")
                    roots.Add(value);
                else if (!options.TryAdd(option, value))
                    throw new ImportException("Duplicate option: " + option);
            }
            if (command == "status")
                return Status();
            if (command == "init")
            {
                foreach (
                    var key in new[] { "--device", "--alias", "--destination", "--cutoff-day" }
                )
                    if (!options.ContainsKey(key))
                        throw new ImportException("init requires " + key);
                ScopedMediaInventory.ValidateRoots(roots);
                if (!Path.IsPathFullyQualified(options["--destination"]))
                    throw new ImportException(
                        "Destination must be an absolute existing directory."
                    );
                var destination = Path.GetFullPath(options["--destination"]);
                if (!Directory.Exists(destination))
                    throw new IOException("Destination directory must already exist.");
                if (
                    !DateOnly.TryParseExact(
                        options["--cutoff-day"],
                        "yyyy-MM-dd",
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.None,
                        out var cutoff
                    )
                )
                    throw new ImportException("Invalid cutoff day; use YYYY-MM-DD.");
                token.ThrowIfCancellationRequested();
                using var held = store.AcquireLock();
                if (store.HasPending)
                    throw new ImportException(
                        "Finish pending imports before changing registrations."
                    );
                var config =
                    store.ReadConfiguration() ?? new ImportConfiguration(1, "filenameImporter", []);
                if (
                    config.Devices.Any(r =>
                        r.Alias.Equals(options["--alias"], StringComparison.OrdinalIgnoreCase)
                    )
                )
                    throw new ImportException(
                        "Alias is already registered; checkpoint was not reset."
                    );
                provider = providerFactory();
                var devices = provider.Discover(token);
                var matches = devices.Where(d => d.Id == options["--device"]).ToArray();
                if (matches.Length == 0)
                    matches = devices
                        .Where(d =>
                            string.Equals(
                                d.FriendlyName,
                                options["--device"],
                                StringComparison.OrdinalIgnoreCase
                            )
                        )
                        .ToArray();
                if (matches.Length != 1)
                    throw new ImportException(
                        "Select exactly one connected phone by ID or unique friendly name.",
                        matches.Length == 0 ? 3 : 2
                    );
                var identity = ImportIdentity.Enroll(matches[0]);
                IReadOnlyList<string> canonicalRoots;
                using (var device = provider.Open(matches[0], token))
                    canonicalRoots = DeviceFolderScope
                        .Resolve(device, roots, token)
                        .Select(r => r.Path)
                        .ToArray();
                var registration = new ImportRegistration(
                    Guid.NewGuid(),
                    options["--alias"],
                    destination,
                    canonicalRoots,
                    identity,
                    "androidFilenameDayV1",
                    new(cutoff, "completedDay", [])
                );
                var updated = config with { Devices = [.. config.Devices, registration] };
                ImportStateStore.Validate(updated);
                var probe = Path.Combine(
                    destination,
                    ".phone-photo-sync-write-check-" + Guid.NewGuid().ToString("N")
                );
                using (
                    var stream = new FileStream(
                        probe,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        1,
                        FileOptions.DeleteOnClose
                    )
                )
                    stream.Flush(true);
                token.ThrowIfCancellationRequested();
                store.WriteConfiguration(updated);
                output.WriteLine(
                    $"Registered {registration.Alias}; destination: {destination}; completed through {cutoff:yyyy-MM-dd}. Run phone-photo-sync to import."
                );
                if (identity.Mode == "deviceId")
                    output.WriteLine(
                        "Identity uses an exact device ID; if it changes after reconnect, automatic matching will stop."
                    );
                return 0;
            }
            if (!File.Exists(store.ConfigPath))
            {
                WriteFirstUse(output);
                return 2;
            }
            token.ThrowIfCancellationRequested();
            provider = providerFactory();
            var runner = new ImportRunner(store, transfer);
            var alias = options.GetValueOrDefault("--device");
            if (dry)
            {
                var preview = runner.DryRun(provider, alias, token);
                output.WriteLine(
                    $"Dry run: {preview.Plan.Candidates.Count} eligible files, {preview.Plan.TotalBytes} bytes; {preview.Plan.SkippedCount} skipped filename/format entries; {preview.Plan.IgnoredNonmediaCount} nonmedia files ignored."
                );
                for (var i = 0; i < preview.Plan.Candidates.Count; i++)
                    output.WriteLine(
                        $"Would copy {preview.Plan.Candidates[i].Source.SourcePath} -> {preview.ProposedNames[i]}"
                    );
                PrintRows(preview.Plan.Rows);
                foreach (var warning in preview.Warnings)
                    output.WriteLine("Warning: " + warning);
                if (preview.PendingReuseCandidates > 0)
                    output.WriteLine(
                        $"Pending reuse candidates: {preview.PendingReuseCandidates}; local hashes will be checked only during real sync."
                    );
                if (
                    preview.AvailableBytes < MediaTransfer.ReserveBytes
                    || preview.AvailableBytes - MediaTransfer.ReserveBytes < preview.Plan.TotalBytes
                )
                    output.WriteLine(
                        "Warning: selected bytes plus 2 GiB reserve exceed current free space."
                    );
                foreach (var problem in preview.Plan.Errors)
                    error.WriteLine(problem);
                return preview.Plan.Complete ? 0 : 4;
            }
            var result = runner.Run(provider, alias, token);
            output.WriteLine(
                $"Copied {result.Copied}; verified reused {result.Reused}; transferred {result.Bytes} bytes. Photos selected: {result.Rows.Count(r => r.Decision == "eligible" && FilenameDayPolicy.Classify(r.Source.FileName) == "photo")}; videos selected: {result.Rows.Count(r => r.Decision == "eligible" && FilenameDayPolicy.Classify(r.Source.FileName) == "video")}."
            );
            PrintRows(result.Rows);
            output.WriteLine($"{result.IgnoredNonmediaCount} nonmedia files ignored.");
            output.WriteLine(
                result.CheckpointCommitted
                    ? $"Checkpoint committed: {result.NewCheckpoint!.Day:yyyy-MM-dd} (tracked boundary)."
                    : "Checkpoint unchanged."
            );
            foreach (var warning in result.Warnings)
                output.WriteLine("Warning: " + warning);
            foreach (var problem in result.Errors)
                error.WriteLine(problem);
            if (result.ExitCode != 0)
                error.WriteLine(
                    $"Recovery state: {store.DirectoryPath}. Reconnect/unlock and rerun for interrupted transfers; inspect the pending journal for source/configuration conflicts."
                );
            return result.ExitCode;
        }
        catch (Exception ex) when (ImportRunner.IsExpected(ex))
        {
            error.WriteLine(ex.Message);
            return ex is ImportException ie ? ie.ExitCode
                : ex is OperationCanceledException ? 130
                : ex is IOException or UnauthorizedAccessException ? 5
                : 2;
        }
        finally
        {
            try
            {
                (provider as IDisposable)?.Dispose();
            }
            catch (IOException ex)
            {
                error.WriteLine("Device manager cleanup warning: " + ex.Message);
            }
        }
    }

    private int Status()
    {
        if (!File.Exists(store.ConfigPath))
        {
            WriteFirstUse(output);
            return 0;
        }
        using var held = store.AcquireReadLock();
        var config = store.ReadConfiguration()!;
        foreach (var r in config.Devices)
        {
            var pending = store.ReadPending(r.Id);
            output.WriteLine(
                $"{r.Alias}: {r.Destination}; checkpoint {r.Checkpoint.Day:yyyy-MM-dd} ({r.Checkpoint.Mode}, {r.Checkpoint.BoundaryKeys.Count} boundary files); identity {r.Identity.Mode}; pending {(pending is null ? "none" : pending.Stage + " / " + pending.Files.Count + " files")}"
            );
        }
        return 0;
    }

    private void PrintRows(IReadOnlyList<ImportPlanRow> rows)
    {
        foreach (var row in rows.Where(r => r.Decision is "skipped" or "error"))
            output.WriteLine($"{row.Decision}: {row.Source.SourcePath}: {row.Reason}");
    }

    private static void WriteFirstUse(TextWriter writer) =>
        writer.WriteLine(
            "Not configured. Run phone-photo-sync init --device <id-or-unique-name> --alias <name> --destination <absolute-existing-directory> --cutoff-day YYYY-MM-DD --root <absolute-camera-folder> [--root ...]"
        );

    public static void WriteHelp(TextWriter writer)
    {
        WriteFirstUse(writer);
        writer.WriteLine("phone-photo-sync [sync [--device <registered-alias>] [--dry-run]]");
        writer.WriteLine("phone-photo-sync status");
        writer.WriteLine(
            "Filename-day importing: camera photos/videos/screenshots only. Unknown names are skipped/reported. Never modifies the phone; existing destination files are preserved."
        );
        writer.WriteLine(
            "Diagnostic commands: list-devices; probe --device <id-or-name> [--output <new.json>]; exif --device <id-or-name> --photo <path> [--photo ...] --output <new.json>"
        );
        writer.WriteLine(
            "plan --inventory <json> --capture-report <json> (--cutoff-day YYYY-MM-DD | --checkpoint <json>) [--output <new.json>]"
        );
        writer.WriteLine(
            "preview --device <id-or-name> --root <folder> [--root ...] --max-photos <limit> (--cutoff-day YYYY-MM-DD | --checkpoint <json>) --output-dir <new-directory>"
        );
        writer.WriteLine(
            "probe is metadata only; exif/preview retain explicit bounded content-read limits (256 KiB per photo). plan is offline. See README for diagnostic contracts."
        );
    }
}
