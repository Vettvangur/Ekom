using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Ekom.Umb;

internal sealed class EkomCacheInitializer
{
    private static readonly object InitializationLock = new();
    private readonly Configuration _config;
    private readonly IServiceProvider _factory;
    private readonly ILogger<EkomCacheInitializer> _logger;

    public EkomCacheInitializer(
        Configuration config,
        IServiceProvider factory,
        ILogger<EkomCacheInitializer> logger)
    {
        _config = config;
        _factory = factory;
        _logger = logger;
    }

    public void Initialize(bool isRestarting)
    {
        lock (InitializationLock)
        {
            try
            {
                if (isRestarting)
                {
                    ClearCaches();
                }

                using var cacheInitializationScope = CacheInitializationScope.Begin();
                using var priceCacheScope = PriceCache.BeginBulkInvalidation();

                foreach (var cacheEntry in _config.CacheList.Value)
                {
                    cacheEntry.FillCache();
                }

                var stockCache = _config.PerStoreStock
                    ? _factory.GetService<IPerStoreCache<StockData>>()
                    : _factory.GetService<IBaseCache<StockData>>() as ICache;

                stockCache?.FillCache();
                _factory.GetService<ICouponCache>()?.FillCache();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ekom cache initialization failed");
            }
        }
    }

    private void ClearCaches()
    {
        PriceCache.InvalidateAll();

        foreach (var cacheEntry in _config.CacheList.Value.OfType<IClearableCache>())
        {
            cacheEntry.ClearCache();
        }

        var stockCache = _config.PerStoreStock
            ? _factory.GetService<IPerStoreCache<StockData>>()
            : _factory.GetService<IBaseCache<StockData>>() as ICache;
        (stockCache as IClearableCache)?.ClearCache();

        _factory.GetService<ICouponCache>()?.Cache.Clear();
    }
}
