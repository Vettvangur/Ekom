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
public sealed class Prices
{
    private readonly Configuration _configuration;
    private readonly DatabaseFactory _databaseFactory;
    private readonly ILogger<Prices> _logger;
    private readonly object _cacheLock = new();
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

    public Task<IReadOnlyList<PriceData>> GetAsync(Guid nodeKey, string propertyAlias = "price", CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Get(nodeKey, propertyAlias));
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
        Publish(events, batch.Select(change => change.NodeKey));
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
        // Serialize refreshes and read committed rows instead of replaying event values:
        // a delayed notification must never overwrite a newer committed price.
        try { Refresh(keys); }
        catch (Exception ex)
        {
            lock (_cacheLock) _cache = null;
            _logger.LogWarning(ex, "Prices committed, but cache refresh failed; the next read will reload prices");
        }
        foreach (var change in events)
        {
            foreach (var handler in PriceEvents.Handlers)
            {
                try { ((EventHandler<PriceChangedEventArgs>)handler)(this, change); }
                catch (Exception ex) { _logger.LogWarning(ex, "Price for {NodeKey} in {Store} committed, but notification failed", change.Key, change.StoreAlias); }
            }
        }
    }

    /// <summary>Load all table prices into the shared cache. Also use to reload after external SQL/server writes.</summary>
    public void FillCache()
    {
        if (!Enabled) return;
        lock (_cacheLock)
        {
            using var db = _databaseFactory.GetDatabase();
            var rows = db.Prices.ToList();
            // Publish only a complete cache; a failed reload leaves the previous cache intact.
            _cache = new ConcurrentDictionary<Guid, PriceData[]>(rows.GroupBy(row => row.NodeKey)
                .Select(group => new KeyValuePair<Guid, PriceData[]>(group.Key, group.ToArray())));
        }
    }

    private void EnsureReady()
    {
        if (_cache != null || !Enabled) return;
        lock (_cacheLock)
            if (_cache == null) FillCache();
    }

    private void Refresh(IEnumerable<Guid> keys)
    {
        lock (_cacheLock)
        {
            if (_cache == null) { FillCache(); return; }
            foreach (var batch in keys.Distinct().Chunk(400))
            {
                using var db = _databaseFactory.GetDatabase();
                var rows = db.Prices.Where(row => Enumerable.Contains(batch, row.NodeKey)).ToList().ToLookup(row => row.NodeKey);
                foreach (var key in batch)
                {
                    var current = rows[key].ToArray();
                    if (current.Length == 0) _cache.TryRemove(key, out _);
                    else _cache[key] = current;
                }
            }
        }
    }

    private IReadOnlyList<PriceData> Read(Guid nodeKey)
    {
        if (!Enabled) return Array.Empty<PriceData>();
        var cache = _cache;
        if (cache == null)
        {
            lock (_cacheLock)
            {
                EnsureReady();
                cache = _cache;
            }
        }
        return cache != null && cache.TryGetValue(nodeKey, out var rows) ? rows : Array.Empty<PriceData>();
    }

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
        EnsureReady();
        return Task.CompletedTask;
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
