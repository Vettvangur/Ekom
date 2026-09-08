using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.API;

/// <summary>
/// The Ekom API for isolated warehouse stock balances.
/// </summary>
public sealed class Warehouse
{
    /// <summary>
    /// Warehouse stock instance.
    /// </summary>
    public static Warehouse Instance => Configuration.Resolver.GetRequiredService<Warehouse>();

    private readonly WarehouseStockRepository _warehouseStockRepository;
    private readonly IWarehouseDefinitionService _warehouseDefinitionService;

    internal Warehouse(
        WarehouseStockRepository warehouseStockRepository,
        IWarehouseDefinitionService warehouseDefinitionService)
    {
        _warehouseStockRepository = warehouseStockRepository;
        _warehouseDefinitionService = warehouseDefinitionService;
    }

    public Task<IReadOnlyList<WarehouseDefinition>> GetWarehousesAsync(string storeAlias, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult<IReadOnlyList<WarehouseDefinition>>(_warehouseDefinitionService
            .GetPublishedWarehouses(NormalizeStoreAlias(storeAlias))
            .Where(warehouse => warehouse.Visible)
            .ToList());
    }

    public async Task<IReadOnlyList<WarehouseStockBalance>> GetAsync(
        string storeAlias,
        IEnumerable<string> skus,
        CancellationToken ct = default)
    {
        string normalizedStoreAlias = NormalizeStoreAlias(storeAlias);
        ArgumentNullException.ThrowIfNull(skus);

        string[] normalizedSkus = skus
            .Select(NormalizeSku)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedSkus.Length == 0)
        {
            return Array.Empty<WarehouseStockBalance>();
        }

        var warehouseKeys = _warehouseDefinitionService
            .GetPublishedWarehouses(normalizedStoreAlias)
            .Where(warehouse => warehouse.Visible)
            .Select(warehouse => warehouse.Key)
            .ToHashSet();

        if (warehouseKeys.Count == 0)
        {
            return Array.Empty<WarehouseStockBalance>();
        }

        List<WarehouseStockData> stockData = await _warehouseStockRepository
            .GetAsync(normalizedStoreAlias, normalizedSkus, ct)
            .ConfigureAwait(false);

