using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;

namespace Ekom.API;

/// <summary>Table-backed price overrides. Writes never save or publish Umbraco content.</summary>
public sealed class Prices : IDisposable
{
    private readonly Configuration _configuration;
    private readonly DatabaseFactory _databaseFactory;
    private readonly ILogger<Prices> _logger;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);
    private readonly object _publicationLock = new();
    private readonly AsyncLocal<IReadOnlyDictionary<Guid, PriceData[]>?> _calculation = new();
    private volatile ConcurrentDictionary<Guid, PriceData[]>? _cache;

    public Prices(Configuration configuration, DatabaseFactory databaseFactory,
        IHttpContextAccessor? httpContext = null, ILogger<Prices>? logger = null)
    {
        _configuration = configuration;
        _databaseFactory = databaseFactory;
        _logger = logger ?? NullLogger<Prices>.Instance;
    }

    public bool Enabled => _configuration.UseDatabasePrices;

    public static Prices Instance => Configuration.Resolver.GetRequiredService<Prices>();

    public IReadOnlyList<PriceData> Get(Guid nodeKey, string propertyAlias = "price")
    {
        propertyAlias = Normalize(propertyAlias, 100);
        return Read(nodeKey).Where(x => x.PropertyAlias == propertyAlias).Select(Clone).ToList();
    }

    public async Task<IReadOnlyList<PriceData>> GetAsync(Guid nodeKey, string propertyAlias = "price", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (_calculation.Value?.ContainsKey(nodeKey) != true)
            await EnsureReadyAsync(cancellationToken).ConfigureAwait(false);
        return Get(nodeKey, propertyAlias);
    }

    public void SetPrice(Guid nodeKey, string storeAlias, string currency, decimal price, string propertyAlias = "price")
        => Apply([new(nodeKey, storeAlias, currency, price, propertyAlias)]);

    public Task SetPriceAsync(Guid nodeKey, string storeAlias, string currency, decimal price,
        string propertyAlias = "price", CancellationToken cancellationToken = default)
        => ApplyAsync([new(nodeKey, storeAlias, currency, price, propertyAlias)], cancellationToken);

    /// <summary>Copy node values into missing table rows. Existing overrides are never replaced.</summary>
    /// <remarks>Supply published raw values to avoid making drafts live. A concurrent insert raises
    /// PriceConflictException and rolls back this node/property batch; retry after reloading.</remarks>
    public async Task<int> MigrateNodePricesAsync(Guid nodeKey, string? nodeValue, IReadOnlyList<IStore> stores,
        string propertyAlias = "price", CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(stores);
        var candidates = PriceHelper.ReadNodePrices(nodeKey, nodeValue, stores, propertyAlias);
        Validate(candidates);
        var existing = await GetAsync(nodeKey, propertyAlias, cancellationToken).ConfigureAwait(false);
        var missing = candidates.Where(change => !existing.Any(row =>
            row.StoreAlias.Equals(change.StoreAlias.Trim(), StringComparison.OrdinalIgnoreCase)
            && row.Currency.Equals(change.Currency.Trim(), StringComparison.OrdinalIgnoreCase))).ToList();
        await ApplyAsync(missing, cancellationToken).ConfigureAwait(false);
        return missing.Count;
    }

    public void RemovePrice(Guid nodeKey, string storeAlias, string currency, string propertyAlias = "price")
        => Apply([new(nodeKey, storeAlias, currency, null, propertyAlias)]);

    public Task RemovePriceAsync(Guid nodeKey, string storeAlias, string currency, string propertyAlias = "price", CancellationToken cancellationToken = default)
        => ApplyAsync([new(nodeKey, storeAlias, currency, null, propertyAlias)], cancellationToken);

    /// <summary>Validate the whole batch before any content save or database mutation.</summary>
    public void Validate(IEnumerable<PriceMutation> changes) => Prepare(changes);

    /// <summary>Atomically apply a batch; conflicts and validation errors leave every row unchanged.</summary>
    public void Apply(IEnumerable<PriceMutation> changes)
    {
        var batch = Prepare(changes);
        if (batch.Count == 0) return;
        List<PriceChangedEventArgs> events;
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                using var db = _databaseFactory.GetDatabase();
                using var tx = db.BeginTransaction(IsolationLevel.Serializable);
                events = new();
                foreach (var change in batch)
                {
                    var query = db.Prices.Where(x => x.NodeKey == change.NodeKey && x.PropertyAlias == change.PropertyAlias
                        && x.StoreAlias == change.StoreAlias && x.Currency == change.Currency);
                    var old = query.FirstOrDefault();
                    Check(change, old);
                    if (old?.Price == change.Price) continue;
                    if (change.Price == null) query.Delete();
                    else if (old == null) db.Insert(Row(change));
                    else query.Set(x => x.Price, change.Price.Value).Set(x => x.UpdateDate, DateTime.UtcNow).Update();
                    events.Add(Event(change, old));
                }
                tx.Commit();
                break;
            }
            catch (Exception ex) when (attempt < 5 && Retryable(ex))
            {
                Thread.Sleep(25 << attempt);
            }
        }
        Publish(events, batch.Select(change => change.NodeKey));
    }

    public async Task ApplyAsync(IEnumerable<PriceMutation> changes, CancellationToken cancellationToken = default)
    {
        var batch = Prepare(changes);
        if (batch.Count == 0) return;
        var events = await StockReservationService.RetryAsync(async () =>
        {
            await using var db = _databaseFactory.GetDatabase();
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
            var committed = new List<PriceChangedEventArgs>();
            foreach (var change in batch)
            {
                var query = db.Prices.Where(x => x.NodeKey == change.NodeKey && x.PropertyAlias == change.PropertyAlias
                    && x.StoreAlias == change.StoreAlias && x.Currency == change.Currency);
                var old = await query.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
                Check(change, old);
                if (old?.Price == change.Price) continue;
                if (change.Price == null) await query.DeleteAsync(cancellationToken).ConfigureAwait(false);
                else if (old == null) await db.InsertAsync(Row(change), token: cancellationToken).ConfigureAwait(false);
                else await query.Set(x => x.Price, change.Price.Value).Set(x => x.UpdateDate, DateTime.UtcNow).UpdateAsync(cancellationToken).ConfigureAwait(false);
                committed.Add(Event(change, old));
            }
            await tx.CommitAsync(cancellationToken).ConfigureAwait(false);
            return committed;
        }, cancellationToken).ConfigureAwait(false);
        // Cancellation or a subscriber failure after commit must not replay an already committed write.
        await PublishAsync(events, batch.Select(change => change.NodeKey)).ConfigureAwait(false);
    }

    private List<PriceMutation> Prepare(IEnumerable<PriceMutation> changes)
    {
        if (!_configuration.UseDatabasePrices) throw new InvalidOperationException("Enable Ekom:UseDatabasePrices before writing table prices.");
        ArgumentNullException.ThrowIfNull(changes);
        var batch = changes.Select(change =>
        {
            if (change.NodeKey == Guid.Empty) throw new ArgumentException("A node key is required.", nameof(changes));
            if (change.Price is decimal price && (price < 0 || price > 99999999999999.9999m || decimal.Round(price, 4) != price))
                throw new ArgumentOutOfRangeException(nameof(changes), "Prices must be non-negative decimal(18,4) values.");
            return change with { PropertyAlias = Normalize(change.PropertyAlias, 100), StoreAlias = Normalize(change.StoreAlias, 100), Currency = Normalize(change.Currency, 50) };
        }).OrderBy(x => x.NodeKey).ThenBy(x => x.PropertyAlias, StringComparer.Ordinal)
            .ThenBy(x => x.StoreAlias, StringComparer.Ordinal).ThenBy(x => x.Currency, StringComparer.Ordinal).ToList();
        if (batch.Select(x => (x.NodeKey, x.PropertyAlias, x.StoreAlias, x.Currency)).Distinct().Count() != batch.Count)
            throw new ArgumentException("A price batch must not contain duplicate keys.", nameof(changes));
        return batch;
    }

    private static void Check(PriceMutation change, PriceData? old)
    {
        if (change.CheckExpected && change.ExpectedPrice != old?.Price) throw new PriceConflictException();
    }

    private static PriceData Row(PriceMutation change) => new()
    {
        NodeKey = change.NodeKey, StoreAlias = change.StoreAlias, Currency = change.Currency,
        PropertyAlias = change.PropertyAlias, Price = change.Price!.Value, UpdateDate = DateTime.UtcNow,
    };

    private static PriceChangedEventArgs Event(PriceMutation change, PriceData? old) => new()
    {
        Key = change.NodeKey, StoreAlias = change.StoreAlias, Currency = change.Currency,
        PropertyAlias = change.PropertyAlias, OldValue = old?.Price, NewValue = change.Price,
    };

    private void Publish(IReadOnlyList<PriceChangedEventArgs> events, IEnumerable<Guid> keys)
    {
        _refreshGate.Wait();
        try
        {
            try
            {
                if (_cache == null) FillLocked();
                else
                {
                    var nodes = keys.Distinct().ToArray();
                    var rows = new List<PriceData>();
                    foreach (var batch in nodes.Chunk(400))
                    {
                        using var db = _databaseFactory.GetDatabase();
                        rows.AddRange(db.Prices.Where(row => Enumerable.Contains(batch, row.NodeKey)).ToList());
                    }
                    PublishRows(nodes, rows);
                }
            }
            catch (Exception ex) { RefreshFailed(ex); }
        }
        finally { _refreshGate.Release(); }
        Notify(events);
    }

    private async Task PublishAsync(IReadOnlyList<PriceChangedEventArgs> events, IEnumerable<Guid> keys)
    {
        // Commit already succeeded. Do not cancel or replay cache publication.
        await _refreshGate.WaitAsync().ConfigureAwait(false);
        try
        {
            try
            {
                if (_cache == null) await FillLockedAsync(CancellationToken.None).ConfigureAwait(false);
                else
                {
                    var nodes = keys.Distinct().ToArray();
                    var rows = new List<PriceData>();
                    foreach (var batch in nodes.Chunk(400))
                    {
                        await using var db = _databaseFactory.GetDatabase();
                        rows.AddRange(await db.Prices.Where(row => Enumerable.Contains(batch, row.NodeKey))
                            .ToListAsync().ConfigureAwait(false));
                    }
                    PublishRows(nodes, rows);
                }
            }
            catch (Exception ex) { RefreshFailed(ex); }
        }
        finally { _refreshGate.Release(); }
        Notify(events);
    }

    private void RefreshFailed(Exception ex)
    {
        lock (_publicationLock) _cache = null;
        _logger.LogWarning(ex, "Prices committed, but cache refresh failed; the next read will reload prices");
    }

    private void Notify(IReadOnlyList<PriceChangedEventArgs> events)
    {
        var previous = _calculation.Value;
        _calculation.Value = null;
        try
        {
            foreach (var change in events)
                foreach (var handler in PriceEvents.Handlers)
                {
                    try { ((EventHandler<PriceChangedEventArgs>)handler)(this, change); }
                    catch (Exception ex) { _logger.LogWarning(ex, "Price for {NodeKey} in {Store} committed, but notification failed", change.Key, change.StoreAlias); }
                }
        }
        finally { _calculation.Value = previous; }
    }

    /// <summary>Load all table prices into the shared cache. Also use to reload after external SQL/server writes.</summary>
    public void FillCache()
    {
        if (!Enabled) return;
        _refreshGate.Wait();
        try { FillLocked(); }
        finally { _refreshGate.Release(); }
    }

    /// <summary>Reload the shared cache without blocking a thread while waiting for SQL or another refresh.</summary>
    public async Task FillCacheAsync(CancellationToken cancellationToken = default)
    {
        if (!Enabled) return;
        await _refreshGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try { await FillLockedAsync(cancellationToken).ConfigureAwait(false); }
        finally { _refreshGate.Release(); }
    }

    private void FillLocked()
    {
        using var db = _databaseFactory.GetDatabase();
        PublishFullCache(db.Prices.ToList());
    }

    private async Task FillLockedAsync(CancellationToken ct)
    {
        await using var db = _databaseFactory.GetDatabase();
        var rows = await db.Prices.ToListAsync(ct).ConfigureAwait(false);
        PublishFullCache(rows);
    }

    private void PublishFullCache(List<PriceData> rows)
    {
        var next = new ConcurrentDictionary<Guid, PriceData[]>(rows.GroupBy(row => row.NodeKey)
            .Select(group => new KeyValuePair<Guid, PriceData[]>(group.Key, group.ToArray())));
        lock (_publicationLock) _cache = next;
    }

    private void PublishRows(Guid[] keys, List<PriceData> rows)
    {
        var grouped = rows.ToLookup(row => row.NodeKey);
        var updates = keys.Select(key => (Key: key, Rows: grouped[key].ToArray())).ToArray();
        // Only in-memory publication is locked. Capture related node snapshots atomically,
        // including when a committed batch spans multiple SQL refresh chunks.
        lock (_publicationLock)
        {
            foreach (var update in updates)
            {
                if (update.Rows.Length == 0) _cache!.TryRemove(update.Key, out _);
                else _cache![update.Key] = update.Rows;
            }
        }
    }

    private void EnsureReady()
    {
        if (_cache != null || !Enabled) return;
        _refreshGate.Wait();
        try { if (_cache == null) FillLocked(); }
        finally { _refreshGate.Release(); }
    }

    private async Task EnsureReadyAsync(CancellationToken ct)
    {
        if (_cache != null || !Enabled) return;
        await _refreshGate.WaitAsync(ct).ConfigureAwait(false);
        try { if (_cache == null) await FillLockedAsync(ct).ConfigureAwait(false); }
        finally { _refreshGate.Release(); }
    }

    internal IDisposable BeginCalculationScope(Guid key, Guid? parentKey = null)
    {
        var previous = _calculation.Value;
        if (previous?.ContainsKey(key) == true && (!parentKey.HasValue || previous.ContainsKey(parentKey.Value)))
            return EmptyReadScope.Instance;
        var snapshot = previous == null ? new Dictionary<Guid, PriceData[]>() : new Dictionary<Guid, PriceData[]>(previous);
        while (true)
        {
            EnsureReady();
            lock (_publicationLock)
            {
                if (Enabled && _cache == null) continue;
                Capture(key);
                if (parentKey.HasValue) Capture(parentKey.Value);
                break;
            }
        }
        _calculation.Value = snapshot;
        return new CalculationScope(() => _calculation.Value = previous);

        void Capture(Guid nodeKey)
        {
            if (!snapshot.ContainsKey(nodeKey))
                snapshot[nodeKey] = _cache != null && _cache.TryGetValue(nodeKey, out var rows) ? rows : [];
        }
    }

    private sealed class CalculationScope(Action restore) : IDisposable
    {
        private bool _disposed;
        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            restore();
        }
    }

    private IReadOnlyList<PriceData> Read(Guid nodeKey)
    {
        if (!Enabled) return Array.Empty<PriceData>();
        if (_calculation.Value?.TryGetValue(nodeKey, out var pinned) == true) return pinned;
        while (true)
        {
            EnsureReady();
            var cache = _cache;
            if (cache != null) return cache.TryGetValue(nodeKey, out var rows) ? rows : Array.Empty<PriceData>();
        }
    }

    public void Dispose() => _refreshGate.Dispose();

    private static PriceData Clone(PriceData row) => new()
    {
        NodeKey = row.NodeKey, PropertyAlias = row.PropertyAlias, StoreAlias = row.StoreAlias,
        Currency = row.Currency, Price = row.Price, UpdateDate = row.UpdateDate,
    };

    /// <summary>Compatibility helper. Reads now share the application cache without a scope.</summary>
    public IDisposable BeginReadScope() => EmptyReadScope.Instance;

    /// <summary>Compatibility helper. Ensures the shared cache is ready; node keys no longer require preloading.</summary>
    public Task PreloadAsync(IEnumerable<Guid> nodeKeys, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(nodeKeys);
        return EnsureReadyAsync(cancellationToken);
    }

    public Task PreloadProductsAsync(IEnumerable<IProduct> products, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(products);
        return PreloadAsync(Array.Empty<Guid>(), cancellationToken);
    }

    private sealed class EmptyReadScope : IDisposable
    {
        public static readonly EmptyReadScope Instance = new();
        public void Dispose() { }
    }

    public string Resolve(Guid nodeKey, string propertyAlias, string storeAlias, string nodeValue, IReadOnlyList<CurrencyModel> currencies)
    {
        if (!_configuration.UseDatabasePrices) return nodeValue;
        propertyAlias = Normalize(propertyAlias, 100);
        var store = Normalize(storeAlias, 100);
        var rows = Read(nodeKey).Where(x => x.PropertyAlias == propertyAlias && x.StoreAlias == store).ToList();
        return rows.Count == 0 ? nodeValue : Merge(nodeValue, storeAlias, currencies, rows);
    }

    internal static string Merge(string nodeValue, string storeAlias, IReadOnlyList<CurrencyModel> currencies, IReadOnlyList<PriceData> rows)
    {
        var values = new List<CurrencyPrice>();
        var defaultCurrency = currencies.FirstOrDefault()?.CurrencyValue ?? string.Empty;
        foreach (var currency in currencies)
        {
            var row = rows.FirstOrDefault(x => string.Equals(x.Currency, currency.CurrencyValue, StringComparison.OrdinalIgnoreCase));
            var price = row?.Price ?? NativeDiscountPrice.Read(nodeValue, storeAlias, currency.CurrencyValue, defaultCurrency);
            if (price.HasValue) values.Add(new CurrencyPrice(price.Value, currency.CurrencyValue));
        }
        return JsonSerializer.Serialize(values);
    }

    private static bool Retryable(Exception ex) => ex is SqliteException { SqliteErrorCode: 5 or 6 }
        || ex is SqliteException { SqliteExtendedErrorCode: 1555 or 2067 }
        || ex is SqlException { Number: 1205 or 2601 or 2627 };

    private static string Normalize(string value, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        value = value.Trim().ToUpperInvariant();
        if (value.Length > maxLength) throw new ArgumentException($"Identifier exceeds {maxLength} characters.", nameof(value));
        return value;
    }
}
