using Ekom.Interfaces;
using Ekom.Models;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Ekom.Cache;

class CategoryCache : PerStoreCache<ICategory>
{
    public override string NodeAlias { get; } = "ekmCategory";

    // storeAlias -> parentId -> child keys
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentBag<Guid>>> _childrenIndex = new();

    // storeAlias -> categoryId -> descendant keys (includes all descendants)
    private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, ConcurrentBag<Guid>>> _descIndex = new();

    public CategoryCache(
        Configuration config,
        ILogger<IPerStoreIndexedCache<ICategory>> logger,
        IBaseCache<IStore> storeCache,
        IPerStoreFactory<ICategory> perStoreFactory,
        IServiceProvider serviceProvider
    ) : base(config, logger, storeCache, perStoreFactory, serviceProvider)
    {
    }

    protected override bool EnableIdIndex => true;
    protected override bool EnableRouteIndex => true;
    protected override IEnumerable<string> GetRoutes(ICategory item)
        => item.Urls ?? Enumerable.Empty<string>();

    protected override string NormalizeRoute(string route)
        => base.NormalizeRoute(route);
    protected override int GetId(ICategory item) => item.Id;

    protected override int FillStoreCache(
        IStore store,
        List<UmbracoContent> results,
        string nodeAlias,
        IReadOnlyDictionary<int, IReadOnlyList<UmbracoContent>>? ancestorsByNodeId)
    {
        var count = base.FillStoreCache(store, results, nodeAlias, ancestorsByNodeId);

        RebuildIndexes(store.Alias);

        return count;
    }

    public override void ClearCache()
    {
        base.ClearCache();
        _childrenIndex.Clear();
        _descIndex.Clear();
    }

    public override void AddOrReplaceFromCache(Guid key, Store store, ICategory item)
    {
        base.AddOrReplaceFromCache(key, store, item);
        RebuildIndexes(store.Alias);
    }

    public override bool RemoveItemFromCache(IStore store, Guid key)
    {
        var removed = RemoveItemFromCacheCore(store, key);
        if (removed)
            RebuildIndexes(store.Alias);

        return removed;
    }

    protected override void RemoveItemsFromCache(IStore store, IReadOnlyCollection<Guid> keys)
    {
        var removed = false;

        foreach (var key in keys)
        {
            removed |= RemoveItemFromCacheCore(store, key);
        }

        if (removed)
        {
            RebuildIndexes(store.Alias);
        }
    }

    private void RebuildIndexes(string storeAlias)
    {
        var children = new ConcurrentDictionary<int, ConcurrentBag<Guid>>();
        var desc = new ConcurrentDictionary<int, ConcurrentBag<Guid>>();

        if (Cache.TryGetValue(storeAlias, out var storeCache))
        {
            // children index
            foreach (var kv in storeCache)
            {
                var key = kv.Key;
                var c = kv.Value;

                if (c == null)
                    continue;

                children.GetOrAdd(c.ParentId, _ => new ConcurrentBag<Guid>()).Add(key);
            }

            // descendants index (using PathArray as you do today, but once per rebuild)
            // For each category, add itself as descendant to each ancestor id found in its PathArray.
            foreach (var kv in storeCache)
            {
                var key = kv.Key;
                var c = kv.Value;

                if (c == null)
                    continue;

                // PathArray contains string ids like "123"
                foreach (var pathIdStr in c.PathArray)
                {
                    if (int.TryParse(pathIdStr, out var ancestorId))
                    {
                        if (ancestorId == c.Id) continue; // exclude self if you want
                        desc.GetOrAdd(ancestorId, _ => new ConcurrentBag<Guid>()).Add(key);
                    }
                }
            }
        }

        _childrenIndex[storeAlias] = children;
        _descIndex[storeAlias] = desc;
    }

    public IEnumerable<ICategory> GetChildren(string storeAlias, int parentId)
    {
        if (!Cache.TryGetValue(storeAlias, out var storeCache))
            return Enumerable.Empty<ICategory>();

        if (!_childrenIndex.TryGetValue(storeAlias, out var idx) || !idx.TryGetValue(parentId, out var keys))
            return Enumerable.Empty<ICategory>();

        var list = new List<ICategory>();
        foreach (var key in keys)
        {
            if (storeCache.TryGetValue(key, out var c) && c != null)
                list.Add(c);
        }

        list.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return list;
    }

    public IEnumerable<ICategory> GetDescendants(string storeAlias, int categoryId)
    {
        if (!Cache.TryGetValue(storeAlias, out var storeCache))
            return Enumerable.Empty<ICategory>();

        if (!_descIndex.TryGetValue(storeAlias, out var idx) || !idx.TryGetValue(categoryId, out var keys))
            return Enumerable.Empty<ICategory>();

        var list = new List<ICategory>();
        foreach (var key in keys)
        {
            if (storeCache.TryGetValue(key, out var c) && c != null)
                list.Add(c);
        }

        list.Sort((a, b) => a.SortOrder.CompareTo(b.SortOrder));
        return list;
    }
}
