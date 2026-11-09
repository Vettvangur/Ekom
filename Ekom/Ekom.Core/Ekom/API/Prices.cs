using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using System.Text.Json;

namespace Ekom.API;

/// <summary>Table-backed price overrides. Writes never save or publish Umbraco content.</summary>
public sealed class Prices
{
    private readonly Configuration _configuration;
    private readonly DatabaseFactory _databaseFactory;

    public Prices(Configuration configuration, DatabaseFactory databaseFactory)
    {
        _configuration = configuration;
        _databaseFactory = databaseFactory;
    }

    public static Prices Instance => Configuration.Resolver.GetRequiredService<Prices>();

    public async Task<IReadOnlyList<PriceData>> GetAsync(Guid nodeKey, string propertyAlias = "price", CancellationToken cancellationToken = default)
    {
        if (!_configuration.UseDatabasePrices) return Array.Empty<PriceData>();
        propertyAlias = Normalize(propertyAlias, 100);
        await using var db = _databaseFactory.GetDatabase();
        return await db.Prices.Where(x => x.NodeKey == nodeKey && x.PropertyAlias == propertyAlias)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public void SetPrice(Guid nodeKey, string storeAlias, string currency, decimal price, string propertyAlias = "price")
    {
        var row = CreateRow(nodeKey, storeAlias, currency, price, propertyAlias);
        using var db = _databaseFactory.GetDatabase();
        db.Prices.InsertOrUpdate(() => new PriceData
            {
                NodeKey = row.NodeKey,
                PropertyAlias = row.PropertyAlias,
                StoreAlias = row.StoreAlias,
                Currency = row.Currency,
                Price = row.Price,
                UpdateDate = row.UpdateDate,
            },
            existing => new PriceData { Price = row.Price, UpdateDate = row.UpdateDate });
    }

    public async Task SetPriceAsync(Guid nodeKey, string storeAlias, string currency, decimal price,
        string propertyAlias = "price", CancellationToken cancellationToken = default)
    {
        var row = CreateRow(nodeKey, storeAlias, currency, price, propertyAlias);
        await using var db = _databaseFactory.GetDatabase();
        await db.Prices.InsertOrUpdateAsync(() => new PriceData
            {
                NodeKey = row.NodeKey,
                PropertyAlias = row.PropertyAlias,
                StoreAlias = row.StoreAlias,
                Currency = row.Currency,
                Price = row.Price,
                UpdateDate = row.UpdateDate,
            },
            existing => new PriceData { Price = row.Price, UpdateDate = row.UpdateDate }, cancellationToken)
            .ConfigureAwait(false);
    }

    // Synchronous model properties use a synchronous SQL read, not sync-over-async.
    // Reading committed rows avoids process-local stale prices on multi-server installations.
    public string Resolve(Guid nodeKey, string propertyAlias, string storeAlias, string nodeValue, IReadOnlyList<CurrencyModel> currencies)
    {
        if (!_configuration.UseDatabasePrices) return nodeValue;
        propertyAlias = Normalize(propertyAlias, 100);
        var normalizedStore = Normalize(storeAlias, 100);
        using var db = _databaseFactory.GetDatabase();
        var rows = db.Prices.Where(x => x.NodeKey == nodeKey && x.PropertyAlias == propertyAlias && x.StoreAlias == normalizedStore).ToList();
        if (rows.Count == 0) return nodeValue;
        return Merge(nodeValue, storeAlias, currencies, rows);
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

    private PriceData CreateRow(Guid nodeKey, string storeAlias, string currency, decimal price, string propertyAlias)
    {
        if (!_configuration.UseDatabasePrices) throw new InvalidOperationException("Enable Ekom:UseDatabasePrices before writing table prices.");
        if (nodeKey == Guid.Empty) throw new ArgumentException("A saved node key is required.", nameof(nodeKey));
        if (price < 0 || price > 99999999999999.9999m || decimal.Round(price, 4) != price)
            throw new ArgumentOutOfRangeException(nameof(price), "Prices must be non-negative decimal(18,4) values.");
        return new PriceData
        {
            NodeKey = nodeKey,
            PropertyAlias = Normalize(propertyAlias, 100),
            StoreAlias = Normalize(storeAlias, 100),
            Currency = Normalize(currency, 50),
            Price = price,
            UpdateDate = DateTime.UtcNow,
        };
    }

    private static string Normalize(string value, int maxLength)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        value = value.Trim().ToUpperInvariant();
        if (value.Length > maxLength) throw new ArgumentException($"Identifier exceeds {maxLength} characters.", nameof(value));
        return value;
    }
}
