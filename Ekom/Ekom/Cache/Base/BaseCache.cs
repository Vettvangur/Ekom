using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace Ekom.Cache;

/// <summary>
/// For custom caches or global non store dependant caches
/// Supports optional secondary indexes:
///  - Id (int) -> Key (Guid)
///  - Sku (string) -> Key (Guid)
/// Primary cache remains: Key(Guid) -> TItem
/// </summary>
abstract class BaseCache<TItem> : ICache, IBaseCache<TItem>
    where TItem : class
{
    protected readonly Configuration _config;
    protected readonly ILogger _logger;
    protected readonly IObjectFactory<TItem>? _objFac;
    protected readonly IServiceProvider _serviceProvider;

    protected INodeService nodeService => _serviceProvider.GetService<INodeService>();

    protected BaseCache(
        Configuration config,
        ILogger<BaseCache<TItem>> logger,
        IObjectFactory<TItem>? objectFactory,
        IServiceProvider serviceProvider)
    {
        _config = config;
        _logger = logger;
        _objFac = objectFactory;
        _serviceProvider = serviceProvider;
    }

    /// <summary>
    /// Umbraco Node Alias
    /// </summary>
    public abstract string NodeAlias { get; }

    // -----------------------------
    // Primary cache
    // -----------------------------

    public virtual ConcurrentDictionary<Guid, TItem> Cache { get; }
        = new ConcurrentDictionary<Guid, TItem>();

    public TItem this[Guid index]
    {
        get => Cache[index];
        set => Cache[index] = value;
    }

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

    protected virtual ConcurrentDictionary<int, Guid> IdIndex { get; } = new ConcurrentDictionary<int, Guid>();
    protected virtual ConcurrentDictionary<string, Guid> SkuIndex { get; } =
        new ConcurrentDictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

    // -----------------------------
    // Internal helpers (index-safe)
    // -----------------------------

    protected void AddOrReplaceFromCache(Guid key, TItem item)
    {
        // remove stale indexes if we are replacing an existing item
        if (Cache.TryGetValue(key, out var old))
        {
            if (EnableIdIndex)
                IdIndex.TryRemove(GetId(old), out _);

            if (EnableSkuIndex)
            {
                var oldSku = GetSku(old);
                if (!string.IsNullOrWhiteSpace(oldSku))
                    SkuIndex.TryRemove(NormalizeSku(oldSku), out _);
            }
        }

        Cache[key] = item;

        if (EnableIdIndex)
            IdIndex[GetId(item)] = key;

        if (EnableSkuIndex)
        {
            var sku = GetSku(item);
            if (!string.IsNullOrWhiteSpace(sku))
                SkuIndex[NormalizeSku(sku)] = key;
        }
    }

    protected void RemoveItemFromCache(Guid key)
    {
        if (!Cache.TryRemove(key, out var item))
            return;

        if (EnableIdIndex)
            IdIndex.TryRemove(GetId(item), out _);

        if (EnableSkuIndex)
        {
            var sku = GetSku(item);
            if (!string.IsNullOrWhiteSpace(sku))
                SkuIndex.TryRemove(NormalizeSku(sku), out _);
        }
    }

    // -----------------------------
    // Lookups (O(1) when enabled)
    // -----------------------------

    public bool TryGetByKey(Guid key, out TItem? item) => Cache.TryGetValue(key, out item);

    public bool TryGetById(int id, out TItem? item)
    {
        item = null;
        if (!EnableIdIndex) return false;

        return IdIndex.TryGetValue(id, out var key) && Cache.TryGetValue(key, out item);
    }

    public bool TryGetBySku(string sku, out TItem? item)
    {
        item = null;
        if (!EnableSkuIndex || string.IsNullOrWhiteSpace(sku)) return false;

        var norm = NormalizeSku(sku);
        return SkuIndex.TryGetValue(norm, out var key) && Cache.TryGetValue(key, out item);
    }

    // -----------------------------
    // Fill
    // -----------------------------

    public virtual void FillCache()
    {
        if (string.IsNullOrWhiteSpace(NodeAlias))
        {
            _logger.LogDebug("BaseCache<{Type}> FillCache skipped (NodeAlias is empty).", typeof(TItem).Name);
            return;
        }

        var stopwatch = Stopwatch.StartNew();
        _logger.LogDebug("Starting to fill base cache for {NodeAlias}...", NodeAlias);

        int count = 0;
        IEnumerable<UmbracoContent> results = nodeService.NodesByTypes(NodeAlias);

        foreach (UmbracoContent r in results)
        {
            try
            {
                TItem? item =
                    (TItem?)(_objFac?.Create(r) ?? Activator.CreateInstance(typeof(TItem), r));

                if (item == null)
                    continue;

                count++;
                AddOrReplaceFromCache(r.Key, item);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to map item. Id: {Id}", r.Id);
            }
        }

        stopwatch.Stop();
        _logger.LogInformation(
            "Finished filling base cache with {Count} items for {NodeAlias}. Time it took: {Elapsed}",
            count,
            NodeAlias,
            stopwatch.Elapsed
        );
    }

    // -----------------------------
    // ICache implementation
    // -----------------------------

    public virtual void AddReplace(UmbracoContent content)
    {
        if (!nodeService.IsItemUnpublished(content))
        {
            TItem? item =
                (TItem?)(_objFac?.Create(content) ?? Activator.CreateInstance(typeof(TItem), content));

            if (item != null)
                AddOrReplaceFromCache(content.Key, item);
        }
    }

    public virtual void Remove(Guid id) => RemoveItemFromCache(id);
}
