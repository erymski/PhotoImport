using System.Globalization;
using PhotoImport.Models;

namespace PhotoImport.Importing;

public static class IPhoneWindowsDayPolicy
{
    public const string Name = "iphoneWindowsDayV1";

    public static bool TryGetDay(PhotoMetadata source, out DateOnly day)
    {
        day = default;
        var timestamp = source.DateCreated;
        var text = timestamp.DisplayValue;
        return timestamp.Source == "dateCreated"
            && timestamp.HasValue
            && timestamp.Error is null
            && text is { Length: >= 19 }
            && text[10] == 'T'
            && DateTime.TryParse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _
            )
            // Use the phone-reported calendar fields, without a PC timezone conversion.
            && DateOnly.TryParseExact(
                text[..10],
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out day
            );
    }
}
