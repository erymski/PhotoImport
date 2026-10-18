using PhotoImport.Models;

namespace PhotoImport.Services;

public static class DayPreviewPlanner
{
    public static PreviewReport Build(ProbeReport inventory, IReadOnlyList<CaptureReportInput> captures, DayCheckpoint checkpoint, CancellationToken token)
    {
        Validate(inventory, captures, checkpoint);
        var report = new PreviewReport { Device = inventory.Device, InventoryScope = inventory.SearchedRoots,
            Checkpoint = checkpoint, TotalPhotoCount = inventory.PhotoCount,
            UnknownSizeCount = inventory.Photos.Count(p => p.SizeBytes is null or 0) };
        report.Warnings.AddRange(inventory.Warnings);
        if (token.IsCancellationRequested) { report.CompletionStatus = "cancelled"; return report; }
        if (inventory.CompletionStatus != "complete" || inventory.Errors.Count != 0)
            report.InputProblems.Add("Inventory is partial or contains errors; the preview cannot be complete.");
        foreach (var capture in captures)
            if (capture.CompletionStatus != "complete" || capture.Errors.Count != 0)
                report.InputProblems.Add("A capture report is partial or contains errors.");
        var inventoryPaths = inventory.Photos.GroupBy(p => p.SourcePath, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.Count(), StringComparer.Ordinal);
        var observations = captures.SelectMany(c => c.Files.Select(f => (c.SchemaVersion, File:f)))
            .GroupBy(x => x.File.SourcePath, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.ToArray(), StringComparer.Ordinal);
        foreach (var path in observations.Keys.Where(p => !inventoryPaths.ContainsKey(p)))
            report.InputProblems.Add($"Capture object has no inventory match: {path}");
        var boundary = new HashSet<string>(checkpoint.BoundaryKeys, StringComparer.Ordinal);
        foreach (var photo in inventory.Photos)
        {
            if (token.IsCancellationRequested) break;
            PreviewRow Unresolved(string reason) => new(photo.SourcePath, photo.SizeBytes, null, null, "unresolved", reason);
            if (inventoryPaths[photo.SourcePath] > 1) { report.Rows.Add(Unresolved("Duplicate inventory path; source association is ambiguous.")); continue; }
            if (!PhotoExtensionSupported(photo.FileName)) { report.Rows.Add(Unresolved("Capture-date source is unproven for this format.")); continue; }
            if (photo.SizeBytes is null or 0) { report.Rows.Add(Unresolved("Source size is unknown; a boundary fingerprint cannot be formed.")); continue; }
            if (!observations.TryGetValue(photo.SourcePath, out var matches)) { report.Rows.Add(Unresolved("No capture report for this inventory object.")); continue; }
            var days = new HashSet<DateOnly>(); var ids = new HashSet<string>(StringComparer.Ordinal); string? problem = null;
            foreach (var match in matches)
            {
                problem = CheckSource(photo, match.SchemaVersion, match.File);
                if (problem is not null) break;
                days.Add(match.File.Observation.CaptureDay!.Value);
                if (!string.IsNullOrWhiteSpace(match.File.Source!.PersistentObjectId)) ids.Add(match.File.Source.PersistentObjectId!);
                report.Warnings.AddRange(match.File.Observation.Warnings);
            }
            if (problem is not null || days.Count != 1 || ids.Count > 1)
            { report.Rows.Add(Unresolved(problem ?? "Conflicting capture observations or persistent IDs.")); continue; }
            var day = days.Single(); var key = BoundaryKey.Create(inventory.Device.Id, photo, day);
            var decision = day < checkpoint.CutoffDay ? "older" : day > checkpoint.CutoffDay ? "eligible"
                : checkpoint.BoundaryMode == "completedDay" || boundary.Contains(key) ? "alreadyImported" : "eligible";
            var reason = decision switch { "older" => "Capture day precedes cutoff.", "alreadyImported" => "Completed cutoff day or recorded boundary key.", _ => "Newer capture day or new tracked boundary-day key." };
            report.Rows.Add(new(photo.SourcePath, photo.SizeBytes, day, key, decision, reason));
        }
        report.CompletionStatus = token.IsCancellationRequested ? "cancelled"
            : report.UnresolvedCount != 0 || report.InputProblems.Count != 0 ? "incomplete" : "complete";
        return report;
    }

    private static bool PhotoExtensionSupported(string name) => new[] {".jpg",".jpeg",".heic",".heif",".dng"}.Contains(Path.GetExtension(name),StringComparer.OrdinalIgnoreCase);

    private static string? CheckSource(PhotoMetadata photo, int? version, JpegExifFileResult file)
    {
        if (version != 2) return "Legacy capture report lacks verified schema-2 source provenance.";
        var source = file.Source;
        if (source is null || source.SizeBytes is null or 0) return "Capture source provenance/size is missing.";
        if (source.CanonicalPath != photo.SourcePath || file.SourcePath != source.CanonicalPath || source.SizeBytes != photo.SizeBytes)
            return "Capture source path or size changed relative to inventory.";
        if (!string.IsNullOrWhiteSpace(photo.PersistentObjectId) && !string.IsNullOrWhiteSpace(source.PersistentObjectId)
            && photo.PersistentObjectId != source.PersistentObjectId) return "Persistent object identifiers disagree.";
        var observation = file.Observation;
        if (observation.Outcome is not ("available" or "invalid" or "ambiguous")) return "Capture observation is incomplete or unsupported.";
        if (observation.CaptureDayStatus != "available" || observation.CaptureDay is null) return "Capture day is missing, invalid, or ambiguous.";
        var evidence = CaptureDayResolver.FromOriginalDates(observation.CaptureDirectories.Select(d => d.DateTimeOriginal));
        if (evidence.Status != "available" || evidence.Day != observation.CaptureDay) return "Capture day disagrees with associated raw EXIF evidence.";
        return null;
    }

    private static void Validate(ProbeReport inventory, IReadOnlyList<CaptureReportInput> captures, DayCheckpoint checkpoint)
    {
        if (string.IsNullOrWhiteSpace(inventory.Device.Id) || checkpoint.SchemaVersion != 1 || checkpoint.DeviceId != inventory.Device.Id)
            throw new PreviewInputException("Checkpoint schema/device must match the inventory device.");
        if (checkpoint.BoundaryMode is not ("completedDay" or "tracked")
            || checkpoint.BoundaryMode == "completedDay" && checkpoint.BoundaryKeys.Count != 0
            || checkpoint.BoundaryKeys.Any(k => k.Length != 64 || k.Any(c => c is not (>= '0' and <= '9') and not (>= 'A' and <= 'F'))))
            throw new PreviewInputException("Invalid boundary mode or key list.");
        foreach (var capture in captures)
        {
            if (capture.SchemaVersion is not (null or 2)) throw new PreviewInputException("Unsupported capture report schema.");
            if (capture.Device.Id != inventory.Device.Id) throw new PreviewInputException("Capture report device differs from inventory.");
        }
    }
}
