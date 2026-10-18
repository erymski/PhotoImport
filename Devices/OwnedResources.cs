namespace PhotoImport.Devices;

// Device discovery creates disposable handles before a session is selected.
internal sealed class OwnedResources<T>(Action<string> cleanupWarning) : IDisposable
    where T : class, IDisposable
{
    private readonly List<T> items = [];
    private T? retained;
    private bool disposed;
    public IReadOnlyList<T> Items => items;

    public static OwnedResources<T> Capture(
        IEnumerable<T> source,
        CancellationToken token,
        Action<string> cleanupWarning
    )
    {
        var owner = new OwnedResources<T>(cleanupWarning);
        try
        {
            foreach (var item in source)
            {
                owner.items.Add(item);
                token.ThrowIfCancellationRequested();
            }
            return owner;
        }
        catch
        {
            owner.Dispose();
            throw;
        }
    }

    public void Retain(T item) => retained = item;

    public void Dispose()
    {
        if (disposed)
            return;
        disposed = true;
        foreach (var item in items)
        {
            if (ReferenceEquals(item, retained))
                continue;
            try
            {
                WpdOperation.Run(item.Dispose, default);
            }
            catch (Exception ex)
            {
                cleanupWarning($"Device handle cleanup warning: {ex.Message}");
            }
        }
    }
}
