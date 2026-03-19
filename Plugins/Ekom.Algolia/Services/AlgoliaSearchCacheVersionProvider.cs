using System.Collections.Concurrent;
using System.Threading;

namespace Ekom.Algolia.Services;

internal sealed class AlgoliaSearchCacheVersionProvider
{
    private readonly ConcurrentDictionary<string, long> _versions = new(StringComparer.OrdinalIgnoreCase);

    public long GetVersion(string storeAlias)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
            return 0;

        return _versions.TryGetValue(storeAlias, out var version)
            ? version
            : 0;
    }

    public void InvalidateStore(string storeAlias)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
            return;

        _versions.AddOrUpdate(
            storeAlias,
            static _ => 1,
            static (_, current) => Interlocked.Increment(ref current));
    }
}
