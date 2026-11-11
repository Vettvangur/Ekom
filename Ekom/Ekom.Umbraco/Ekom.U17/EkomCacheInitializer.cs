using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Umb.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb;

internal sealed class EkomCacheInitializer
{
    private static readonly object InitializationLock = new();
    private readonly Configuration _config;
    private readonly IServiceProvider _factory;
    private readonly IUmbracoContextFactory _contextFactory;
    private readonly IPublishedContentQuery _publishedContentQuery;
    private readonly EkomCacheBuildContext _cacheBuildContext;
    private readonly Umbraco17ContentCache _contentCache;
    private readonly ILogger<EkomCacheInitializer> _logger;

    public EkomCacheInitializer(
        Configuration config,
        IServiceProvider factory,
        IUmbracoContextFactory contextFactory,
        IPublishedContentQuery publishedContentQuery,
        EkomCacheBuildContext cacheBuildContext,
        Umbraco17ContentCache contentCache,
        ILogger<EkomCacheInitializer> logger)
    {
        _config = config;
        _factory = factory;
        _contextFactory = contextFactory;
        _publishedContentQuery = publishedContentQuery;
        _cacheBuildContext = cacheBuildContext;
        _contentCache = contentCache;
        _logger = logger;
    }

    public void Initialize(bool isRestarting) => Initialize(isRestarting, scheduleRetry: true);

    private void Initialize(bool isRestarting, bool scheduleRetry)
    {
        lock (InitializationLock)
        {
            var readiness = _factory.GetRequiredService<StockReservationReadiness>();
            var wasInitialized = readiness.IsInitialized;
            readiness.Initializing();
            try
            {
                // A content or coupon change that lands mid-rebuild discards it, so try again a few times.
                for (var attempt = 1; ; attempt++)
                {
                    try
                    {
                        RebuildCaches(readiness);
                        break;
                    }
                    catch (CacheRebuildConflictException) when (attempt < MaxRebuildAttempts)
                    {
                        _logger.LogWarning(
                            "Ekom caches changed during rebuild attempt {Attempt} of {MaxAttempts}, retrying",
                            attempt,
                            MaxRebuildAttempts);
                    }
                }
            }
            catch (Exception ex)
            {
                if (wasInitialized) readiness.Initialized();
                _logger.LogError(ex, "Ekom cache initialization failed");
                // Startup must still allow a new installation to configure its store.
                // A requested rebuild must report failure to the backoffice caller.
                if (isRestarting) throw;

                // Nothing is live yet after a failed startup build, so content that kept changing
                // must not leave the catalog empty until someone rebuilds it by hand.
                if (scheduleRetry && !wasInitialized && ex is CacheRebuildConflictException)
                {
                    ScheduleStartupRetry();
                }
            }
        }
    }

    private static int _startupRetryScheduled;

    private void ScheduleStartupRetry()
    {
        if (Interlocked.Exchange(ref _startupRetryScheduled, 1) == 1)
        {
            return;
        }

        var scopeFactory = _factory.GetRequiredService<IServiceScopeFactory>();
        var readiness = _factory.GetRequiredService<StockReservationReadiness>();
        var logger = _logger;
        _ = Task.Run(async () =>
        {
            try
            {
                for (var attempt = 1; attempt <= MaxStartupRetries; attempt++)
                {
                    await Task.Delay(TimeSpan.FromSeconds(Math.Min(30, 2 * attempt))).ConfigureAwait(false);
                    if (readiness.IsInitialized)
                    {
                        return;
                    }

                    logger.LogInformation("Retrying the Ekom startup cache build, attempt {Attempt} of {MaxAttempts}", attempt, MaxStartupRetries);
                    using var scope = scopeFactory.CreateScope();
                    scope.ServiceProvider.GetRequiredService<EkomCacheInitializer>().Initialize(false, scheduleRetry: false);
                    if (readiness.IsInitialized)
                    {
                        return;
                    }
                }

                logger.LogError("Ekom caches are still empty after {MaxAttempts} startup retries. Rebuild them from the backoffice.", MaxStartupRetries);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Retrying the Ekom startup cache build failed");
            }
            finally
            {
                Volatile.Write(ref _startupRetryScheduled, 0);
            }
        });
    }

    private const int MaxStartupRetries = 30;

    private const int MaxRebuildAttempts = 3;

    private void RebuildCaches(StockReservationReadiness readiness)
    {
        using var rebuild = _config.CacheRebuild.Begin();
        ClearRebuildCaches();

        using var contextReference = _contextFactory.EnsureUmbracoContext();
        var rootNode = _publishedContentQuery.ContentAtRoot()
            .FirstOrDefault(x => x.IsDocumentType("ekom"));

        if (rootNode == null)
        {
            throw new InvalidOperationException("Ekom root node not found.");
        }

        var allNodes = rootNode.AncestorsOrSelf()
            .Concat(rootNode.Descendants())
            .ToList();
        using var cacheBuildScope = _cacheBuildContext.Begin(allNodes);
        using var cacheInitializationScope = CacheInitializationScope.Begin();
        using var priceCacheScope = PriceCache.BeginBulkInvalidation();

        _factory.GetRequiredService<Ekom.API.Prices>().FillCache();

        foreach (var cacheEntry in _config.CacheList.Value)
        {
            cacheEntry.FillCache();
        }

        var stockCache = _config.PerStoreStock
            ? _factory.GetService<IPerStoreCache<StockData>>()
            : _factory.GetService<IBaseCache<StockData>>() as ICache;

        stockCache?.FillCache();

        _factory.GetRequiredService<WarehouseStockCache>().FillCache();
        _factory.GetService<ICouponCache>()?.FillCache();
        rebuild.Commit();
        PriceCache.InvalidateAll();
        readiness.Initialized();
        _logger.LogInformation("Ekom cache rebuild published successfully");
    }

    // All dictionaries below resolve to the isolated rebuild context.
    private void ClearRebuildCaches()
    {
        _contentCache.Clear();

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
