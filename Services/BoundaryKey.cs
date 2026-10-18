using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using PhotoImport.Models;

namespace PhotoImport.Services;

public static class BoundaryKey
{
    public static string Create(string deviceId, PhotoMetadata photo, DateOnly captureDay)
    {
        if (photo.SizeBytes is null or 0)
            throw new PreviewInputException(
                "A known nonzero source size is required for a boundary key."
            );
        return Convert.ToHexString(
            SHA256.HashData(
                JsonSerializer.SerializeToUtf8Bytes(
                    new object[]
                    {
                        deviceId,
                        photo.SourcePath,
                        photo.SizeBytes.Value,
                        captureDay.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    }
                )
            )
        );
    }
}
