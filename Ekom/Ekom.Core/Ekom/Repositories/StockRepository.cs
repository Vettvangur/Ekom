using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Logging;
using System.Data;

namespace Ekom.Repositories;

/// <summary>
/// Handles database transactions for <see cref="StockData"/>
/// </summary>
class StockRepository
{
    readonly ILogger _logger;
    readonly DatabaseFactory _databaseFactory;
    /// <summary>
    /// ctor
    /// </summary>
    public StockRepository(DatabaseFactory databaseFactory, ILogger<StockRepository> logger)
    {
        _databaseFactory = databaseFactory;
        _logger = logger;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="uniqueId">
    /// Expects a value in the format
    /// $"{storeAlias}_{uniqueId}" for PerStore Stock
    /// Guid otherwise
    /// </param>
    /// <returns></returns>
    public async Task<StockData> GetStockByUniqueIdAsync(string uniqueId, CancellationToken ct)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        StockData? stockData = await db.StockData
            .Where(x => x.UniqueId == uniqueId)
            .FirstOrDefaultAsync()
            .ConfigureAwait(false);

        return stockData ?? await CreateNewStockRecordAsync(uniqueId, ct).ConfigureAwait(false);
    }

    public async Task<StockData> CreateNewStockRecordAsync(string uniqueId, CancellationToken ct)
    {
        DateTime dateNow = DateTime.Now;
        StockData stockData = new StockData
        {
            UniqueId = uniqueId,
            CreateDate = dateNow,
            UpdateDate = dateNow,
        };

        // Run synchronously to ensure that callers can expect a db record present after method runs
        await using DbContext db = _databaseFactory.GetDatabase();

        return await StockReservationService.RetryAsync(async () =>
        {
            var existing = await db.StockData.FirstOrDefaultAsync(x => x.UniqueId == uniqueId, ct).ConfigureAwait(false);
            if (existing != null) return existing;
            await db.InsertAsync(stockData, token: ct).ConfigureAwait(false);
            return stockData;
        }, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Gets all stock records.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of all stock data</returns>
    public async Task<List<StockData>> GetAllStockAsync(CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        return await db.StockData.ToListAsync(token: ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Increment or decrement stock by the supplied value
    /// </summary>
    /// <param name="uniqueId"></param>
    /// <param name="value"></param>
    /// <param name="oldValue">Old stock value</param>
    /// <param name="ct">Cancellation token</param>
    /// <exception cref="StockException">
    /// If database and cache are out of sync, throws an exception that contains the value currently stored in database
    /// </exception>
    /// <returns></returns>
    public async Task<decimal> SetAsync(string uniqueId, decimal value, decimal oldValue, CancellationToken ct = default)
    {
        await MutateAsync(uniqueId, value, increment: false, ct).ConfigureAwait(false);
        return value;
    }

    /// <summary>Returns the committed mutation's previous value. Never calculates a delta from cache.</summary>
    public Task<decimal> MutateAsync(string uniqueId, decimal value, bool increment, CancellationToken ct = default)
    {
        return StockReservationService.RetryAsync(async () =>
        {
            await using var db = _databaseFactory.GetDatabase();
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            var row = await db.StockData.FirstOrDefaultAsync(x => x.UniqueId == uniqueId, ct).ConfigureAwait(false);
            var oldValue = row?.Stock ?? 0;
            if (row != null && (increment ? value == 0 : value == oldValue))
            {
                await tx.CommitAsync(ct).ConfigureAwait(false);
                return oldValue;
            }
            var now = DateTime.Now;
            if (row == null)
            {
                if (increment && value < 0) throw new NotEnoughStockException($"Not enough stock available for {uniqueId}.");
                await db.InsertAsync(new StockData { UniqueId = uniqueId, Stock = value, CreateDate = now, UpdateDate = now }, token: ct).ConfigureAwait(false);
            }
            else if (increment)
            {
                var affected = await db.StockData.Where(x => x.UniqueId == uniqueId && x.Stock + value >= 0)
                    .Set(x => x.Stock, x => Math.Round(x.Stock + value, 2)).Set(x => x.UpdateDate, now).UpdateAsync(ct).ConfigureAwait(false);
                if (affected != 1) throw new NotEnoughStockException($"Not enough stock available for {uniqueId}.");
            }
            else
            {
                // An explicit set intentionally replaces the SQL balance, including concurrent reservations.
                await db.StockData.Where(x => x.UniqueId == uniqueId).Set(x => x.Stock, value)
                    .Set(x => x.UpdateDate, now).UpdateAsync(ct).ConfigureAwait(false);
            }
            await tx.CommitAsync(ct).ConfigureAwait(false);
            return oldValue;
        }, ct);
    }
}
