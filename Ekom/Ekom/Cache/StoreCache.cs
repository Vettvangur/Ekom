using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Ekom.Cache;

class StoreCache : BaseCache<IStore>
{
    public override string NodeAlias { get; } = "ekmStore";

    public StoreCache(
        Configuration config,
        ILogger<BaseCache<IStore>> logger,
        IObjectFactory<IStore> objectFactory,
        IServiceProvider serviceProvider
    ) : base(config, logger, objectFactory, serviceProvider)
    {
    }

    protected override bool EnableIdIndex => true;
    protected override int GetId(IStore item) => item.Id;

    public override void FillCache()
    {
        var stopwatch = Stopwatch.StartNew();

        _logger.LogDebug("Starting to fill store cache...");
        int count = 0;

        IEnumerable<UmbracoContent> results = nodeService.NodesByTypes(NodeAlias);

        foreach (UmbracoContent r in results)
        {
            // If objectFactory is present, use it; otherwise create Store directly
            IStore item = _objFac?.Create(r) ?? new Store(r);

            count++;

            // IMPORTANT: use helper to keep indexes consistent
            AddOrReplaceFromCache(r.Key, item);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Finished filling store cache with {Count} items. Time it took to fill: {Elapsed}",
            count,
            stopwatch.Elapsed);
    }

    /// <summary>
    /// StoreCache-specific AddReplace triggers refill of succeeding per-store caches.
    /// </summary>
    public override void AddReplace(UmbracoContent node)
    {
        Store? item = (Store)(_objFac?.Create(node) ?? Activator.CreateInstance(typeof(Store), node));

        if (item == null)
            return;

        // IMPORTANT: use helper to keep indexes consistent
        AddOrReplaceFromCache(node.Key, item);

        IEnumerable<ICache> succeedingCaches = _config.Succeeding(this);

        // Refill all per-store caches for this store
        foreach (ICache cacheEntry in succeedingCaches)
        {
            if (cacheEntry is IPerStoreCache perStoreCache)
            {
                perStoreCache.FillCache(item);
            }
        }
    }
}
