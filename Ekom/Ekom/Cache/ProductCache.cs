using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Ekom.Cache;

class ProductCache : PerStoreCache<IProduct>
{
    public override string NodeAlias { get; } = "ekmProduct";

    // storeAlias -> (categoryId -> product keys)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentBag<Guid>>> _categoryIndex
        = new();

    public ProductCache(
        Configuration config,
        ILogger<IPerStoreCache<IProduct>> logger,
        IBaseCache<IStore> storeCache,
        IPerStoreFactory<IProduct> perStoreFactory,
        IServiceProvider serviceProvider
    )
        : base(config, logger, storeCache, perStoreFactory, serviceProvider)
    {
    }

    protected override bool EnableIdIndex => true;
    protected override bool EnableSkuIndex => true;
    protected override int GetId(IProduct item) => item.Id;
    protected override string? GetSku(IProduct item) => item.SKU;
    protected override string NormalizeSku(string sku) => sku.Trim();

    /// <summary>
    /// Rebuild store cache + Id/Sku indexes (base) AND rebuild our category index for that store.
    /// </summary>
    protected override int FillStoreCache(IStore store, List<UmbracoContent> results, string nodeAlias)
    {
        // base builds: Cache[storeAlias], IdIndex[storeAlias], SkuIndex[storeAlias]
        var count = base.FillStoreCache(store, results, nodeAlias);

        // rebuild category index for this store
        var idx = new ConcurrentDictionary<int, ConcurrentBag<Guid>>();

        if (Cache.TryGetValue(store.Alias, out var storeCache))
        {
            foreach (var kv in storeCache)
            {
                var key = kv.Key;
                var p = kv.Value;

                if (p.Categories == null) continue;

                foreach (var c in p.Categories)
                {
                    idx.GetOrAdd(c.Id, _ => new ConcurrentBag<Guid>()).Add(key);
                }
            }
        }

        _categoryIndex[store.Alias] = idx;

        return count;
    }

    /// <summary>
    /// Returns products belonging to ANY of the given category ids.
    /// Dedupes by product key.
    /// </summary>
    public IEnumerable<IProduct> GetByAnyCategoryIds(string storeAlias, IEnumerable<int> categoryIds)
    {
        if (!Cache.TryGetValue(storeAlias, out var storeCache))
            return Enumerable.Empty<IProduct>();

        if (!_categoryIndex.TryGetValue(storeAlias, out var idx))
            return Enumerable.Empty<IProduct>();

        var keys = new HashSet<Guid>();

        foreach (var catId in categoryIds)
        {
            if (!idx.TryGetValue(catId, out var bag))
                continue;

            foreach (var key in bag)
                keys.Add(key);
        }

        var list = new List<IProduct>(keys.Count);
        foreach (var key in keys)
        {
            if (storeCache.TryGetValue(key, out var p) && p != null)
                list.Add(p);
        }

        return list;
    }

    public bool HasAnyInCategory(string storeAlias, int categoryId)
    {
        return _categoryIndex.TryGetValue(storeAlias, out var storeIdx)
            && storeIdx.TryGetValue(categoryId, out var set)
            && set.Count > 0;
    }

}
