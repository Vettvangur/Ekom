namespace Ekom.Umb.Services;

/// <summary>
/// Marks content saves made by the import. The import writes stock straight to the stock table and never
/// edits a node's stock property, so the save handler must not push that stored value back over the table.
/// </summary>
internal static class ImportSaveScope
{
    private static readonly AsyncLocal<int> Depth = new();

    internal static bool IsActive => Depth.Value > 0;

    internal static IDisposable Begin()
    {
        Depth.Value++;
        return new Scope();
    }

    private sealed class Scope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            Depth.Value--;
        }
    }
}
