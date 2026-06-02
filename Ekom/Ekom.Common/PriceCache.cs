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

        gen = RaiseGenerationCreatedAsync(itemKey, gen, CancellationToken.None).GetAwaiter().GetResult();

        return gen;
    }

    public static async ValueTask<string> GetItemGenerationAsync(string itemKey, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();

        var gen = _itemGenerations.GetOrAdd(itemKey, _ => Guid.NewGuid().ToString("N"));

        gen = await RaiseGenerationCreatedAsync(itemKey, gen, ct).ConfigureAwait(false);

        return gen;
    }

    public static void InvalidateItem(string itemKey)
    {
        var newGen = Guid.NewGuid().ToString("N");

        _itemGenerations[itemKey] = newGen;


        OnGenerationInvalidated?.Invoke(
            null,
            new PriceGenerationEventArgs(itemKey, newGen)
        );

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

    public static event Func<PriceGenerationEventArgs, CancellationToken, ValueTask>? OnGenerationCreatedAsync;
    public static event EventHandler<PriceGenerationEventArgs>? OnGenerationInvalidated;

    private static async ValueTask<string> RaiseGenerationCreatedAsync(string itemKey, string gen, CancellationToken ct)
    {
        var args = new PriceGenerationEventArgs(itemKey, gen);
        var handlers = OnGenerationCreatedAsync;

        if (handlers is null)
            return args.Generation;

        foreach (var handler in handlers.GetInvocationList())
        {
            ct.ThrowIfCancellationRequested();
            await ((Func<PriceGenerationEventArgs, CancellationToken, ValueTask>)handler)(args, ct).ConfigureAwait(false);
        }

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
