using Ekom.Cache;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using System.Runtime.ExceptionServices;

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

    private readonly IWarehouseStockRepository _warehouseStockRepository;
    private readonly IWarehouseDefinitionService _warehouseDefinitionService;
    private readonly WarehouseStockCache _warehouseStockCache;

    internal Warehouse(
        IWarehouseStockRepository warehouseStockRepository,
        IWarehouseDefinitionService warehouseDefinitionService,
        WarehouseStockCache warehouseStockCache)
    {
        _warehouseStockRepository = warehouseStockRepository;
        _warehouseDefinitionService = warehouseDefinitionService;
        _warehouseStockCache = warehouseStockCache;
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
        await Task.CompletedTask.ConfigureAwait(false);
        string normalizedStoreAlias = NormalizeStoreAlias(storeAlias);
        ArgumentNullException.ThrowIfNull(skus);
        ct.ThrowIfCancellationRequested();
        _warehouseStockCache.EnsureReady();

        HashSet<string> normalizedSkus = skus
            .Select(NormalizeSku)
            .ToHashSet(StringComparer.Ordinal);

        if (normalizedSkus.Count == 0)
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

        IReadOnlyList<WarehouseStockBalance> balances = _warehouseStockCache
            .Get(normalizedStoreAlias, normalizedSkus)
            .Where(stock => warehouseKeys.Contains(stock.WarehouseKey))
            .Select(Map)
            .ToList();

        return balances;
    }

    /// <summary>
    /// Gets a warehouse balance for a SKU from the in-memory cache.
    /// </summary>
    public async Task<WarehouseStockBalance?> GetAsync(
        string storeAlias,
        Guid warehouseKey,
        string sku,
        CancellationToken ct = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        string normalizedStoreAlias = NormalizeStoreAlias(storeAlias);
        ValidateWarehouseKey(warehouseKey);
        EnsureWarehouseExists(normalizedStoreAlias, warehouseKey);
        string normalizedSku = NormalizeSku(sku);
        ct.ThrowIfCancellationRequested();

        WarehouseStockData? stockData = _warehouseStockCache.Get(normalizedStoreAlias, warehouseKey, normalizedSku);
        return stockData is null ? null : Map(stockData);
    }

    /// <summary>
    /// Gets warehouse balances for SKUs from the in-memory cache.
    /// </summary>
    public async Task<IReadOnlyList<WarehouseStockBalance>> GetAsync(
        string storeAlias,
        Guid warehouseKey,
        IEnumerable<string> skus,
        CancellationToken ct = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        string normalizedStoreAlias = NormalizeStoreAlias(storeAlias);
        ValidateWarehouseKey(warehouseKey);
        EnsureWarehouseExists(normalizedStoreAlias, warehouseKey);
        ArgumentNullException.ThrowIfNull(skus);
        ct.ThrowIfCancellationRequested();
        _warehouseStockCache.EnsureReady();

        HashSet<string> normalizedSkus = skus
            .Select(NormalizeSku)
            .ToHashSet(StringComparer.Ordinal);

        if (normalizedSkus.Count == 0)
        {
            return Array.Empty<WarehouseStockBalance>();
        }

        IReadOnlyList<WarehouseStockBalance> balances = _warehouseStockCache
            .Get(normalizedStoreAlias, normalizedSkus, warehouseKey)
            .Select(Map)
            .ToList();

        return balances;
    }

    /// <summary>
    /// Gets visible warehouses combined with the cached balance for one SKU.
    /// </summary>
    public async Task<IReadOnlyList<WarehouseStockLevel>> GetForSkuAsync(
        string storeAlias,
        string sku,
        CancellationToken ct = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        string normalizedStoreAlias = NormalizeStoreAlias(storeAlias);
        string normalizedSku = NormalizeSku(sku);
        ct.ThrowIfCancellationRequested();
        _warehouseStockCache.EnsureReady();

        IReadOnlyList<WarehouseStockLevel> levels = _warehouseDefinitionService
            .GetPublishedWarehouses(normalizedStoreAlias)
            .Where(warehouse => warehouse.Visible)
            .OrderBy(warehouse => warehouse.SortOrder)
            .Select(warehouse =>
            {
                WarehouseStockData? stock = _warehouseStockCache.Get(normalizedStoreAlias, warehouse.Key, normalizedSku);
                return new WarehouseStockLevel
                {
                    StoreAlias = normalizedStoreAlias,
                    WarehouseKey = warehouse.Key,
                    Code = warehouse.Code,
                    Name = warehouse.Name,
                    SortOrder = warehouse.SortOrder,
                    Sku = normalizedSku,
                    Balance = stock?.Balance,
                    UpdateDate = stock?.UpdateDate,
                };
            })
            .ToList();

        return levels;
    }

    /// <summary>
    /// Sets a warehouse balance, skipping persistence when the value is unchanged.
    /// </summary>
    public async Task<WarehouseStockBalance> SetAsync(
        string storeAlias,
        Guid warehouseKey,
        string sku,
        decimal balance,
        CancellationToken ct = default)
    {
        NormalizedMutation mutation = NormalizeMutation(new WarehouseStockMutationRequest
        {
            StoreAlias = storeAlias,
            WarehouseKey = warehouseKey,
            Sku = sku,
            Operation = WarehouseStockMutationOperation.Set,
            Balance = balance,
        }, 0);

        WarehouseStockBatchResult result = await ApplyNormalizedAsync([mutation], 1, null, ct).ConfigureAwait(false);
        WarehouseStockMutationResult entry = result.Entries[0];
        ThrowIfFailed(entry);

        return new WarehouseStockBalance
        {
            StoreAlias = entry.StoreAlias,
            WarehouseKey = entry.WarehouseKey,
            Sku = entry.Sku,
            Balance = entry.Balance!.Value,
            UpdateDate = entry.UpdateDate!.Value,
        };
    }

    /// <summary>
    /// Sets warehouse balances for one warehouse.
    /// </summary>
    public async Task<IReadOnlyList<WarehouseStockBalance>> SetAsync(
        string storeAlias,
        Guid warehouseKey,
        IEnumerable<WarehouseStockBalanceRequest> balances,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(balances);
        var mutations = new List<NormalizedMutation>();
        var skus = new HashSet<string>(StringComparer.Ordinal);

        foreach (WarehouseStockBalanceRequest balance in balances)
        {
            ArgumentNullException.ThrowIfNull(balance);
            NormalizedMutation mutation = NormalizeMutation(new WarehouseStockMutationRequest
            {
                StoreAlias = storeAlias,
                WarehouseKey = warehouseKey,
                Sku = balance.Sku,
                Operation = WarehouseStockMutationOperation.Set,
                Balance = balance.Balance,
            }, mutations.Count);

            if (!skus.Add(mutation.Sku))
            {
                throw new ArgumentException("Balances must not contain duplicate SKUs after normalization.", nameof(balances));
            }

            mutations.Add(mutation);
        }

        var entries = new List<WarehouseStockMutationResult>(mutations.Count);
        foreach (NormalizedMutation mutation in mutations)
        {
            WarehouseStockBatchResult result = await ApplyNormalizedAsync(
                [mutation with { Index = 0 }],
                1,
                null,
                ct).ConfigureAwait(false);
            WarehouseStockMutationResult entry = result.Entries[0];
            ThrowIfFailed(entry);
            entries.Add(entry);
        }

        return entries.Select(entry => new WarehouseStockBalance
        {
            StoreAlias = entry.StoreAlias,
            WarehouseKey = entry.WarehouseKey,
            Sku = entry.Sku,
            Balance = entry.Balance!.Value,
            UpdateDate = entry.UpdateDate!.Value,
        }).ToList();
    }

    /// <summary>
    /// Clears a persisted warehouse balance. An already-unset balance is unchanged.
    /// </summary>
    /// <returns><see langword="true"/> when a persisted record was removed.</returns>
    public async Task<bool> ClearAsync(
        string storeAlias,
        Guid warehouseKey,
        string sku,
        CancellationToken ct = default)
    {
        NormalizedMutation mutation = NormalizeMutation(new WarehouseStockMutationRequest
        {
            StoreAlias = storeAlias,
            WarehouseKey = warehouseKey,
            Sku = sku,
            Operation = WarehouseStockMutationOperation.Clear,
        }, 0);

        WarehouseStockBatchResult result = await ApplyNormalizedAsync([mutation], 1, null, ct).ConfigureAwait(false);
        WarehouseStockMutationResult entry = result.Entries[0];
        ThrowIfFailed(entry);
        return entry.Status == WarehouseStockMutationStatus.Cleared;
    }

    /// <summary>
    /// Applies warehouse mutations across stores, warehouses, and SKUs with per-entry failures.
    /// </summary>
    public async Task<WarehouseStockBatchResult> UpdateAsync(
        IEnumerable<WarehouseStockMutationRequest> mutations,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        List<WarehouseStockMutationRequest> requests = mutations.ToList();
        var normalized = new List<NormalizedMutation>(requests.Count);
        var initialResults = new WarehouseStockMutationResult?[requests.Count];
        var identities = new HashSet<WarehouseStockIdentity>();

        for (int index = 0; index < requests.Count; index++)
        {
            WarehouseStockMutationRequest? request = requests[index];
            try
            {
                ArgumentNullException.ThrowIfNull(request);
                NormalizedMutation mutation = NormalizeMutation(request, index);
                var identity = new WarehouseStockIdentity(mutation.StoreAlias.ToUpperInvariant(), mutation.WarehouseKey, mutation.Sku);
                if (!identities.Add(identity))
                {
                    throw new ArgumentException("The batch contains a duplicate warehouse stock identity after normalization.", nameof(mutations));
                }

                normalized.Add(mutation);
            }
            catch (ArgumentException ex)
            {
                initialResults[index] = Failure(index, request, ex);
            }
            catch (InvalidOperationException ex)
            {
                initialResults[index] = Failure(index, request, ex);
            }
        }

        return await ApplyNormalizedAsync(normalized, requests.Count, initialResults, ct).ConfigureAwait(false);
    }

    private async Task<WarehouseStockBatchResult> ApplyNormalizedAsync(
        IReadOnlyList<NormalizedMutation> mutations,
        int resultCount,
        WarehouseStockMutationResult?[]? initialResults,
        CancellationToken ct)
    {
        var results = initialResults ?? new WarehouseStockMutationResult?[resultCount];
        await _warehouseStockCache.MutationLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            _warehouseStockCache.EnsureReady();
            var persistenceRequests = new List<WarehouseStockPersistenceRequest>();
            Dictionary<int, NormalizedMutation> mutationsByIndex = mutations.ToDictionary(mutation => mutation.Index);

            foreach (NormalizedMutation mutation in mutations)
            {
                WarehouseStockData? existing = _warehouseStockCache.Get(
                    mutation.StoreAlias,
                    mutation.WarehouseKey,
                    mutation.Sku);

                if (mutation.Operation == WarehouseStockMutationOperation.Set && existing?.Balance == mutation.Balance)
                {
                    results[mutation.Index] = Success(mutation, WarehouseStockMutationStatus.Unchanged, existing);
                    continue;
                }

                if (mutation.Operation == WarehouseStockMutationOperation.Clear && existing == null)
                {
                    results[mutation.Index] = Success(mutation, WarehouseStockMutationStatus.Unchanged, null);
                    continue;
                }

                persistenceRequests.Add(new WarehouseStockPersistenceRequest(
                    mutation.Index,
                    existing?.StoreAlias ?? mutation.StoreAlias.ToUpperInvariant(),
                    mutation.WarehouseKey,
                    existing?.Sku ?? mutation.Sku,
                    mutation.Operation,
                    mutation.Balance,
                    existing));
            }

            if (persistenceRequests.Count == 0)
            {
                return CreateBatchResult(results);
            }

            WarehouseStockPersistenceBatchResult persistence = await _warehouseStockRepository
                .ApplyAsync(persistenceRequests, ct)
                .ConfigureAwait(false);

            foreach (WarehouseStockPersistenceResult persisted in persistence.Results)
            {
                NormalizedMutation mutation = mutationsByIndex[persisted.Request.Index];
                if (persisted.Exception != null)
                {
                    results[mutation.Index] = Failure(mutation, persisted.Exception);
                    continue;
                }

                if (mutation.Operation == WarehouseStockMutationOperation.Clear)
                {
                    _warehouseStockCache.Remove(mutation.StoreAlias, mutation.WarehouseKey, mutation.Sku);
                    results[mutation.Index] = Success(mutation, WarehouseStockMutationStatus.Cleared, null);
                    continue;
                }

                _warehouseStockCache.AddOrUpdate(persisted.Data!);
                WarehouseStockMutationStatus status = persisted.Request.ExistingData == null
                    ? WarehouseStockMutationStatus.Inserted
                    : WarehouseStockMutationStatus.Updated;
                results[mutation.Index] = Success(mutation, status, persisted.Data);
            }

            if (persistence.WasCanceled)
            {
                ct.ThrowIfCancellationRequested();
                throw new OperationCanceledException(ct);
            }
        }
        finally
        {
            _warehouseStockCache.MutationLock.Release();
        }

        return CreateBatchResult(results);
    }

    private static WarehouseStockBatchResult CreateBatchResult(WarehouseStockMutationResult?[] results)
    {
        return new WarehouseStockBatchResult
        {
            Entries = results
                .Select(result => result ?? throw new InvalidOperationException("Warehouse mutation did not produce a result."))
                .ToList(),
        };
    }

    private NormalizedMutation NormalizeMutation(WarehouseStockMutationRequest request, int index)
    {
        string storeAlias = NormalizeStoreAlias(request.StoreAlias);
        ValidateWarehouseKey(request.WarehouseKey);
        EnsureWarehouseExists(storeAlias, request.WarehouseKey);
        string sku = NormalizeSku(request.Sku);

        if (!Enum.IsDefined(request.Operation))
        {
            throw new ArgumentOutOfRangeException(nameof(request.Operation));
        }

        if (request.Operation == WarehouseStockMutationOperation.Set)
        {
            if (!request.Balance.HasValue)
            {
                throw new ArgumentException("A balance is required for a set operation.", nameof(request));
            }

            ValidateBalance(request.Balance.Value);
        }

        return new NormalizedMutation(index, storeAlias, request.WarehouseKey, sku, request.Operation, request.Balance);
    }

    private static WarehouseStockMutationResult Success(
        NormalizedMutation mutation,
        WarehouseStockMutationStatus status,
        WarehouseStockData? data)
    {
        return new WarehouseStockMutationResult
        {
            Index = mutation.Index,
            StoreAlias = mutation.StoreAlias,
            WarehouseKey = mutation.WarehouseKey,
            Sku = mutation.Sku,
            Status = status,
            Balance = data?.Balance,
            UpdateDate = data?.UpdateDate,
        };
    }

    private static WarehouseStockMutationResult Failure(NormalizedMutation mutation, Exception exception)
    {
        return new WarehouseStockMutationResult
        {
            Index = mutation.Index,
            StoreAlias = mutation.StoreAlias,
            WarehouseKey = mutation.WarehouseKey,
            Sku = mutation.Sku,
            Status = WarehouseStockMutationStatus.Failed,
            Balance = mutation.Balance,
            Error = exception.Message,
            Exception = exception,
        };
    }

    private static WarehouseStockMutationResult Failure(
        int index,
        WarehouseStockMutationRequest? request,
        Exception exception)
    {
        return new WarehouseStockMutationResult
        {
            Index = index,
            StoreAlias = request?.StoreAlias ?? string.Empty,
            WarehouseKey = request?.WarehouseKey ?? Guid.Empty,
            Sku = request?.Sku ?? string.Empty,
            Status = WarehouseStockMutationStatus.Failed,
            Balance = request?.Balance,
            Error = exception.Message,
            Exception = exception,
        };
    }

    private static void ThrowIfFailed(WarehouseStockMutationResult result)
    {
        if (result.Status != WarehouseStockMutationStatus.Failed)
        {
            return;
        }

        if (result.Exception != null)
        {
            ExceptionDispatchInfo.Capture(result.Exception).Throw();
        }

        throw new InvalidOperationException(result.Error ?? "Warehouse stock update failed.");
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

    private sealed record NormalizedMutation(
        int Index,
        string StoreAlias,
        Guid WarehouseKey,
        string Sku,
        WarehouseStockMutationOperation Operation,
        decimal? Balance);

    private readonly record struct WarehouseStockIdentity(string StoreAlias, Guid WarehouseKey, string Sku);
}
