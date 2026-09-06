using Microsoft.Extensions.Caching.Memory;
using System.Collections.Concurrent;

namespace Ekom.Cache;

public static class PriceCache
{
    private static readonly object _lock = new();
    private static int _bulkInvalidationDepth;
    private static bool _compactionPending;

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

    public static string GetItemGeneration(string itemKey) => GetItemGeneration(itemKey, null);

    public static string GetItemGeneration(string itemKey, string? storeAlias)
    {
        var gen = _itemGenerations.GetOrAdd(itemKey, _ => Guid.NewGuid().ToString("N"));

        gen = RaiseGenerationCreatedAsync(itemKey, gen, storeAlias, CancellationToken.None).GetAwaiter().GetResult();

        return gen;
    }

    public static ValueTask<string> GetItemGenerationAsync(string itemKey, CancellationToken ct = default)
        => GetItemGenerationAsync(itemKey, null, ct);

    public static async ValueTask<string> GetItemGenerationAsync(string itemKey, string? storeAlias, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        var gen = _itemGenerations.GetOrAdd(itemKey, _ => Guid.NewGuid().ToString("N"));

        gen = await RaiseGenerationCreatedAsync(itemKey, gen, storeAlias, ct).ConfigureAwait(false);

        return gen;
    }

    public static void InvalidateItem(string itemKey) => InvalidateItem(itemKey, null);

    public static void InvalidateItem(string itemKey, string? storeAlias)
    {
        var newGen = Guid.NewGuid().ToString("N");

        _itemGenerations[itemKey] = newGen;


        OnGenerationInvalidated?.Invoke(
            null,
            new PriceGenerationEventArgs(itemKey, newGen, storeAlias)
        );

        if (DeferCompaction())
        {
            return;
        }

        CompactCache();
    }

    public static IDisposable BeginBulkInvalidation()
    {
        lock (_lock)
        {
            _bulkInvalidationDepth++;
        }

        return new BulkInvalidationScope();
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

    private static bool DeferCompaction()
    {
        lock (_lock)
        {
            if (_bulkInvalidationDepth == 0)
            {
                return false;
            }

            _compactionPending = true;
            return true;
        }
    }

    private static void CompactCache() => (_cache as MemoryCache)?.Compact(0.05);

    private sealed class BulkInvalidationScope : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;

            var compact = false;
            lock (_lock)
            {
                _bulkInvalidationDepth--;
                if (_bulkInvalidationDepth == 0 && _compactionPending)
                {
                    _compactionPending = false;
                    compact = true;
                }
            }

            if (compact)
            {
                CompactCache();
            }
        }
    }

    private static async ValueTask<string> RaiseGenerationCreatedAsync(
        string itemKey,
        string gen,
        string? storeAlias,
        CancellationToken ct)
    {
        var args = new PriceGenerationEventArgs(itemKey, gen, storeAlias);
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

        /// <summary>
        /// Store alias associated with the price operation, or null when the store is unknown.
        /// </summary>
        public string? StoreAlias { get; }
        public string Generation { get; set; }

        /// <summary>
        /// Ambient <see cref="Ekom.PricingContext"/> active when the generation was requested.
        /// Handlers that vary prices by context should fold the relevant values into
        /// <see cref="Generation"/> so cached prices are partitioned accordingly.
        /// Case-insensitive, empty when no context is active.
        /// </summary>
        public IReadOnlyDictionary<string, string> PricingContext { get; }

        public PriceGenerationEventArgs(string itemKey, string generation)
            : this(itemKey, generation, null)
        {
        }

        public PriceGenerationEventArgs(string itemKey, string generation, string? storeAlias)
        {
            ItemKey = itemKey;
            Generation = generation;
            StoreAlias = storeAlias;
            PricingContext = Ekom.PricingContext.CurrentOrEmpty;
        }
    }
}
