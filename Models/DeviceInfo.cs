namespace PhotoImport.Models;

public record DeviceInfo(string Id, string? FriendlyName, string? Manufacturer, string? Model,
    string? Transport, IReadOnlyDictionary<string, string?> Identifiers)
{
    public IReadOnlyList<string> Warnings { get; init; } = [];
}
