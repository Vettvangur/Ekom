using Ekom.Cache;
using Ekom.Events;
using Ekom.Models;
using LinqToDB;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;

namespace Ekom.Services;

/// <summary>Best-effort post-commit notification. Failure must never replay a stock mutation.</summary>
internal sealed class StockChangePublisher : IDisposable
{
    private readonly DatabaseFactory _database;
    private readonly Configuration _config;
    private readonly IBaseCache<StockData> _stock;
    private readonly IPerStoreCache<StockData> _perStore;
    private readonly ILogger<StockChangePublisher> _logger;
    private readonly SemaphoreSlim _refreshLock = new(1, 1);

    public StockChangePublisher(DatabaseFactory database, Configuration config,
        IBaseCache<StockData> stock, IPerStoreCache<StockData> perStore, ILogger<StockChangePublisher> logger)
    {
        _database = database;
        _config = config;
        _stock = stock;
        _perStore = perStore;
        _logger = logger;
    }

    public async Task PublishAsync(Guid key, string? storeAlias, string uniqueId, decimal oldValue, decimal newValue, CancellationToken ct = default)
    {
        // The row is already committed. A cache rebuild running right now may have read stock before
        // that, so instead of discarding the rebuild the row is read again once it is published.
        using var cacheUpdate = _config.CacheRebuild.UpdateRefreshable(
            uniqueId,
            () => RefreshAfterRebuildAsync(key, storeAlias, uniqueId));
        try
        {
            if (await RefreshCacheAsync(key, storeAlias, uniqueId, ct).ConfigureAwait(false) == null) return;

            // No-op requests still refresh a potentially stale cache, without emitting a change.
            if (oldValue == newValue) return;

            await StockEvents.OnStockChangedAsync(this, new StockChangedEventArgs
            {
                Key = key, StoreAlias = storeAlias, OldValue = oldValue, NewValue = newValue,
            }, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stock {StockId} committed, but cache/event publication failed", uniqueId);
        }
    }

    /// <summary>Batch form of <see cref="PublishAsync"/>: one read refreshes the cache for every committed row.</summary>
    public async Task PublishManyAsync(
        IReadOnlyList<(Guid Key, string? StoreAlias, string UniqueId, decimal OldValue, decimal NewValue)> changes,
        CancellationToken ct = default)
    {
        if (changes.Count == 0) return;

        var cacheUpdates = new List<IDisposable>(changes.Count);
        try
        {
            foreach (var change in changes)
            {
                cacheUpdates.Add(_config.CacheRebuild.UpdateRefreshable(
                    change.UniqueId,
                    () => RefreshAfterRebuildAsync(change.Key, change.StoreAlias, change.UniqueId)));
            }

            var ids = changes.Select(x => x.UniqueId).ToArray();
            var current = new Dictionary<string, StockData>(StringComparer.OrdinalIgnoreCase);
            await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await using var db = _database.GetDatabase();
                var rows = await db.StockData.Where(x => Enumerable.Contains(ids, x.UniqueId)).ToListAsync(ct).ConfigureAwait(false);
                foreach (var row in rows) current[row.UniqueId] = row;

                foreach (var change in changes)
                {
                    if (!current.TryGetValue(change.UniqueId, out var row)) continue;
                    if (_config.PerStoreStock && change.StoreAlias != null)
                        _perStore.Cache.GetOrAdd(change.StoreAlias, _ => new ConcurrentDictionary<Guid, StockData>())[change.Key] = row;
                    else if (!_config.PerStoreStock && change.StoreAlias == null)
                        _stock.Cache[change.Key] = row;
                }
            }
            finally { _refreshLock.Release(); }

            foreach (var change in changes)
            {
                // No-op requests still refresh a potentially stale cache, without emitting a change.
                if (change.OldValue == change.NewValue || !current.ContainsKey(change.UniqueId)) continue;

                try
                {
                    await StockEvents.OnStockChangedAsync(this, new StockChangedEventArgs
                    {
                        Key = change.Key, StoreAlias = change.StoreAlias, OldValue = change.OldValue, NewValue = change.NewValue,
                    }, ct).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Stock {StockId} committed, but event publication failed", change.UniqueId);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "{Count} stock values committed, but cache/event publication failed", changes.Count);
        }
        finally
        {
            for (var i = cacheUpdates.Count - 1; i >= 0; i--) cacheUpdates[i].Dispose();
        }
    }

    /// <summary>Reads the committed row and puts it in the live cache. Never changes stock.</summary>
    private async Task<StockData?> RefreshCacheAsync(Guid key, string? storeAlias, string uniqueId, CancellationToken ct)
    {
        await _refreshLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await using var db = _database.GetDatabase();
            var current = await db.StockData.FirstOrDefaultAsync(x => x.UniqueId == uniqueId, ct).ConfigureAwait(false);
            if (current == null) return null;
            if (_config.PerStoreStock && storeAlias != null)
                _perStore.Cache.GetOrAdd(storeAlias, _ => new ConcurrentDictionary<Guid, StockData>())[key] = current;
            else if (!_config.PerStoreStock && storeAlias == null)
                _stock.Cache[key] = current;
            return current;
        }
        finally { _refreshLock.Release(); }
    }

    private async Task RefreshAfterRebuildAsync(Guid key, string? storeAlias, string uniqueId)
    {
        try
        {
            await RefreshCacheAsync(key, storeAlias, uniqueId, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Stock {StockId} could not be refreshed after the cache rebuild", uniqueId);
        }
    }

    public void Dispose() => _refreshLock.Dispose();
}
