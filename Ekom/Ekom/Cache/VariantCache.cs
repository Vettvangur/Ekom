using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Ekom.Cache;

class VariantCache : PerStoreCache<IVariant>
{
    public override string NodeAlias { get; } = "ekmProductVariant";

    public VariantCache(
        Configuration config,
        ILogger<IPerStoreIndexedCache<IVariant>> logger,
        IBaseCache<IStore> storeCache,
        IPerStoreFactory<IVariant> perStoreFactory,
        IServiceProvider serviceProvider
    ) : base(config, logger, storeCache, perStoreFactory, serviceProvider)
    {
    }

    protected override bool EnableIdIndex => true;
    protected override bool EnableSkuIndex => true;

    protected override int GetId(IVariant item) => item.Id;
    protected override string? GetSku(IVariant item) => item.SKU;
    protected override string NormalizeSku(string sku) => sku.Trim();

    // -----------------------------
    // Group index: storeAlias -> (VariantGroupId -> variant keys)
    // -----------------------------
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentBag<Guid>>> _groupIndex
        = new();

    private ConcurrentDictionary<int, ConcurrentBag<Guid>> GetStoreGroupIndex(string storeAlias) =>
        _groupIndex.GetOrAdd(storeAlias, _ => new ConcurrentDictionary<int, ConcurrentBag<Guid>>());

    // -----------------------------
    // Product index: storeAlias -> (ProductId -> variant keys)
    // -----------------------------
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentBag<Guid>>> _productIndex
        = new();

    private ConcurrentDictionary<int, ConcurrentBag<Guid>> GetStoreProductIndex(string storeAlias) =>
        _productIndex.GetOrAdd(storeAlias, _ => new ConcurrentDictionary<int, ConcurrentBag<Guid>>());

    protected override int FillStoreCache(IStore store, List<UmbracoContent> results, string nodeAlias)
    {
        // base builds: Cache[store], IdIndex, SkuIndex
        var count = base.FillStoreCache(store, results, nodeAlias);

        // rebuild group + product indexes for the store from the freshly built store cache
        var gi = new ConcurrentDictionary<int, ConcurrentBag<Guid>>();
        var pi = new ConcurrentDictionary<int, ConcurrentBag<Guid>>();

        if (Cache.TryGetValue(store.Alias, out var storeCache))
        {
            foreach (var kv in storeCache)
            {
                var key = kv.Key;
                var v = kv.Value;

                gi.GetOrAdd(v.VariantGroupId, _ => new ConcurrentBag<Guid>()).Add(key);
                pi.GetOrAdd(v.ProductId, _ => new ConcurrentBag<Guid>()).Add(key);
            }
        }

        _groupIndex[store.Alias] = gi;
        _productIndex[store.Alias] = pi;

        return count;
    }

    public IEnumerable<IVariant> GetByGroup(string storeAlias, int groupId)
    {
        if (!Cache.TryGetValue(storeAlias, out var storeCache))
            return Enumerable.Empty<IVariant>();

        if (!_groupIndex.TryGetValue(storeAlias, out var gi) || !gi.TryGetValue(groupId, out var keys))
            return Enumerable.Empty<IVariant>();

        var list = new List<IVariant>();
        foreach (var key in keys)
        {
            if (storeCache.TryGetValue(key, out var v) && v != null && v.VariantGroupId == groupId)
                list.Add(v);
        }

        list.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return list;
    }

    public IEnumerable<IVariant> GetByProductId(string storeAlias, int productId)
    {
        if (!Cache.TryGetValue(storeAlias, out var storeCache))
            return Enumerable.Empty<IVariant>();

        if (!_productIndex.TryGetValue(storeAlias, out var pi) || !pi.TryGetValue(productId, out var keys))
            return Enumerable.Empty<IVariant>();

        var list = new List<IVariant>();
        foreach (var key in keys)
        {
            if (storeCache.TryGetValue(key, out var v) && v != null && v.ProductId == productId)
                list.Add(v);
        }

        list.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return list;
    }

    // Keep indexes consistent for event-driven updates too
    public override void AddOrReplaceFromCache(Guid key, Store store, IVariant item)
    {
        base.AddOrReplaceFromCache(key, store, item);

        GetStoreGroupIndex(store.Alias)
            .GetOrAdd(item.VariantGroupId, _ => new ConcurrentBag<Guid>())
            .Add(key);

        GetStoreProductIndex(store.Alias)
            .GetOrAdd(item.ProductId, _ => new ConcurrentBag<Guid>())
            .Add(key);
    }

    public override bool RemoveItemFromCache(IStore store, Guid key)
    {
        // We remove from primary cache; indexes are “lazy-cleaned” because getters verify:
        // storeCache.TryGetValue(key, out v) && v.Group/ProductId matches.
        return base.RemoveItemFromCache(store, key);
    }
}
