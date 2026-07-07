using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Ekom.Repositories;

class DiscountStockRepository
{
    readonly ILogger _logger;
    readonly DatabaseFactory _databaseFactory;
    /// <summary>
    /// ctor
    /// </summary>
    public DiscountStockRepository(
        ILogger<DiscountStockRepository> logger,
        DatabaseFactory databaseFactory)
    {
        _logger = logger;
        _databaseFactory = databaseFactory;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="uniqueId">
    /// Expects a value in the format
    /// $"{uniqueId}_{coupon}" for coupon Stock
    /// Discount Guid otherwise
    /// </param>
    /// <returns></returns>
    public async Task<DiscountStockData> GetStockByUniqueIdAsync(string uniqueId)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        DiscountStockData? stockData = await db.DiscountStockData
            .Where(x => x.UniqueId == uniqueId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        return stockData ?? await CreateNewStockRecordAsync(uniqueId).ConfigureAwait(false);
    }

    public async Task<DiscountStockData> CreateNewStockRecordAsync(string uniqueId)
    {
        DateTime dateNow = DateTime.Now;
        DiscountStockData stockData = new DiscountStockData
        {
            UniqueId = uniqueId,
            CreateDate = dateNow,
            UpdateDate = dateNow,
        };

        // Run synchronously to ensure that callers can expect a db record present after method runs
        await using DbContext db = _databaseFactory.GetDatabase();

        await db.InsertAsync(stockData).ConfigureAwait(false);

        return stockData;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    public async Task<IEnumerable<DiscountStockData>> GetAllStockAsync()
    {
        await using DbContext db = _databaseFactory.GetDatabase();
        List<DiscountStockData> data = await db.DiscountStockData.ToListAsync()
            .ConfigureAwait(false);
        return data;
    }

    /// <summary>
    /// Increment or decrement stock by the supplied value
    /// </summary>
    /// <param name="uniqueId"></param>
    /// <param name="value">Increment or decrement stock by this value</param>
    /// <exception cref="NotEnoughStockException">
    /// If database and cache are out of sync, throws an exception that contains the value currently stored in database
    /// </exception>
    /// <returns></returns>
    public async Task UpdateAsync(string uniqueId, int value)
    {
        // We start pessimistic, checking before attempting update.
        // This also takes care of ensuring a DiscountStockData record exists.
        DiscountStockData stockDataFromRepo = await GetStockByUniqueIdAsync(uniqueId).ConfigureAwait(false);

        if (stockDataFromRepo.Stock + value < 0)
        {
            throw new NotEnoughStockException($"Not enough stock available for {uniqueId}.")
            {
                RepoValue = stockDataFromRepo.Stock,
            };
        }

        await using DbContext db = _databaseFactory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable).ConfigureAwait(false);

        int rows = await db.DiscountStockData
            .Where(x => x.UniqueId == uniqueId && x.Stock + value >= 0)
            .Set(x => x.Stock, x => x.Stock + value)
            .Set(x => x.UpdateDate, x => DateTime.Now)
            .UpdateAsync()
            .ConfigureAwait(false);

        if (rows == 0)
        {
            await transaction.RollbackAsync().ConfigureAwait(false);
            throw new NotEnoughStockException($"Not enough stock available for {uniqueId}.");
        }

        await transaction.CommitAsync().ConfigureAwait(false);
    }
}
