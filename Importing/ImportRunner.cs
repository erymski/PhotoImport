using System.Text.Json;
using PhotoImport.Devices;
using PhotoImport.Models;

namespace PhotoImport.Importing;

public record ImportDryRunResult(
    ImportRegistration Registration,
    DeviceInfo Device,
    ImportPlan Plan,
    IReadOnlyList<string> ProposedNames,
    long AvailableBytes,
    IReadOnlyList<string> Warnings,
    int PendingReuseCandidates
);

public sealed class ImportRunner(
    ImportStateStore store,
    MediaTransfer transfer,
    Func<DateTimeOffset>? utcNow = null
)
{
    internal static bool Same<T>(T left, T right) =>
        JsonSerializer.Serialize(left, ImportStateStore.Json)
        == JsonSerializer.Serialize(right, ImportStateStore.Json);

    public ImportDryRunResult DryRun(
        IPhotoDeviceProvider provider,
        string? alias,
        CancellationToken token
    )
    {
        using var held = store.AcquireReadLock();
        var config = store.ReadConfiguration() ?? throw new ImportException("Run init first.");
        var (registration, info) = ImportIdentity.Select(
            config.Devices,
            provider.Discover(token),
            alias
        );
        using var device = provider.Open(info, token);
        var inventory = ScopedMediaInventory.Read(
            device,
            registration.Roots,
            token,
            registration.DatePolicy
        );
        var plan = FilenameImportPlanner.Build(registration, inventory);
        var pending = store.ReadPending(registration.Id);
        if (pending is not null)
            ValidateSession(registration, pending);
        var destination = new ImportDestination(registration.Destination);
        if (!Directory.Exists(destination.Root))
            throw new IOException("Configured destination is missing.");
        var reserved = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var names = plan.Candidates.Select(c => PlanName(destination, c, reserved)).ToArray();
        return new(
            registration,
            info,
            plan,
            names,
            transfer.AvailableBytes(destination.Root),
            inventory.Warnings,
            pending?.Files.Count(f => f.Stage != "planned") ?? 0
        );
    }

    public ImportRunResult Run(
        IPhotoDeviceProvider provider,
        string? alias,
        CancellationToken token
    )
    {
        try
        {
            using var held = store.AcquireLock();
            return RunLocked(provider, alias, token);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            return Failure(new(), ex);
        }
    }

    private ImportRunResult RunLocked(
        IPhotoDeviceProvider provider,
        string? alias,
        CancellationToken token
    )
    {
        var result = new ImportRunResult { Started = (utcNow ?? (() => DateTimeOffset.UtcNow))() };
        PendingImport? session = null;
        IPhotoDevice? device = null;
        ImportRegistration? registration = null;
        try
        {
            token.ThrowIfCancellationRequested();
            var config =
                store.ReadConfiguration()
                ?? throw new ImportException("No importer configuration. Run init first.");
            var selected = ImportIdentity.Select(config.Devices, provider.Discover(token), alias);
            registration = selected.Registration;
            result = result with
            {
                OldCheckpoint = registration.Checkpoint,
                NewCheckpoint = registration.Checkpoint,
            };
            session = store.ReadPending(registration.Id);
            if (session is not null)
            {
                ValidateSession(registration, session);
                if (
                    session.NewCheckpoint is not null
                    && Same(registration.Checkpoint, session.NewCheckpoint)
                )
                {
                    result = result with
                    {
                        CheckpointCommitted = true,
                        OldCheckpoint = session.Registration.Checkpoint,
                        NewCheckpoint = registration.Checkpoint,
                    };
                    return Archive(session with { Stage = "committed" }, result);
                }
                if (!Same(registration.Checkpoint, session.Registration.Checkpoint))
                    throw new ImportException(
                        "Pending journal checkpoint conflicts with configuration."
                    );
            }
            device = provider.Open(selected.Device, token);
            if (device is not IMediaContentDevice content)
                throw new ImportException("Device backend does not support full media reads.", 3);
            var inventory = ScopedMediaInventory.Read(
                device,
                registration.Roots,
                token,
                registration.DatePolicy
            );
            var plan = FilenameImportPlanner.Build(registration, inventory);
            result = result with
            {
                Rows = plan.Rows,
                Warnings = inventory.Warnings,
                IgnoredNonmediaCount = inventory.IgnoredNonmediaCount,
            };
            if (!plan.Complete)
                throw new ImportException(string.Join(Environment.NewLine, plan.Errors), 4);
            var destination = new ImportDestination(registration.Destination);
            if (!Directory.Exists(destination.Root))
                throw new IOException("Configured destination is missing.");
            if (session is null)
            {
                var id = Guid.NewGuid();
                var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var entries = new List<PendingImportFile>();
                foreach (var c in plan.Candidates)
                    entries.Add(
                        new(
                            c,
                            PlanName(destination, c, used),
                            destination.CreatePartialPath(id, c.Key)
                        )
                    );
                session = new(1, id, registration, entries);
            }
            void Persist(PendingImport updated)
            {
                store.WritePending(updated);
                session = updated;
            }
            if (session.Files.Count != 0)
                Persist(session);
            foreach (var entry in session.Files.ToArray())
            {
                token.ThrowIfCancellationRequested();
                session = transfer.TryRecover(session, entry, Persist, token);
            }
            var current = plan.Candidates.ToDictionary(c => c.Key, StringComparer.Ordinal);
            foreach (var entry in session.Files.Where(f => f.Stage != "completed"))
            {
                if (
                    !current.TryGetValue(entry.Candidate.Key, out var match)
                    || !SameSource(entry.Candidate.Source, match.Source, registration.DatePolicy)
                )
                    throw new ImportException(
                        "Unfinished source is missing or changed; inspect pending journal: "
                            + entry.Candidate.Source.SourcePath,
                        4
                    );
            }
            var keys = session.Files.Select(f => f.Candidate.Key).ToHashSet(StringComparer.Ordinal);
            // iPhone IDs are connection-local: bind unfinished entries to this verified inventory.
            // MediaTransfer still checks the fresh ID immediately before reading content.
            var planned = session
                .Files.Select(f =>
                    f.Stage == "completed" || registration.DatePolicy != IPhoneWindowsDayPolicy.Name
                        ? f
                        : f with
                        {
                            Candidate = current[f.Candidate.Key],
                        }
                )
                .ToList();
            var reserved = planned
                .Select(f => f.FinalPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var c in plan.Candidates.Where(c => !keys.Contains(c.Key)))
                planned.Add(
                    new(
                        c,
                        PlanName(destination, c, reserved),
                        destination.CreatePartialPath(session.SessionId, c.Key)
                    )
                );
            session = session with { Files = planned, Stage = "copying", NewCheckpoint = null };
            if (session.Files.Count != 0)
                Persist(session);
            var reused = session.Files.Count(f => f.Stage == "completed");
            result = result with { Reused = reused };
            var remaining = session.Files.Where(f => f.Stage != "completed").ToArray();
            if (remaining.Length > 0)
                transfer.CheckSpace(
                    destination.Root,
                    remaining.Aggregate(
                        0L,
                        (sum, f) => checked(sum + (long)f.Candidate.Source.SizeBytes!.Value)
                    )
                );
            foreach (var entry in remaining)
            {
                session = transfer.Copy(content, session, entry, Persist, token);
                result = result with
                {
                    Copied = result.Copied + 1,
                    Bytes = checked(
                        result.Bytes
                        + session.Files.Single(f => f.Candidate.Key == entry.Candidate.Key).Length
                    ),
                };
            }
            // Complete device cleanup before the irreversible checkpoint commit.
            device.Dispose();
            device = null;
            token.ThrowIfCancellationRequested();
            if (session.Files.Count == 0)
                return Archive(session, result);
            var latest = session.Files.Max(f => f.Candidate.Day);
            var boundary = session
                .Files.Where(f => f.Candidate.Day == latest)
                .Select(f => f.Candidate.Key);
            if (latest == registration.Checkpoint.Day && registration.Checkpoint.Mode == "tracked")
                boundary = boundary.Concat(registration.Checkpoint.BoundaryKeys);
            var next = new ImportCheckpoint(
                latest,
                "tracked",
                boundary.Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray()
            );
            session = session with { Stage = "readyToCommit", NewCheckpoint = next };
            Persist(session);
            token.ThrowIfCancellationRequested();
            store.WriteConfiguration(
                config with
                {
                    Devices = config
                        .Devices.Select(r =>
                            r.Id == registration.Id ? r with { Checkpoint = next } : r
                        )
                        .ToArray(),
                }
            );
            result = result with { CheckpointCommitted = true, NewCheckpoint = next };
            session = session with { Stage = "committed" };
            result = Archive(session, result);
        }
        catch (Exception ex) when (IsExpected(ex))
        {
            // File.Replace can succeed before a fault or cancellation is observed.
            if (session?.NewCheckpoint is not null && registration is not null)
            {
                try
                {
                    var actual = store
                        .ReadConfiguration()
                        ?.Devices.SingleOrDefault(r => r.Id == registration.Id)
                        ?.Checkpoint;
                    if (actual is not null && Same(actual, session.NewCheckpoint))
                        result = result with { CheckpointCommitted = true, NewCheckpoint = actual };
                }
                catch (Exception readError) when (IsExpected(readError))
                {
                    result = result with
                    {
                        Warnings =
                        [
                            .. result.Warnings,
                            "Could not re-read commit state; inspect config and pending journal: "
                                + readError.Message,
                        ],
                    };
                }
            }
            result = Failure(result, ex);
        }
        finally
        {
            if (device is not null)
            {
                try
                {
                    device.Dispose();
                }
                catch (Exception ex) when (IsExpected(ex))
                {
                    result = result with
                    {
                        ExitCode = result.ExitCode == 0 ? 3 : result.ExitCode,
                        Errors = [.. result.Errors, "Device cleanup: " + ex.Message],
                    };
                }
            }
        }
        return Finish(result);
    }

    private ImportRunResult Finish(ImportRunResult result) =>
        result with
        {
            Finished = (utcNow ?? (() => DateTimeOffset.UtcNow))(),
        };

    private ImportRunResult Archive(PendingImport session, ImportRunResult result)
    {
        result = Finish(result);
        store.Archive(session, result);
        try
        {
            ImportDirectoryLease.RemoveEmptySession(
                session.Registration.Destination,
                session.SessionId
            );
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            result = result with
            {
                Warnings =
                [
                    .. result.Warnings,
                    "Could not remove empty staging directory: " + ex.Message,
                ],
            };
        }
        return result;
    }

    private static bool SameSource(
        PhotoMetadata expected,
        PhotoMetadata actual,
        string datePolicy
    ) =>
        expected.SourcePath == actual.SourcePath
        && expected.SizeBytes == actual.SizeBytes
        && (
            datePolicy == IPhoneWindowsDayPolicy.Name
                ? expected.DateCreated.DisplayValue == actual.DateCreated.DisplayValue
                : string.IsNullOrWhiteSpace(expected.PersistentObjectId)
                    || string.IsNullOrWhiteSpace(actual.PersistentObjectId)
                    || expected.PersistentObjectId == actual.PersistentObjectId
        );

    private static void ValidateSession(ImportRegistration registration, PendingImport session)
    {
        if (
            !Same(
                registration with
                {
                    Checkpoint = session.Registration.Checkpoint,
                },
                session.Registration
            )
        )
            throw new ImportException("Registration settings changed while an import is pending.");
    }

    private static string PlanName(
        ImportDestination destination,
        ImportCandidate c,
        HashSet<string> reserved
    )
    {
        var path = destination.ChooseFinalPath(c);
        var basePath = path;
        var index = 0;
        while (!reserved.Add(path))
            path = Path.Combine(
                destination.Root,
                Path.GetFileNameWithoutExtension(basePath)
                    + "_"
                    + c.Key[..12]
                    + "_"
                    + (++index)
                    + Path.GetExtension(basePath)
            );
        return path;
    }

    internal static bool IsExpected(Exception ex) =>
        ex
            is ImportException
                or IOException
                or UnauthorizedAccessException
                or OperationCanceledException
                or FormatException
                or ArgumentException
                or OverflowException;

    private static ImportRunResult Failure(ImportRunResult result, Exception ex) =>
        result with
        {
            ExitCode =
                ex is OperationCanceledException ? (result.CheckpointCommitted ? 0 : 130)
                : ex is ImportException ie ? ie.ExitCode
                : ex is IOException or UnauthorizedAccessException ? 5
                : 2,
            Errors = [.. result.Errors, ex.Message],
        };
}
