using PhotoImport.Importing;

namespace PhotoImport.Tests.Fakes;

public sealed class ImportFixture : IDisposable
{
    public string Root { get; } =
        Path.Combine(Path.GetTempPath(), "photo-import-tests-" + Guid.NewGuid().ToString("N"));
    public string State => Path.Combine(Root, "state");
    public string Destination => Path.Combine(Root, "photos");
    public ImportRegistration Registration =>
        FilenameImportPlannerTests.Registration() with
        {
            Destination = Destination,
        };
    public ImportConfiguration Config => new(1, "filenameImporter", [Registration]);

    public ImportFixture() => Directory.CreateDirectory(Destination);

    public void Dispose()
    {
        if (Directory.Exists(Root))
            Directory.Delete(Root, true);
    }
}
