using Ekom.Models;

namespace Ekom.Repositories;

internal interface IWarehouseStockRepository
{
    Task<List<WarehouseStockData>> GetAllAsync(CancellationToken ct = default);

    Task<WarehouseStockPersistenceBatchResult> ApplyAsync(
        IReadOnlyList<WarehouseStockPersistenceRequest> requests,
        CancellationToken ct = default);
}
