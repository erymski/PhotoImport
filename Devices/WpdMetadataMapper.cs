using System.Globalization;
using PhotoImport.Models;

namespace PhotoImport.Devices;

public static class WpdMetadataMapper
{
    public static TimestampObservation ObserveTimestamp(string source, object? value, bool rawValueAvailable, string? error = null)
    {
        if (error is not null) return new(source, null, null, null, "unknown", "error", error);
        if (value is null) return TimestampObservation.Missing(source);
        var display = value switch
        {
            // Keep the clock fields, without treating a library-assigned Kind as source timezone evidence.
            DateTime date => date.ToString("yyyy-MM-dd'T'HH:mm:ss.fffffff", CultureInfo.InvariantCulture),
            DateTimeOffset date => date.ToString("O", CultureInfo.InvariantCulture),
            _ => Convert.ToString(value, CultureInfo.InvariantCulture)
        };
        return new(source, rawValueAvailable ? display : null,
            rawValueAvailable ? value.GetType().FullName : null, display,
            rawValueAvailable && value is DateTimeOffset ? "present" : "unknown",
            rawValueAvailable ? "raw" : "projected", null);
    }
}
