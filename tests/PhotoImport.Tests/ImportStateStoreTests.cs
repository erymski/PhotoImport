using PhotoImport.Importing;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public class ImportStateStoreTests
{
    [Fact]
    public void RoundTripAndExclusiveLock()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        using (var held = store.AcquireLock())
        {
            store.WriteConfiguration(f.Config);
            Assert.Equal("S21", store.ReadConfiguration()!.Devices.Single().Alias);
            Assert.Throws<IOException>(() => new ImportStateStore(f.State).AcquireLock());
        }
        using var read = store.AcquireReadLock();
        Assert.Single(store.ReadConfiguration()!.Devices);
    }

    [Fact]
    public void FailedAtomicReplacementPreservesPreviousConfigurationAndBackup()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        using var held = store.AcquireLock();
        store.WriteConfiguration(f.Config);
        store.WriteConfiguration(
            f.Config with
            {
                Devices = [f.Registration with { Alias = "new" }],
            }
        );
        Assert.Contains("S21", File.ReadAllText(Path.Combine(f.State, "config.json.bak")));
        var broken = new ImportStateStore(
            f.State,
            p =>
            {
                if (p == ImportWritePoint.BeforeConfigReplace)
                    throw new IOException("disk failure");
            }
        );
        held.Dispose();
        using var brokenLock = broken.AcquireLock();
        Assert.Throws<IOException>(() => broken.WriteConfiguration(f.Config));
        Assert.Equal("new", broken.ReadConfiguration()!.Devices.Single().Alias);
    }

    [Fact]
    public void RejectsMalformedSchemaDuplicateAliasesAndCorruptJson()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        using var held = store.AcquireLock();
        Assert.Throws<ImportException>(() =>
            store.WriteConfiguration(f.Config with { SchemaVersion = 99 })
        );
        Assert.Throws<ImportException>(() =>
            store.WriteConfiguration(
                f.Config with
                {
                    Devices = [f.Registration, f.Registration with { Id = Guid.NewGuid() }],
                }
            )
        );
        File.WriteAllText(Path.Combine(f.State, "config.json"), "{}");
        Assert.Throws<ImportException>(() => store.ReadConfiguration());
    }

    [Fact]
    public void ReadOnlyMissingStateDoesNotCreateAnything()
    {
        using var f = new ImportFixture();
        Assert.Null(new ImportStateStore(f.State).ReadConfiguration());
        Assert.False(Directory.Exists(f.State));
    }
}
