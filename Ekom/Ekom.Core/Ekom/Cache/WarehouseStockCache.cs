using Ekom.Models;
using Ekom.Repositories;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ekom.Cache;

internal sealed class WarehouseStockCache
{
    private readonly IWarehouseStockRepository _repository;
    private readonly ILogger<WarehouseStockCache> _logger;
    private ConcurrentDictionary<WarehouseStockCacheKey, WarehouseStockData> _cache = new();
    private bool _isReady;

    public WarehouseStockCache(
        IWarehouseStockRepository repository,
        ILogger<WarehouseStockCache> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    internal SemaphoreSlim MutationLock { get; } = new(1, 1);

    public void FillCache()
    {
        MutationLock.Wait();
        try
        {
            var stopwatch = Stopwatch.StartNew();
            _logger.LogInformation("Starting to fill warehouse stock cache...");

            List<WarehouseStockData> stock = _repository.GetAllAsync().GetAwaiter().GetResult();
            var nextCache = new ConcurrentDictionary<WarehouseStockCacheKey, WarehouseStockData>();
            foreach (WarehouseStockData item in stock)
            {
                WarehouseStockCacheKey key = WarehouseStockCacheKey.Create(item.StoreAlias, item.WarehouseKey, item.Sku);
                nextCache.AddOrUpdate(
                    key,
                    item,
                    (_, current) => current.UpdateDate >= item.UpdateDate ? current : item);
            }

            if (nextCache.Count != stock.Count)
            {
                _logger.LogWarning(
                    "Warehouse stock persistence contained {DuplicateCount} duplicate normalized identities; the newest balances were cached.",
                    stock.Count - nextCache.Count);
            }

            Interlocked.Exchange(ref _cache, nextCache);
            Volatile.Write(ref _isReady, true);

            stopwatch.Stop();
            _logger.LogInformation(
                "Finished filling warehouse stock cache with {Count} items. Time it took to fill: {Elapsed}",
                nextCache.Count,
                stopwatch.Elapsed);
        }
        finally
        {
            MutationLock.Release();
        }
    }

    internal WarehouseStockData? Get(string storeAlias, Guid warehouseKey, string sku)
    {
        EnsureReady();
        _cache.TryGetValue(WarehouseStockCacheKey.Create(storeAlias, warehouseKey, sku), out WarehouseStockData? stock);
        return stock;
    }

    internal IReadOnlyList<WarehouseStockData> Get(
        string storeAlias,
        IReadOnlySet<string> skus,
        Guid? warehouseKey = null)
    {
        EnsureReady();
        string normalizedStoreAlias = WarehouseStockCacheKey.Normalize(storeAlias);

        return _cache
            .Where(item => item.Key.StoreAlias == normalizedStoreAlias
                && (!warehouseKey.HasValue || item.Key.WarehouseKey == warehouseKey.Value)
                && skus.Contains(item.Key.Sku))
            .Select(item => item.Value)
            .ToList();
    }

    internal void AddOrUpdate(WarehouseStockData stock)
    {
        _cache[WarehouseStockCacheKey.Create(stock.StoreAlias, stock.WarehouseKey, stock.Sku)] = stock;
    }

    internal void Remove(string storeAlias, Guid warehouseKey, string sku)
    {
        _cache.TryRemove(WarehouseStockCacheKey.Create(storeAlias, warehouseKey, sku), out _);
    }

    internal void EnsureReady()
    {
        if (!Volatile.Read(ref _isReady))
        {
            throw new InvalidOperationException("Warehouse stock cache has not been initialized.");
        }
    }

    private readonly record struct WarehouseStockCacheKey(string StoreAlias, Guid WarehouseKey, string Sku)
    {
        public static WarehouseStockCacheKey Create(string storeAlias, Guid warehouseKey, string sku)
        {
            return new WarehouseStockCacheKey(Normalize(storeAlias), warehouseKey, Normalize(sku));
        }

        public static string Normalize(string value) => value.Trim().ToUpperInvariant();
    }
}
