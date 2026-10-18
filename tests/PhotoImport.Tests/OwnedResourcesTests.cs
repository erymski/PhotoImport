using PhotoImport.Devices;

namespace PhotoImport.Tests;

public class OwnedResourcesTests
{
    private sealed class Resource(bool fails = false) : IDisposable
    {
        public int DisposalCount { get; private set; }

        public void Dispose()
        {
            DisposalCount++;
            if (fails)
                throw new IOException("Cleanup failed");
        }
    }

    [Fact]
    public void AcquisitionFailureDisposesAlreadyCreatedPrefix()
    {
        var first = new Resource();
        IEnumerable<Resource> Sequence()
        {
            yield return first;
            throw new IOException("Enumeration failed");
        }
        Assert.Throws<IOException>(() =>
            OwnedResources<Resource>.Capture(Sequence(), default, _ => { })
        );
        Assert.Equal(1, first.DisposalCount);
    }

    [Fact]
    public void CancellationDisposesAllAcquiredResources()
    {
        var a = new Resource();
        var b = new Resource();
        using var cts = new CancellationTokenSource();
        IEnumerable<Resource> Sequence()
        {
            yield return a;
            cts.Cancel();
            yield return b;
        }
        Assert.Throws<OperationCanceledException>(() =>
            OwnedResources<Resource>.Capture(Sequence(), cts.Token, _ => { })
        );
        Assert.Equal(1, a.DisposalCount);
        Assert.Equal(1, b.DisposalCount);
    }

    [Fact]
    public void RetainedSessionSurvivesOtherResourcesAndCleanupFailure()
    {
        var a = new Resource(true);
        var b = new Resource();
        var selected = new Resource();
        var warnings = new List<string>();
        using (
            var resources = OwnedResources<Resource>.Capture(
                [a, b, selected],
                default,
                warnings.Add
            )
        )
        {
            resources.Retain(selected);
        }
        Assert.Equal(1, a.DisposalCount);
        Assert.Equal(1, b.DisposalCount);
        Assert.Equal(0, selected.DisposalCount);
        Assert.Contains(warnings, x => x.Contains("Cleanup failed"));
        selected.Dispose();
    }
}
