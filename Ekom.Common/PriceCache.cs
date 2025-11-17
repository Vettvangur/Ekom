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

    private static readonly ConcurrentDictionary<string, string> _itemGenerations = new();

    public static string GetItemGeneration(string itemKey)
    {
        var gen = _itemGenerations.GetOrAdd(itemKey, _ => Guid.NewGuid().ToString("N"));

        gen = RaiseGenerationCreated(itemKey, gen);

        return gen;
    }

    public static void InvalidateItem(string itemKey)
    {
        _itemGenerations[itemKey] = Guid.NewGuid().ToString("N");
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

    public static event EventHandler<PriceGenerationEventArgs>? OnGenerationCreated;

    private static string RaiseGenerationCreated(string itemKey, string gen)
    {
        var args = new PriceGenerationEventArgs(itemKey, gen);
        OnGenerationCreated?.Invoke(null, args);
        return args.Generation;
    }

    public class PriceGenerationEventArgs : EventArgs
    {
        public string ItemKey { get; }
        public string Generation { get; set; }

        public PriceGenerationEventArgs(string itemKey, string generation)
        {
            ItemKey = itemKey;
            Generation = generation;
        }
    }
}
