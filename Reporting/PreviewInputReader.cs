using System.Text.Json;
using PhotoImport.Models;

namespace PhotoImport.Reporting;

public static class PreviewInputReader
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public static ProbeReport ReadInventory(string path, CancellationToken token) =>
        Read(
            path,
            token,
            root =>
            {
                Device(Object(root, "device"));
                Status(root, "complete", "incomplete", "cancelled");
                String(root, "startedAt");
                String(root, "enumerationDuration");
                Strings(root, "searchedRoots");
                foreach (var photo in Array(root, "photos"))
                {
                    IsObject(photo);
                    String(photo, "objectId", allowEmpty: true);
                    var source = String(photo, "sourcePath")!;
                    DevicePath(source);
                    var name = String(photo, "fileName");
                    if (name != Path.GetFileName(source))
                        Fail("Inventory filename differs from source path.");
                    NullableString(photo, "persistentObjectId");
                    Unsigned(photo, "sizeBytes");
                    NullableString(photo, "contentType");
                    NullableString(photo, "format");
                    foreach (
                        var field in new[]
                        {
                            "dateAuthored",
                            "dateCreated",
                            "dateModified",
                            "ptpCaptureDate",
                        }
                    )
                    {
                        var date = Object(photo, field);
                        String(date, "source");
                        String(date, "availability");
                        String(date, "offsetPresence");
                        foreach (
                            var value in new[] { "rawValue", "rawType", "displayValue", "error" }
                        )
                            NullableString(date, value);
                    }
                    Strings(photo, "warnings");
                }
                foreach (var failure in Array(root, "errors"))
                {
                    IsObject(failure);
                    String(failure, "operation");
                    String(failure, "message", allowEmpty: true);
                    NullableString(failure, "path");
                    NullableString(failure, "objectId");
                }
                OptionalStrings(root, "warnings");
                var report = root.Deserialize<ProbeReport>(Options)!;
                if (
                    root.TryGetProperty("photoCount", out var count)
                    && (!count.TryGetInt32(out var countValue) || countValue != report.PhotoCount)
                )
                    Fail("Inventory count disagrees with rows.");
                return report;
            }
        );

    public static CaptureReportInput ReadCapture(string path, CancellationToken token) =>
        Read(
            path,
            token,
            root =>
            {
                int? version = null;
                if (root.TryGetProperty("schemaVersion", out var schema))
                {
                    if (
                        schema.ValueKind != JsonValueKind.Number
                        || !schema.TryGetInt32(out var v)
                        || v != 2
                    )
                        Fail("Unsupported capture schema.");
                    version = schema.GetInt32();
                }
                var device = Object(root, "device");
                Device(device);
                Status(root, "complete", "incomplete", "cancelled", "deviceError");
                Strings(root, "errors");
                OptionalStrings(root, "warnings");
                foreach (var file in Array(root, "files"))
                {
                    IsObject(file);
                    DevicePath(String(file, "sourcePath")!);
                    Number(file, "elapsedSeconds");
                    if (version == 2 || file.TryGetProperty("source", out _))
                    {
                        var source = Required(file, "source");
                        if (source.ValueKind != JsonValueKind.Null)
                        {
                            IsObject(source);
                            DevicePath(String(source, "canonicalPath")!);
                            Unsigned(source, "sizeBytes");
                            NullableString(source, "persistentObjectId");
                        }
                    }
                    var observation = Object(file, "observation");
                    var bytes = Required(observation, "bytesRead");
                    if (
                        bytes.ValueKind != JsonValueKind.Number
                        || !bytes.TryGetInt32(out var read)
                        || read < 0
                        || read > 262144
                    )
                        Fail("Invalid bounded byte count.");
                    Choice(
                        String(observation, "outcome")!,
                        "available",
                        "missing",
                        "invalid",
                        "ambiguous",
                        "unsupported",
                        "incomplete",
                        "cancelled"
                    );
                    var fields = Object(observation, "rawFields");
                    foreach (var field in fields.EnumerateObject())
                        StringOrNull(field.Value);
                    NullableString(observation, "captureClock");
                    NullableString(observation, "captureTimestamp");
                    NullableString(observation, "error");
                    Strings(observation, "warnings");
                    if (version == 2)
                    {
                        String(observation, "source");
                        Choice(
                            String(observation, "captureDayStatus")!,
                            "available",
                            "missing",
                            "invalid",
                            "ambiguous"
                        );
                        NullableString(observation, "captureDay");
                        foreach (var directory in Array(observation, "captureDirectories"))
                        {
                            IsObject(directory);
                            NullableString(directory, "dateTimeOriginal");
                            NullableString(directory, "subSecTimeOriginal");
                            NullableString(directory, "offsetTimeOriginal");
                        }
                    }
                }
                return new CaptureReportInput(
                    version,
                    device.Deserialize<DeviceInfo>(Options)!,
                    String(root, "completionStatus")!,
                    Required(root, "files").Deserialize<JpegExifFileResult[]>(Options)!
                )
                {
                    Errors = Required(root, "errors").Deserialize<string[]>(Options)!,
                };
            }
        );

    public static DayCheckpoint ReadCheckpoint(string path, CancellationToken token) =>
        Read(
            path,
            token,
            root =>
            {
                var schema = Required(root, "schemaVersion");
                if (
                    schema.ValueKind != JsonValueKind.Number
                    || !schema.TryGetInt32(out var version)
                    || version != 1
                )
                    Fail("Unsupported checkpoint schema.");
                String(root, "deviceId");
                String(root, "cutoffDay");
                Choice(String(root, "boundaryMode")!, "completedDay", "tracked");
                Strings(root, "boundaryKeys");
                return root.Deserialize<DayCheckpoint>(Options)!;
            }
        );

    private static T Read<T>(string path, CancellationToken token, Func<JsonElement, T> parse)
    {
        token.ThrowIfCancellationRequested();
        try
        {
            using var stream = File.OpenRead(path);
            using var document = JsonDocument.Parse(stream);
            token.ThrowIfCancellationRequested();
            CheckDuplicates(document.RootElement);
            IsObject(document.RootElement);
            var result = parse(document.RootElement);
            token.ThrowIfCancellationRequested();
            return result;
        }
        catch (PreviewInputException)
        {
            throw;
        }
        catch (Exception ex)
            when (ex
                    is JsonException
                        or FormatException
                        or InvalidOperationException
                        or OverflowException
            )
        {
            throw new PreviewInputException($"Invalid input {path}: {ex.Message}");
        }
    }

    private static void CheckDuplicates(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in value.EnumerateObject())
            {
                if (!names.Add(property.Name))
                    Fail("Duplicate JSON property: " + property.Name);
                CheckDuplicates(property.Value);
            }
        }
        else if (value.ValueKind == JsonValueKind.Array)
            foreach (var item in value.EnumerateArray())
                CheckDuplicates(item);
    }

    private static void Device(JsonElement device)
    {
        String(device, "id");
        var ids = Object(device, "identifiers");
        foreach (var field in ids.EnumerateObject())
            StringOrNull(field.Value);
        OptionalStrings(device, "warnings");
    }

    private static void Status(JsonElement root, params string[] choices) =>
        Choice(String(root, "completionStatus")!, choices);

    private static void Choice(string value, params string[] choices)
    {
        if (!choices.Contains(value, StringComparer.Ordinal))
            Fail("Unknown value: " + value);
    }

    private static void IsObject(JsonElement value)
    {
        if (value.ValueKind != JsonValueKind.Object)
            Fail("Expected JSON object.");
    }

    private static JsonElement Required(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value)
            ? value
            : throw new PreviewInputException("Missing required property: " + name);

    private static JsonElement Object(JsonElement root, string name)
    {
        var value = Required(root, name);
        IsObject(value);
        return value;
    }

    private static JsonElement.ArrayEnumerator Array(JsonElement root, string name)
    {
        var value = Required(root, name);
        if (value.ValueKind != JsonValueKind.Array)
            Fail("Expected array: " + name);
        return value.EnumerateArray();
    }

    private static string? String(JsonElement root, string name, bool allowEmpty = false)
    {
        var value = Required(root, name);
        if (value.ValueKind != JsonValueKind.String)
            Fail("Expected string: " + name);
        var text = value.GetString();
        if (!allowEmpty && string.IsNullOrWhiteSpace(text))
            Fail("Empty string: " + name);
        return text;
    }

    private static void NullableString(JsonElement root, string name) =>
        StringOrNull(Required(root, name));

    private static void StringOrNull(JsonElement value)
    {
        if (value.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            Fail("Expected string or null.");
    }

    private static void Strings(JsonElement root, string name)
    {
        foreach (var value in Array(root, name))
            if (value.ValueKind != JsonValueKind.String)
                Fail("Expected string array: " + name);
    }

    private static void OptionalStrings(JsonElement root, string name)
    {
        if (root.TryGetProperty(name, out _))
            Strings(root, name);
    }

    private static void Unsigned(JsonElement root, string name)
    {
        var value = Required(root, name);
        if (
            value.ValueKind != JsonValueKind.Null
            && (value.ValueKind != JsonValueKind.Number || !value.TryGetUInt64(out _))
        )
            Fail("Expected unsigned size or null.");
    }

    private static void Number(JsonElement root, string name)
    {
        var value = Required(root, name);
        if (
            value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var number)
            || !double.IsFinite(number)
            || number < 0
        )
            Fail("Expected nonnegative finite number.");
    }

    private static void DevicePath(string path)
    {
        if (
            !path.StartsWith('\\')
            || path.StartsWith("\\\\", StringComparison.Ordinal)
            || path.IndexOfAny(['/', ':', '*', '?', '\0']) >= 0
            || path.Split('\\').Skip(1).Any(p => p.Length == 0 || p is "." or "..")
        )
            Fail("Invalid absolute device file path.");
    }

    private static void Fail(string message) => throw new PreviewInputException(message);
}
