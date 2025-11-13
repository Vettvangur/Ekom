using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace Ekom.Cache;

public static class PriceCache
{
    private static readonly object _lock = new();

    private static IMemoryCache? _cache;

    // Called from Startup to inject IMemoryCache into this shared library
    public static void SetCache(IMemoryCache cache)
    {
        _cache = cache;
    }

    private static IMemoryCache Cache
        => _cache ?? throw new InvalidOperationException("PriceCache cache has not been initialized. Call PriceCache.SetCache(memoryCache) at startup.");

    // Global generation
    private static string _globalGeneration = Guid.NewGuid().ToString("N");
    public static string GlobalGeneration
    {
        get
        {
            lock (_lock) return _globalGeneration;
        }
    }

    // Per-product generations
    private static readonly ConcurrentDictionary<string, string> _itemGenerations = new();

    public static string GetItemGeneration(string productKey)
        => _itemGenerations.GetOrAdd(productKey, _ => Guid.NewGuid().ToString("N"));

    public static void InvalidateItem(string productKey)
    {
        _itemGenerations[productKey] = Guid.NewGuid().ToString("N");
        (Cache as MemoryCache)?.Compact(0.05);
    }

    public static void InvalidateAll()
    {
        lock (_lock)
        {
            _globalGeneration = Guid.NewGuid().ToString("N");
            _itemGenerations.Clear();
        }

        (Cache as MemoryCache)?.Compact(1.0);
    }
}
