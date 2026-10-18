using System.Text.Json;
using PhotoImport.Devices;
using PhotoImport.Models;
using PhotoImport.Reporting;
using PhotoImport.Services;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public class ProbeReportWriterTests
{
    private static ProbeReport Report()
    {
        using var device = MetadataProbeTests.SimpleDevice();
        var entry = FakePhotoDevice.Entry("p", "\\DCIM\\a.jpg");
        device.Metadata["p"] = FakePhotoDevice.Photo(entry) with
        {
            PersistentObjectId = "persistent-candidate",
            DateAuthored = WpdMetadataMapper.ObserveTimestamp("dateAuthored", new DateTime(2026, 10, 12, 14, 32, 5), false),
            DateCreated = WpdMetadataMapper.ObserveTimestamp("dateCreated", new DateTimeOffset(2026, 10, 12, 14, 32, 5, TimeSpan.FromHours(2)), true),
            Warnings = ["Capture semantics unverified"]
        };
        return MetadataProbe.Run(device, FakePhotoDevice.Info(), default);
    }

    [Fact]
    public void ConsoleShowsMetadataAndUncertainty()
    {
        using var text = new StringWriter(); ProbeReportWriter.WriteConsole(Report(), text);
        var value = text.ToString();
        foreach (var expected in new[] { "Phone", "USB", "unverified", "\\DCIM", "a.jpg", "42", "dateAuthored", "dateCreated",
            "dateModified", "ptpCaptureDate", "persistent-candidate", "unknown", "missing", "Capture semantics unverified", "Elapsed", "complete" })
            Assert.Contains(expected, value, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void JsonContainsCompleteTimestampEvidenceAndNulls()
    {
        var path = Path.Combine(Path.GetTempPath(), "probe-" + Guid.NewGuid() + ".json");
        try
        {
            ProbeReportWriter.WriteJson(Report(), path);
            using var doc = JsonDocument.Parse(File.ReadAllText(path)); var json = doc.RootElement;
            Assert.Equal(1, json.GetProperty("withCaptureDateCount").GetInt32());
            var photo = json.GetProperty("photos")[0];
            Assert.Equal("\\DCIM\\a.jpg", photo.GetProperty("sourcePath").GetString());
            Assert.Equal(JsonValueKind.Null, photo.GetProperty("dateAuthored").GetProperty("rawValue").ValueKind);
            Assert.Equal("2026-10-12T14:32:05.0000000", photo.GetProperty("dateAuthored").GetProperty("displayValue").GetString());
            Assert.Equal("unknown", photo.GetProperty("dateAuthored").GetProperty("offsetPresence").GetString());
            Assert.Equal("2026-10-12T14:32:05.0000000+02:00", photo.GetProperty("dateCreated").GetProperty("rawValue").GetString());
            Assert.Equal("System.DateTimeOffset", photo.GetProperty("dateCreated").GetProperty("rawType").GetString());
            Assert.Equal("missing", photo.GetProperty("ptpCaptureDate").GetProperty("availability").GetString());
        }
        finally { if (File.Exists(path)) File.Delete(path); }
    }
}
