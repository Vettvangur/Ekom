using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

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

        await db.InsertAsync(stockData, token: ct).ConfigureAwait(false);

        return stockData;
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
        StockData stockDataFromRepo = await GetStockByUniqueIdAsync(uniqueId, ct).ConfigureAwait(false);

        if (stockDataFromRepo.Stock != oldValue)
        {
            _logger.LogError($"The database and cache are out of sync! OrderLine: " + uniqueId + " Stock Sent it: " + oldValue + " Current DB Stock: " + stockDataFromRepo.Stock);
            //throw new StockException()
            //{
            //    RepoValue = stockDataFromRepo.Stock,
            //};
        }

        stockDataFromRepo.Stock = value;
        stockDataFromRepo.UpdateDate = DateTime.Now;

        // Called synchronously and hopefully contained by a locking construct
        await using DbContext db = _databaseFactory.GetDatabase();
        await db.UpdateAsync(stockDataFromRepo, token: ct).ConfigureAwait(false);
        return stockDataFromRepo.Stock;
    }

    /// <summary>
    /// Rollback scheduled stock reservation.
    /// </summary>
    /// <param name="jobId"></param>
    /// <param name="ct">Cancellation token</param>
    /// <exception cref="StockException"></exception>
    public async Task RollBackJob(string jobId, CancellationToken ct = default)
    {
        await using DbContext db = _databaseFactory.GetDatabase();

        string hangfireArgument = await db.FromSql<string>(
                "SELECT Arguments FROM [HangFire].[Job] WHERE Id = @0 AND StateName = @1",
                jobId,
                "Scheduled"
            )
            .FirstOrDefaultAsync(ct)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(hangfireArgument))
        {
            List<string>? arguments = JsonConvert.DeserializeObject<List<string>>(hangfireArgument);

            Guid key = new Guid(JsonConvert.DeserializeObject<string>(arguments.FirstOrDefault()));
            decimal stock = Convert.ToDecimal(arguments.LastOrDefault());

            await API.Stock.Instance.IncrementStockAsync(key, stock, ct).ConfigureAwait(false);
        }
    }
}
