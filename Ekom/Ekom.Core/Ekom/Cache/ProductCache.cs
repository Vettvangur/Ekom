using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Ekom.Cache;

class ProductCache : PerStoreCache<IProduct>
{
    public override string NodeAlias { get; } = "ekmProduct";

    // storeAlias -> categoryId -> (productKey -> dummy)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<Guid, byte>>> _categoryIndex
        = new();

    public ProductCache(
        Configuration config,
        ILogger<IPerStoreIndexedCache<IProduct>> logger,
        IBaseCache<IStore> storeCache,
        IPerStoreFactory<IProduct> perStoreFactory,
        IServiceProvider serviceProvider
    )
        : base(config, logger, storeCache, perStoreFactory, serviceProvider)
    {
    }

    protected override bool EnableIdIndex => true;
    protected override bool EnableSkuIndex => true;
    protected override bool EnableRouteIndex => true;
    protected override int GetId(IProduct item) => item.Id;
    protected override string? GetSku(IProduct item) => item.SKU;
    protected override IEnumerable<string> GetRoutes(IProduct item)
        => item.Urls ?? Enumerable.Empty<string>();

    // optional: if you want different normalization for products
    protected override string NormalizeRoute(string route)
        => base.NormalizeRoute(route);

    protected override string NormalizeSku(string sku) => sku.Trim();

    /// <summary>
    /// Rebuild store cache + Id/Sku indexes (base) AND rebuild our category index for that store.
    /// </summary>
    protected override int FillStoreCache(
        IStore store,
        List<UmbracoContent> results,
        string nodeAlias,
        IReadOnlyDictionary<int, IReadOnlyList<UmbracoContent>>? ancestorsByNodeId)
    {
        var count = base.FillStoreCache(store, results, nodeAlias, ancestorsByNodeId);

        var idx = new ConcurrentDictionary<int, ConcurrentDictionary<Guid, byte>>();

        if (Cache.TryGetValue(store.Alias, out var storeCache))
        {
            foreach (var (key, p) in storeCache)
            {
                if (p?.Categories == null) continue;

                foreach (var c in p.Categories)
                    idx.GetOrAdd(c.Id, _ => new ConcurrentDictionary<Guid, byte>())[key] = 0;
            }
        }

        _categoryIndex[store.Alias] = idx;
        return count;
    }

    public override void ClearCache()
    {
        base.ClearCache();
        _categoryIndex.Clear();
    }

    public override void AddOrReplaceFromCache(Guid key, Store store, IProduct item)
    {
        var alias = store.Alias;

        // 1) Remove stale category mappings (based on old item if present)
        if (Cache.TryGetValue(alias, out var storeCache) && storeCache.TryGetValue(key, out var old) && old != null)
            RemoveFromCategoryIndex(alias, key, old);

        // 2) Let base update primary cache + id/sku indexes
        base.AddOrReplaceFromCache(key, store, item);

        // 3) Add new category mappings
        AddToCategoryIndex(alias, key, item);
    }

    private void AddToCategoryIndex(string storeAlias, Guid productKey, IProduct product)
    {
        if (product.Categories == null) return;

        var storeIdx = _categoryIndex.GetOrAdd(storeAlias,
            _ => new ConcurrentDictionary<int, ConcurrentDictionary<Guid, byte>>());

        foreach (var c in product.Categories)
            storeIdx.GetOrAdd(c.Id, _ => new ConcurrentDictionary<Guid, byte>())[productKey] = 0;
    }

    private void RemoveFromCategoryIndex(string storeAlias, Guid productKey, IProduct product)
    {
        if (product.Categories == null) return;

        if (!_categoryIndex.TryGetValue(storeAlias, out var storeIdx))
            return;

        foreach (var c in product.Categories)
        {
            if (storeIdx.TryGetValue(c.Id, out var set))
                set.TryRemove(productKey, out _);
        }
    }

    public IEnumerable<IProduct> GetByCategoryAndDescendants(string storeAlias, int rootCategoryId, IEnumerable<int> descendantCategoryIds)
    {
        if (!Cache.TryGetValue(storeAlias, out var storeCache))
            return Enumerable.Empty<IProduct>();

        if (!_categoryIndex.TryGetValue(storeAlias, out var idx))
            return Enumerable.Empty<IProduct>();

        var keys = new HashSet<Guid>();

        // root category
        if (idx.TryGetValue(rootCategoryId, out var rootSet))
            foreach (var key in rootSet.Keys) keys.Add(key);

        // descendants
        foreach (var catId in descendantCategoryIds)
        {
            if (!idx.TryGetValue(catId, out var set))
                continue;

            foreach (var key in set.Keys)
                keys.Add(key);
        }

        var list = new List<IProduct>(keys.Count);
        foreach (var key in keys)
            if (storeCache.TryGetValue(key, out var p) && p != null)
                list.Add(p);

        return list;
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
            if (!idx.TryGetValue(catId, out var set))
                continue;

            foreach (var key in set.Keys)
                keys.Add(key);
        }

        var list = new List<IProduct>(keys.Count);
        foreach (var key in keys)
            if (storeCache.TryGetValue(key, out var p) && p != null)
                list.Add(p);

        return list;
    }

    public bool HasAnyInCategory(string storeAlias, int categoryId)
    {
        return _categoryIndex.TryGetValue(storeAlias, out var storeIdx)
            && storeIdx.TryGetValue(categoryId, out var set)
            && !set.IsEmpty;
    }

}
