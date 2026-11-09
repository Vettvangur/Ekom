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
using System.Data;
using System.Text.Json;

namespace Ekom.API;

/// <summary>Table-backed price overrides. Writes never save or publish Umbraco content.</summary>
public sealed class Prices
{
    private readonly Configuration _configuration;
    private readonly DatabaseFactory _databaseFactory;
    private readonly IHttpContextAccessor? _httpContext;
    private readonly ILogger<Prices> _logger;
    private readonly object _requestKey = new();

    public Prices(Configuration configuration, DatabaseFactory databaseFactory,
        IHttpContextAccessor? httpContext = null, ILogger<Prices>? logger = null)
    {
        _configuration = configuration;
        _databaseFactory = databaseFactory;
        _httpContext = httpContext;
        _logger = logger ?? NullLogger<Prices>.Instance;
    }

    public static Prices Instance => Configuration.Resolver.GetRequiredService<Prices>();

    public IReadOnlyList<PriceData> Get(Guid nodeKey, string propertyAlias = "price")
    {
        propertyAlias = Normalize(propertyAlias, 100);
        return Read(nodeKey).Where(x => x.PropertyAlias == propertyAlias).ToList();
    }

    public async Task<IReadOnlyList<PriceData>> GetAsync(Guid nodeKey, string propertyAlias = "price", CancellationToken cancellationToken = default)
    {
        propertyAlias = Normalize(propertyAlias, 100);
        await using var db = _databaseFactory.GetDatabase();
        return await db.Prices.Where(x => x.NodeKey == nodeKey && x.PropertyAlias == propertyAlias)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public void SetPrice(Guid nodeKey, string storeAlias, string currency, decimal price, string propertyAlias = "price")
        => Apply([new(nodeKey, storeAlias, currency, price, propertyAlias)]);

    public Task SetPriceAsync(Guid nodeKey, string storeAlias, string currency, decimal price,
        string propertyAlias = "price", CancellationToken cancellationToken = default)
        => ApplyAsync([new(nodeKey, storeAlias, currency, price, propertyAlias)], cancellationToken);

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
        Publish(events);
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
        Publish(events);
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

    private void Publish(IReadOnlyList<PriceChangedEventArgs> events)
    {
        foreach (var change in events)
        {
            var items = _httpContext?.HttpContext?.Items;
            if (items != null) lock (items)
                if (items.TryGetValue(_requestKey, out var value) && value is Dictionary<Guid, List<PriceData>> snapshot) snapshot.Remove(change.Key);
            foreach (var handler in PriceEvents.Handlers)
            {
                try { ((EventHandler<PriceChangedEventArgs>)handler)(this, change); }
                catch (Exception ex) { _logger.LogWarning(ex, "Price for {NodeKey} in {Store} committed, but notification failed", change.Key, change.StoreAlias); }
            }
        }
    }

    // One immutable node-price snapshot per HTTP request, shared by base and discount prices.
    // A fresh request on any server reads committed data; writes invalidate the current request's snapshot.
    private List<PriceData> Read(Guid nodeKey)
    {
        List<PriceData> Load()
        {
            using var db = _databaseFactory.GetDatabase();
            return db.Prices.Where(x => x.NodeKey == nodeKey).ToList();
        }
        var items = _httpContext?.HttpContext?.Items;
        if (items == null) return Load();
        lock (items)
        {
            if (!items.TryGetValue(_requestKey, out var value)) items[_requestKey] = value = new Dictionary<Guid, List<PriceData>>();
            var snapshot = (Dictionary<Guid, List<PriceData>>)value!;
            if (!snapshot.TryGetValue(nodeKey, out var rows)) snapshot[nodeKey] = rows = Load();
            return rows;
        }
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
