using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ekom.Cache;

/// <summary>
/// Per store caching for entities of generic type TItem
/// Supports optional secondary indexes per store:
///  - Id (int) -> Key (Guid)
///  - Sku (string) -> Key (Guid)
/// Primary store remains: storeAlias -> (Key Guid -> TItem)
/// </summary>
/// <typeparam name="TItem">Type of entity to cache</typeparam>
abstract class PerStoreCache<TItem> : ICache, IPerStoreCache, IPerStoreCache<TItem>, IPerStoreIndexedCache<TItem>
    where TItem : class
{
    protected readonly Configuration _config;
    protected readonly ILogger _logger;
    protected readonly IBaseCache<IStore> _storeCache;
    protected readonly IPerStoreFactory<TItem> _objFac;
    protected readonly IServiceProvider _serviceProvider;

    protected INodeService nodeService => _serviceProvider.GetService<INodeService>();

    protected PerStoreCache(
        Configuration config,
        ILogger<IPerStoreCache<TItem>> logger,
        IBaseCache<IStore> storeCache,
        IPerStoreFactory<TItem> objFac,
        IServiceProvider serviceProvider)
    {
        _config = config;
        _logger = logger;
        _storeCache = storeCache;
        _objFac = objFac;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Umbraco Node Alias
    /// </summary>
    public abstract string NodeAlias { get; }

    // -----------------------------
    // Primary per-store cache
    // -----------------------------

    /// <summary>
    /// Concurrent dictionaries per store: storeAlias -> (Key Guid -> TItem)
    /// </summary>
    public virtual ConcurrentDictionary<string, ConcurrentDictionary<Guid, TItem>> Cache { get; }
        = new ConcurrentDictionary<string, ConcurrentDictionary<Guid, TItem>>();

    /// <summary>
    /// Store cache indexer: returns the Key->Item dictionary for a store alias
    /// </summary>
    public ConcurrentDictionary<Guid, TItem> this[string storeAlias] => Cache[storeAlias];

    // -----------------------------
    // Optional secondary indexes
    // -----------------------------

    /// <summary>
    /// Opt-in Id index. Override to true for caches that support item.Id.
    /// </summary>
    protected virtual bool EnableIdIndex => false;

    /// <summary>
    /// Opt-in SKU index. Override to true for caches that support item.SKU.
    /// </summary>
    protected virtual bool EnableSkuIndex => false;

    /// <summary>
    /// Return the stable int Id for this item (only used if EnableIdIndex == true).
    /// </summary>
    protected virtual int GetId(TItem item) =>
        throw new NotSupportedException($"{GetType().Name} does not support Id indexing. Override EnableIdIndex/GetId.");

    /// <summary>
    /// Return the SKU for this item (only used if EnableSkuIndex == true).
    /// </summary>
    protected virtual string? GetSku(TItem item) => null;

    /// <summary>
    /// Normalize SKU before storing/looking up in SKU index.
    /// </summary>
    protected virtual string NormalizeSku(string sku) => sku.Trim();

    /// <summary>
    /// SKU comparer for dictionary (default case-insensitive).
    /// </summary>
    protected virtual StringComparer SkuComparer => StringComparer.OrdinalIgnoreCase;

    /// <summary>
    /// storeAlias -> (Id -> Key)
    /// </summary>
    protected virtual ConcurrentDictionary<string, ConcurrentDictionary<int, Guid>> IdIndex { get; }
        = new ConcurrentDictionary<string, ConcurrentDictionary<int, Guid>>();

    /// <summary>
    /// storeAlias -> (Sku -> Key)
    /// </summary>
    protected virtual ConcurrentDictionary<string, ConcurrentDictionary<string, Guid>> SkuIndex { get; }
        = new ConcurrentDictionary<string, ConcurrentDictionary<string, Guid>>();

    private ConcurrentDictionary<Guid, TItem> GetStoreCache(string alias) =>
        Cache.GetOrAdd(alias, _ => new ConcurrentDictionary<Guid, TItem>());

    private ConcurrentDictionary<int, Guid> GetStoreIdIndex(string alias) =>
        IdIndex.GetOrAdd(alias, _ => new ConcurrentDictionary<int, Guid>());

    private ConcurrentDictionary<string, Guid> GetStoreSkuIndex(string alias) =>
        SkuIndex.GetOrAdd(alias, _ => new ConcurrentDictionary<string, Guid>(SkuComparer));

    // -----------------------------
    // Fill
    // -----------------------------

    public virtual void FillCache() => FillCache(null);

    /// <summary>
    /// Base Fill cache method appropriate for most derived caches
    /// </summary>
    /// <param name="storeParam">
    /// This parameter is supplied when adding a store at runtime,
    /// triggering the given store's filling
    /// </param>
    public virtual void FillCache(IStore? storeParam = null)
    {
        if (string.IsNullOrWhiteSpace(NodeAlias))
        {
            _logger.LogError("No NodeAlias, can not fill cache.");
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Starting to fill per store cache for {NodeAlias}...", NodeAlias);

        int count = 0;

        try
        {
            List<UmbracoContent> results = nodeService.NodesByTypes(NodeAlias).ToList();
            _logger.LogInformation("Filling per store cache for {NodeAlias}... Nodes: {Count}", NodeAlias, results.Count);

            if (storeParam == null) // Startup initialization
            {
                foreach (IStore store in _storeCache.Cache.Select(x => x.Value))
                {
                    count += FillStoreCache(store, results, NodeAlias);
                }
            }
            else // Triggered with dynamic addition/removal of store
            {
                count += FillStoreCache(storeParam, results, NodeAlias);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Filling per store cache failed for {NodeAlias}!", NodeAlias);
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Finished filling per store cache with {Count} items for {NodeAlias}. Time it took to fill: {Elapsed}",
            count,
            NodeAlias,
            stopwatch.Elapsed
        );
    }

    /// <summary>
    /// Fill the given store's cache of TItem (and optional indexes).
    /// </summary>
    protected virtual int FillStoreCache(IStore store, List<UmbracoContent> results, string nodeAlias)
    {
        int count = 0;

        // Replace whole store cache (fast + consistent)
        var curStoreCache = Cache[store.Alias] = new ConcurrentDictionary<Guid, TItem>();

        ConcurrentDictionary<int, Guid>? curIdIndex = null;
        ConcurrentDictionary<string, Guid>? curSkuIndex = null;

        if (EnableIdIndex)
            curIdIndex = IdIndex[store.Alias] = new ConcurrentDictionary<int, Guid>();

        if (EnableSkuIndex)
            curSkuIndex = SkuIndex[store.Alias] = new ConcurrentDictionary<string, Guid>(SkuComparer);

        LoopTimer timer = new LoopTimer(results.Count, _logger, nodeAlias);

        foreach (UmbracoContent r in results)
        {
            timer.StartIteration();

            try
            {
                if (r.IsItemDisabled(store))
                    continue;

                TItem? item = _objFac?.Create(r, store)
                              ?? (TItem)Activator.CreateInstance(typeof(TItem), r, store);

                if (item == null)
                    continue;

                count++;

                // Primary cache
                curStoreCache[r.Key] = item;

                // Optional Id index
                if (EnableIdIndex)
                {
                    var id = GetId(item);
                    curIdIndex![id] = r.Key;
                }

                // Optional SKU index
                if (EnableSkuIndex)
                {
                    var sku = GetSku(item);
                    if (!string.IsNullOrWhiteSpace(sku))
                        curSkuIndex![NormalizeSku(sku)] = r.Key;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error on adding item with id: {Id} in Store: {StoreAlias} to cache.",
                    r.Id,
                    store.Alias
                );
            }

            timer.EndIteration();
        }

        return count;
    }

    // -----------------------------
    // Event updates
    // -----------------------------

    /// <summary>
    /// Add/replace an item in a single store cache + indexes.
    /// Use this when you already know the store.
    /// </summary>
    public virtual void AddOrReplaceFromCache(Guid key, Store store, TItem item)
    {
        var alias = store.Alias;

        var storeCache = GetStoreCache(alias);

        // If item already exists, remove stale index entries (Id/Sku might have changed)
        if (storeCache.TryGetValue(key, out var old))
        {
            if (EnableIdIndex)
                GetStoreIdIndex(alias).TryRemove(GetId(old), out _);

            if (EnableSkuIndex)
            {
                var oldSku = GetSku(old);
                if (!string.IsNullOrWhiteSpace(oldSku))
                    GetStoreSkuIndex(alias).TryRemove(NormalizeSku(oldSku), out _);
            }
        }

        storeCache[key] = item;

        if (EnableIdIndex)
            GetStoreIdIndex(alias)[GetId(item)] = key;

        if (EnableSkuIndex)
        {
            var sku = GetSku(item);
            if (!string.IsNullOrWhiteSpace(sku))
                GetStoreSkuIndex(alias)[NormalizeSku(sku)] = key;
        }
    }

    /// <summary>
    /// Remove an item from a single store cache + indexes.
    /// </summary>
    public virtual bool RemoveItemFromCache(IStore store, Guid key)
    {
        var alias = store.Alias;

        if (!GetStoreCache(alias).TryRemove(key, out var item))
            return false;

        if (EnableIdIndex)
            GetStoreIdIndex(alias).TryRemove(GetId(item), out _);

        if (EnableSkuIndex)
        {
            var sku = GetSku(item);
            if (!string.IsNullOrWhiteSpace(sku))
                GetStoreSkuIndex(alias).TryRemove(NormalizeSku(sku), out _);
        }

        return true;
    }

    /// <summary>
    /// Adds or replaces an item from all store caches (based on disabled rules).
    /// Also maintains optional Id/SKU indexes.
    /// </summary>
    public void AddOrReplaceFromAllCaches(UmbracoContent node)
    {
        IEnumerable<UmbracoContent> ancestors = nodeService.NodeAncestors(node.Id.ToString());

        foreach (KeyValuePair<Guid, IStore> store in _storeCache.Cache)
        {
            var alias = store.Value.Alias;

            try
            {
                bool isDisabled = node.IsItemDisabled(store.Value, ancestors);

                if (isDisabled)
                {
                    // remove from primary + indexes
                    RemoveItemFromCache(store.Value, node.Key);
                    continue;
                }

                TItem? item = _objFac?.Create(node, store.Value)
                             ?? (TItem)Activator.CreateInstance(typeof(TItem), node, store.Value);

                if (item != null)
                {
                    // Use the unified method that also handles stale indexes
                    AddOrReplaceFromCache(node.Key, (Store)store.Value, item);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error on adding item with id: {Id} in Store: {StoreAlias}",
                    node.Id,
                    alias
                );
            }
        }
    }

    /// <summary>
    /// Removes an item from all store caches + indexes.
    /// </summary>
    public void RemoveItemFromAllCaches(Guid key)
    {
        foreach (KeyValuePair<Guid, IStore> store in _storeCache.Cache)
        {
            RemoveItemFromCache(store.Value, key);
        }
    }

    // -----------------------------
    // ICache implementation
    // -----------------------------

    public virtual void AddReplace(UmbracoContent node) => AddOrReplaceFromAllCaches(node);

    public virtual void Remove(Guid id) => RemoveItemFromAllCaches(id);

    // -----------------------------
    // Lookups (O(1) when enabled)
    // -----------------------------

    public bool TryGetByKey(string storeAlias, Guid key, out TItem? item)
    {
        item = null;
        return Cache.TryGetValue(storeAlias, out var d) && d.TryGetValue(key, out item);
    }

    public bool TryGetById(string storeAlias, int id, out TItem? item)
    {
        item = null;
        if (!EnableIdIndex) return false;

        return IdIndex.TryGetValue(storeAlias, out var idx)
            && idx.TryGetValue(id, out var key)
            && Cache.TryGetValue(storeAlias, out var d)
            && d.TryGetValue(key, out item);
    }

    public bool TryGetBySku(string storeAlias, string sku, out TItem? item)
    {
        item = null;
        if (!EnableSkuIndex || string.IsNullOrWhiteSpace(sku)) return false;

        var norm = NormalizeSku(sku);

        return SkuIndex.TryGetValue(storeAlias, out var idx)
            && idx.TryGetValue(norm, out var key)
            && Cache.TryGetValue(storeAlias, out var d)
            && d.TryGetValue(key, out item);
    }
}
