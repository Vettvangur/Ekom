using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Ekom.Cache;

class VariantGroupCache : PerStoreCache<IVariantGroup>
{
    public override string NodeAlias { get; } = "ekmProductVariantGroup";

    // storeAlias -> (ProductId -> group keys)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentDictionary<Guid, byte>>> _productIndex
        = new();

    private ConcurrentDictionary<int, ConcurrentDictionary<Guid, byte>> GetStoreProductIndex(string storeAlias) =>
        _productIndex.GetOrAdd(storeAlias, _ => new ConcurrentDictionary<int, ConcurrentDictionary<Guid, byte>>());

    public VariantGroupCache(
        Configuration config,
        ILogger<IPerStoreIndexedCache<IVariantGroup>> logger,
        IBaseCache<IStore> storeCache,
        IPerStoreFactory<IVariantGroup> perStoreFactory,
        IServiceProvider serviceProvider
    ) : base(config, logger, storeCache, perStoreFactory, serviceProvider)
    {
    }

    protected override bool EnableIdIndex => true;
    protected override int GetId(IVariantGroup item) => item.Id;

    protected override int FillStoreCache(IStore store, List<UmbracoContent> results, string nodeAlias)
    {
        var count = base.FillStoreCache(store, results, nodeAlias);

        var pi = new ConcurrentDictionary<int, ConcurrentDictionary<Guid, byte>>();

        if (Cache.TryGetValue(store.Alias, out var storeCache))
        {
            foreach (var kv in storeCache)
            {
                var key = kv.Key;
                var g = kv.Value;

                pi.GetOrAdd(g.ProductId, _ => new ConcurrentDictionary<Guid, byte>()).TryAdd(key, 0);
            }
        }

        _productIndex[store.Alias] = pi;
        return count;
    }

    public IEnumerable<IVariantGroup> GetByProductId(string storeAlias, int productId)
    {
        if (!Cache.TryGetValue(storeAlias, out var storeCache))
            return Enumerable.Empty<IVariantGroup>();

        if (!_productIndex.TryGetValue(storeAlias, out var pi) || !pi.TryGetValue(productId, out var keys))
            return Enumerable.Empty<IVariantGroup>();

        var list = new List<IVariantGroup>();
        foreach (var key in keys.Keys)
        {
            if (storeCache.TryGetValue(key, out var g) && g != null && g.ProductId == productId)
                list.Add(g);
        }

        list.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return list;
    }

    // Event-driven updates (lazy-cleaned, same approach as VariantCache)
    public override void AddOrReplaceFromCache(Guid key, Store store, IVariantGroup item)
    {
        base.AddOrReplaceFromCache(key, store, item);

        GetStoreProductIndex(store.Alias)
            .GetOrAdd(item.ProductId, _ => new ConcurrentDictionary<Guid, byte>())
            .TryAdd(key, 0);
    }

    public override bool RemoveItemFromCache(IStore store, Guid key)
    {
        return base.RemoveItemFromCache(store, key);
    }
}
