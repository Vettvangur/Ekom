namespace Ekom.Algolia.Indexing;

/// <summary>
/// Suppresses automatic incremental Algolia indexing for the current asynchronous execution flow.
/// </summary>
public static class AlgoliaIndexingScope
{
    private static readonly AsyncLocal<int> Depth = new();

    /// <summary>
    /// Gets a value indicating whether automatic incremental Algolia indexing is suppressed.
    /// </summary>
    public static bool IsSuppressed => Depth.Value > 0;

    /// <summary>
    /// Suppresses automatic incremental Algolia indexing until the returned scope is disposed.
    /// </summary>
    public static IDisposable Suppress()
    {
        Depth.Value++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;

            _disposed = true;
            Depth.Value--;
        }
    }
}
