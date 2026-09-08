using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using System.Diagnostics.CodeAnalysis;

namespace Ekom.Repositories;

internal sealed class WarehouseStockRepository : IWarehouseStockRepository
{
    private readonly DatabaseFactory _databaseFactory;

    public WarehouseStockRepository(DatabaseFactory databaseFactory)
    {
        _databaseFactory = databaseFactory;
    }

    public async Task<List<WarehouseStockData>> GetAllAsync(CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();
        return await db.WarehouseStockData.ToListAsync(ct).ConfigureAwait(false);
    }

    [SuppressMessage(
        "Design",
        "CA1031:Do not catch general exception types",
        Justification = "Each database mutation must report its own failure and allow later entries to continue.")]
    public async Task<WarehouseStockPersistenceBatchResult> ApplyAsync(
        IReadOnlyList<WarehouseStockPersistenceRequest> requests,
        CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();
        var results = new List<WarehouseStockPersistenceResult>(requests.Count);

        foreach (WarehouseStockPersistenceRequest request in requests)
        {
            try
            {
                if (request.Operation == WarehouseStockMutationOperation.Clear)
                {
                    string normalizedStoreAlias = request.StoreAlias.Trim().ToUpperInvariant();
                    string normalizedSku = request.Sku.Trim().ToUpperInvariant();
                    await db.WarehouseStockData
                        .Where(item => item.WarehouseKey == request.WarehouseKey
                            && item.StoreAlias.ToUpper() == normalizedStoreAlias
                            && item.Sku.ToUpper() == normalizedSku)
                        .DeleteAsync(ct)
                        .ConfigureAwait(false);
                    results.Add(new WarehouseStockPersistenceResult(request, null, null));
                    continue;
                }

                DateTime now = DateTime.UtcNow;
                decimal balance = request.Balance.GetValueOrDefault();
                await db.WarehouseStockData.InsertOrUpdateAsync(
                        () => new WarehouseStockData
                        {
                            StoreAlias = request.StoreAlias,
                            WarehouseKey = request.WarehouseKey,
                            Sku = request.Sku,
                            Balance = balance,
                            CreateDate = now,
                            UpdateDate = now,
                        },
                        _ => new WarehouseStockData
                        {
                            Balance = balance,
                            UpdateDate = now,
                        },
                        token: ct)
                    .ConfigureAwait(false);

                results.Add(new WarehouseStockPersistenceResult(
                    request,
                    new WarehouseStockData
                    {
                        StoreAlias = request.StoreAlias,
                        WarehouseKey = request.WarehouseKey,
                        Sku = request.Sku,
                        Balance = balance,
                        CreateDate = request.ExistingData?.CreateDate ?? now,
                        UpdateDate = now,
                    },
                    null));
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                return new WarehouseStockPersistenceBatchResult(results, true);
            }
            catch (Exception ex)
            {
                results.Add(new WarehouseStockPersistenceResult(request, null, ex));
            }
        }

        return new WarehouseStockPersistenceBatchResult(results, false);
    }
}

internal sealed record WarehouseStockPersistenceRequest(
    int Index,
    string StoreAlias,
    Guid WarehouseKey,
    string Sku,
    WarehouseStockMutationOperation Operation,
    decimal? Balance,
    WarehouseStockData? ExistingData);

internal sealed record WarehouseStockPersistenceResult(
    WarehouseStockPersistenceRequest Request,
    WarehouseStockData? Data,
    Exception? Exception);

internal sealed record WarehouseStockPersistenceBatchResult(
    IReadOnlyList<WarehouseStockPersistenceResult> Results,
    bool WasCanceled);