        return stockData
            .Where(stock => warehouseKeys.Contains(stock.WarehouseKey))
            .Select(Map)
            .ToList();
    }

    /// <summary>
    /// Gets a warehouse balance for a SKU.
    /// </summary>
    /// <param name="storeAlias">Store alias that owns the balance.</param>
    /// <param name="warehouseKey">Warehouse identifier.</param>
    /// <param name="sku">SKU to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The balance when one exists; otherwise, <see langword="null"/>.</returns>
    public async Task<WarehouseStockBalance?> GetAsync(
        string storeAlias,
        Guid warehouseKey,
        string sku,
        CancellationToken ct = default)
    {
        string normalizedStoreAlias = NormalizeStoreAlias(storeAlias);
        ValidateWarehouseKey(warehouseKey);
        EnsureWarehouseExists(normalizedStoreAlias, warehouseKey);
        string normalizedSku = NormalizeSku(sku);

        WarehouseStockData? stockData = await _warehouseStockRepository
            .GetAsync(normalizedStoreAlias, warehouseKey, normalizedSku, ct)
            .ConfigureAwait(false);

        return stockData is null ? null : Map(stockData);
    }

    /// <summary>
    /// Gets warehouse balances for SKUs.
    /// </summary>
    /// <param name="storeAlias">Store alias that owns the balances.</param>
    /// <param name="warehouseKey">Warehouse identifier.</param>
    /// <param name="skus">SKUs to retrieve.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>Balances for SKUs with persisted records.</returns>
    public async Task<IReadOnlyList<WarehouseStockBalance>> GetAsync(
        string storeAlias,
        Guid warehouseKey,
        IEnumerable<string> skus,
        CancellationToken ct = default)
    {
        string normalizedStoreAlias = NormalizeStoreAlias(storeAlias);
        ValidateWarehouseKey(warehouseKey);
        EnsureWarehouseExists(normalizedStoreAlias, warehouseKey);
        ArgumentNullException.ThrowIfNull(skus);

        string[] normalizedSkus = skus
            .Select(NormalizeSku)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (normalizedSkus.Length == 0)
        {
            return Array.Empty<WarehouseStockBalance>();
        }

        List<WarehouseStockData> stockData = await _warehouseStockRepository
            .GetAsync(normalizedStoreAlias, warehouseKey, normalizedSkus, ct)
            .ConfigureAwait(false);

        return stockData.Select(Map).ToList();
    }

    /// <summary>
    /// Sets a warehouse balance for a SKU, creating it when it does not exist.
    /// </summary>
    /// <param name="storeAlias">Store alias that owns the balance.</param>
    /// <param name="warehouseKey">Warehouse identifier.</param>
    /// <param name="sku">SKU to set.</param>
    /// <param name="balance">Non-negative warehouse balance.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The set balance.</returns>
    public async Task<WarehouseStockBalance> SetAsync(
        string storeAlias,
        Guid warehouseKey,
        string sku,
        decimal balance,
        CancellationToken ct = default)
    {
        string normalizedStoreAlias = NormalizeStoreAlias(storeAlias);
        ValidateWarehouseKey(warehouseKey);
        EnsureWarehouseExists(normalizedStoreAlias, warehouseKey);
        string normalizedSku = NormalizeSku(sku);
        ValidateBalance(balance);

        WarehouseStockData stockData = await _warehouseStockRepository
            .SetAsync(normalizedStoreAlias, warehouseKey, normalizedSku, balance, ct)
            .ConfigureAwait(false);

        return Map(stockData);
    }

    /// <summary>
    /// Sets warehouse balances, creating records when they do not exist.
    /// </summary>
    /// <param name="storeAlias">Store alias that owns the balances.</param>
    /// <param name="warehouseKey">Warehouse identifier.</param>
    /// <param name="balances">SKU balances to set.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The set balances.</returns>
    public async Task<IReadOnlyList<WarehouseStockBalance>> SetAsync(
        string storeAlias,
        Guid warehouseKey,
        IEnumerable<WarehouseStockBalanceRequest> balances,
        CancellationToken ct = default)
    {
        string normalizedStoreAlias = NormalizeStoreAlias(storeAlias);
        ValidateWarehouseKey(warehouseKey);
        EnsureWarehouseExists(normalizedStoreAlias, warehouseKey);
        ArgumentNullException.ThrowIfNull(balances);

        var normalizedBalances = new List<WarehouseStockBalanceRequest>();
        var skus = new HashSet<string>(StringComparer.Ordinal);

        foreach (WarehouseStockBalanceRequest balance in balances)
        {
            ArgumentNullException.ThrowIfNull(balance);

            string normalizedSku = NormalizeSku(balance.Sku);
            ValidateBalance(balance.Balance);

            if (!skus.Add(normalizedSku))
            {
                throw new ArgumentException("Balances must not contain duplicate SKUs after normalization.", nameof(balances));
            }

            normalizedBalances.Add(new WarehouseStockBalanceRequest
            {
                Sku = normalizedSku,
                Balance = balance.Balance,
            });
        }

        var results = new List<WarehouseStockBalance>(normalizedBalances.Count);
        foreach (WarehouseStockBalanceRequest balance in normalizedBalances)
        {
            WarehouseStockData stockData = await _warehouseStockRepository
                .SetAsync(normalizedStoreAlias, warehouseKey, balance.Sku, balance.Balance, ct)
                .ConfigureAwait(false);

            results.Add(Map(stockData));
        }

        return results;
    }

    private static string NormalizeStoreAlias(string storeAlias)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            throw new ArgumentException("Store alias is required.", nameof(storeAlias));
        }

        return storeAlias.Trim();
    }

    private static void ValidateWarehouseKey(Guid warehouseKey)
    {
        if (warehouseKey == Guid.Empty)
        {
            throw new ArgumentException("Warehouse identifier is required.", nameof(warehouseKey));
        }
    }

    private void EnsureWarehouseExists(string storeAlias, Guid warehouseKey)
    {
        if (!_warehouseDefinitionService.GetPublishedWarehouses(storeAlias).Any(warehouse => warehouse.Key == warehouseKey))
        {
            throw new InvalidOperationException($"Warehouse '{warehouseKey}' is not configured for store '{storeAlias}'.");
        }
    }

    private static string NormalizeSku(string sku)
    {
        if (string.IsNullOrWhiteSpace(sku))
        {
            throw new ArgumentException("SKU is required.", nameof(sku));
        }

        return sku.Trim().ToUpperInvariant();
    }

    private static void ValidateBalance(decimal balance)
    {
        if (balance < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(balance), "Warehouse balance must be non-negative.");
        }
    }

    private static WarehouseStockBalance Map(WarehouseStockData stockData)
    {
        return new WarehouseStockBalance
        {
            StoreAlias = stockData.StoreAlias,
            WarehouseKey = stockData.WarehouseKey,
            Sku = stockData.Sku,
            Balance = stockData.Balance,
            UpdateDate = stockData.UpdateDate,
        };
    }
}
