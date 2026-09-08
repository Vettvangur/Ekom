using Ekom.Models;
using Ekom.Services;
using LinqToDB;

namespace Ekom.Repositories;

internal sealed class WarehouseStockRepository
{
    private readonly DatabaseFactory _databaseFactory;

    public WarehouseStockRepository(DatabaseFactory databaseFactory)
    {
        _databaseFactory = databaseFactory;
    }

    public async Task<WarehouseStockData?> GetAsync(
        string storeAlias,
        Guid warehouseKey,
        string sku,
        CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        return await db.WarehouseStockData
            .FirstOrDefaultAsync(
                x => x.StoreAlias == storeAlias
                    && x.WarehouseKey == warehouseKey
                    && x.Sku == sku,
                ct)
            .ConfigureAwait(false);
    }

    public async Task<List<WarehouseStockData>> GetAsync(
        string storeAlias,
        IReadOnlyCollection<string> skus,
        CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        return await db.WarehouseStockData
            .Where(x => x.StoreAlias == storeAlias && skus.Contains(x.Sku))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<List<WarehouseStockData>> GetAsync(
        string storeAlias,
        Guid warehouseKey,
        IReadOnlyCollection<string> skus,
        CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        return await db.WarehouseStockData
            .Where(x => x.StoreAlias == storeAlias
                && x.WarehouseKey == warehouseKey
                && skus.Contains(x.Sku))
            .ToListAsync(ct)
            .ConfigureAwait(false);
    }

    public async Task<WarehouseStockData> SetAsync(
        string storeAlias,
        Guid warehouseKey,
        string sku,
        decimal balance,
        CancellationToken ct = default)
    {
        DateTime now = DateTime.UtcNow;
        await using DbContext db = _databaseFactory.GetDatabase();

        await db.WarehouseStockData.InsertOrUpdateAsync(
                () => new WarehouseStockData
                {
                    StoreAlias = storeAlias,
                    WarehouseKey = warehouseKey,
                    Sku = sku,
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

        return new WarehouseStockData
        {
            StoreAlias = storeAlias,
            WarehouseKey = warehouseKey,
            Sku = sku,
            Balance = balance,
            CreateDate = now,
            UpdateDate = now,
        };
    }
}
