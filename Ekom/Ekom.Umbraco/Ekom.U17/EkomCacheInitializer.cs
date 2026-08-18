using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Umb.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core;
using Umbraco.Cms.Core.Web;
using Umbraco.Extensions;

namespace Ekom.Umb;

internal sealed class EkomCacheInitializer
{
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

    public void Initialize(bool isRestarting)
    {
        try
        {
            if (isRestarting)
            {
                ClearCaches();
            }

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

    private void ClearCaches()
    {
        _contentCache.Clear();
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
