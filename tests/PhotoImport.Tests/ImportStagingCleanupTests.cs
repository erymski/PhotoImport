using PhotoImport.Importing;
using PhotoImport.Tests.Fakes;

namespace PhotoImport.Tests;

public class ImportStagingCleanupTests
{
    [Fact]
    public void SuccessfulImportRemovesOnlyItsEmptySessionDirectory()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var provider = ImportRunnerTests.Setup(f, store);
        var staging = Path.Combine(f.Destination, ".phone-photo-sync");
        var other = Directory.CreateDirectory(Path.Combine(staging, "other-session")).FullName;
        var result = new ImportRunner(store, new()).Run(provider, null, default);
        Assert.Equal(0, result.ExitCode);
        Assert.True(result.CheckpointCommitted);
        Assert.Equal(new[] { other }, Directory.GetDirectories(staging));
        Assert.Equal(
            new byte[] { 1, 2, 3 },
            File.ReadAllBytes(Path.Combine(f.Destination, "20260904_1.mp4"))
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void SuccessfulImportPreservesNonemptySessionDirectory(bool nestedDirectory)
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var provider = ImportRunnerTests.Setup(f, store);
        string? retained = null;
        var transfer = new MediaTransfer(fault: point =>
        {
            if (point != TransferPoint.AfterCompleted)
                return;
            var session = Directory
                .GetDirectories(Path.Combine(f.Destination, ".phone-photo-sync"))
                .Single();
            retained = Path.Combine(session, "keep");
            if (nestedDirectory)
                Directory.CreateDirectory(retained);
            else
                File.WriteAllText(retained, "keep");
        });
        Assert.Equal(0, new ImportRunner(store, transfer).Run(provider, null, default).ExitCode);
        Assert.True(
            nestedDirectory ? Directory.Exists(retained) : File.ReadAllText(retained!) == "keep"
        );
    }

    [Fact]
    public void InterruptedPartialIsKeptAndSuccessfulResumeRemovesSessionDirectory()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var provider = ImportRunnerTests.Setup(f, store);
        var transfer = new MediaTransfer(fault: point =>
        {
            if (point == TransferPoint.AfterPrepared)
                throw new IOException("interrupted before rename");
        });
        Assert.Equal(5, new ImportRunner(store, transfer).Run(provider, null, default).ExitCode);
        var staging = Path.Combine(f.Destination, ".phone-photo-sync");
        var session = Directory.GetDirectories(staging).Single();
        Assert.Single(Directory.GetFiles(session, "*.partial"));
        Assert.True(store.HasPending);
        Assert.Equal(0, new ImportRunner(store, new()).Run(provider, null, default).ExitCode);
        Assert.False(Directory.Exists(session));
        Assert.Single(provider.Device.Opened);
        Assert.False(store.HasPending);
    }

    [Fact]
    public void CommittedArchiveRetryAlsoRemovesEmptySessionDirectory()
    {
        using var f = new ImportFixture();
        var store = new ImportStateStore(f.State);
        var provider = ImportRunnerTests.Setup(f, store);
        var broken = new ImportStateStore(
            f.State,
            point =>
            {
                if (point == ImportWritePoint.BeforeArchive)
                    throw new IOException("archive unavailable");
            }
        );
        var first = new ImportRunner(broken, new()).Run(provider, null, default);
        Assert.True(first.CheckpointCommitted);
        var session = Directory
            .GetDirectories(Path.Combine(f.Destination, ".phone-photo-sync"))
            .Single();
        Assert.Equal(0, new ImportRunner(store, new()).Run(provider, null, default).ExitCode);
        Assert.False(Directory.Exists(session));
        Assert.Single(provider.Device.Opened);
    }

    [Fact]
    public void CleanupRejectsReplacedStagingJunctionWithoutDeletingTargetSession()
    {
        using var f = new ImportFixture();
        var normal = new ImportStateStore(f.State);
        var provider = ImportRunnerTests.Setup(f, normal);
        var staging = Path.Combine(f.Destination, ".phone-photo-sync");
        var target = Directory.CreateDirectory(Path.Combine(f.Root, "outside")).FullName;
        string? outsideSession = null;
        var linked = false;
        var store = new ImportStateStore(
            f.State,
            point =>
            {
                if (point != ImportWritePoint.BeforeArchive)
                    return;
                var sessionName = Path.GetFileName(Directory.GetDirectories(staging).Single());
                outsideSession = Directory
                    .CreateDirectory(Path.Combine(target, sessionName))
                    .FullName;
                Directory.Move(staging, staging + "-old");
                var start = new System.Diagnostics.ProcessStartInfo("cmd.exe")
                {
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                };
                foreach (var arg in new[] { "/c", "mklink", "/J", staging, target })
                    start.ArgumentList.Add(arg);
                using var process = System.Diagnostics.Process.Start(start)!;
                process.WaitForExit();
                Assert.Equal(0, process.ExitCode);
                linked = true;
            }
        );
        try
        {
            var result = new ImportRunner(store, new()).Run(provider, null, default);
            Assert.Equal(0, result.ExitCode);
            Assert.True(Directory.Exists(outsideSession));
            Assert.Contains(
                result.Warnings,
                w => w.Contains("staging", StringComparison.OrdinalIgnoreCase)
            );
        }
        finally
        {
            if (linked)
                Directory.Delete(staging, false);
        }
    }

    [Fact]
    public void CleanupFailureWarnsWithoutFailingCommittedImport()
    {
        using var f = new ImportFixture();
        var normal = new ImportStateStore(f.State);
        var provider = ImportRunnerTests.Setup(f, normal);
        ImportDirectoryLease? held = null;
        var store = new ImportStateStore(
            f.State,
            point =>
            {
                if (point == ImportWritePoint.BeforeArchive)
                {
                    var session = Directory
                        .GetDirectories(Path.Combine(f.Destination, ".phone-photo-sync"))
                        .Single();
                    held = ImportDirectoryLease.Acquire(
                        f.Destination,
                        Guid.ParseExact(Path.GetFileName(session), "N")
                    );
                }
            }
        );
        try
        {
            var result = new ImportRunner(store, new()).Run(provider, null, default);
            Assert.Equal(0, result.ExitCode);
            Assert.True(result.CheckpointCommitted);
            Assert.Contains(
                result.Warnings,
                w => w.Contains("staging", StringComparison.OrdinalIgnoreCase)
            );
            Assert.False(store.HasPending);
        }
        finally
        {
            held?.Dispose();
        }
    }
}
