using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PhotoImport.Models;

namespace PhotoImport.Importing;

public static class FilenameImportPlanner
{
    public static string CreateKey(Guid registrationId, PhotoMetadata source, DateOnly day)
    {
        if (source.SizeBytes is null or 0 or > long.MaxValue) throw new ImportException("A known positive source size is required.");
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new object[]
            {registrationId, source.SourcePath, source.SizeBytes.Value, day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)})));
    }

    public static void ValidateCheckpoint(ImportCheckpoint checkpoint)
    {
        if (checkpoint is null || checkpoint.BoundaryKeys is null || checkpoint.Mode is not ("completedDay" or "tracked")
            || checkpoint.Mode == "completedDay" && checkpoint.BoundaryKeys.Count != 0
            || checkpoint.BoundaryKeys.Any(k => k is null || k.Length != 64 || k.Any(c => !char.IsAsciiHexDigitUpper(c)))
            || checkpoint.BoundaryKeys.Distinct(StringComparer.Ordinal).Count() != checkpoint.BoundaryKeys.Count)
            throw new ImportException("Invalid importer checkpoint.");
    }

    public static ImportPlan Build(ImportRegistration registration, ImportInventory inventory)
    {
        ValidateCheckpoint(registration.Checkpoint);
        if (registration.DatePolicy != "androidFilenameDayV1") throw new ImportException("Unsupported importer date policy.");
        var errors = inventory.Errors.ToList();
        if (!inventory.Complete) errors.Add("Inventory is incomplete.");
        var rows = new List<ImportPlanRow>(); var candidates = new List<ImportCandidate>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); long total = 0;
        foreach (var source in inventory.Files.OrderBy(f => f.SourcePath, StringComparer.Ordinal))
        {
            string? problem = null;
            if (!seen.Add(source.SourcePath)) problem = "Duplicate canonical source path.";
            if (source.FileName != Path.GetFileName(source.SourcePath)) problem = "Source basename does not match path.";
            if (problem is not null) { errors.Add(problem); rows.Add(new(source,null,null,"error",problem)); continue; }
            var kind = FilenameDayPolicy.Classify(source.FileName);
            if (kind is null || !FilenameDayPolicy.TryGetDay(source.FileName, out var day))
            { rows.Add(new(source,null,null,"skipped",kind is null ? "Unsupported extension." : "Unknown filename date.")); continue; }
            var checkpoint = registration.Checkpoint;
            if (day < checkpoint.Day || day == checkpoint.Day && checkpoint.Mode == "completedDay")
            { rows.Add(new(source,day,null,"older","On or before completed cutoff.")); continue; }
            if (source.SizeBytes is null or 0 or > long.MaxValue)
            { errors.Add("Unknown or invalid eligible source size: " + source.SourcePath); rows.Add(new(source,day,null,"error",errors[^1])); continue; }
            var key = CreateKey(registration.Id,source,day);
            var decision = day == checkpoint.Day && checkpoint.BoundaryKeys.Contains(key,StringComparer.Ordinal) ? "alreadyImported" : "eligible";
            rows.Add(new(source,day,key,decision,"Filename-day policy."));
            if (decision != "eligible") continue;
            try { total = checked(total + (long)source.SizeBytes.Value); }
            catch (OverflowException) { errors.Add("Selected byte total exceeds supported range."); }
            candidates.Add(new(source,day,key,kind));
        }
        return new(rows,candidates,errors);
    }
}
