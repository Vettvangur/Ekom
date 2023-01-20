using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Ekom.Repositories
{
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
        public async Task<StockData> GetStockByUniqueIdAsync(string uniqueId)
        {
            using (var db = _databaseFactory.GetDatabase())
            {
                var stockData = await db.StockData
                    .Where(x => x.UniqueId == uniqueId)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                return stockData ?? await CreateNewStockRecordAsync(uniqueId).ConfigureAwait(false);
            }
        }

        public async Task<StockData> CreateNewStockRecordAsync(string uniqueId)
        {
            var dateNow = DateTime.Now;
            var stockData = new StockData
            {
                UniqueId = uniqueId,
                CreateDate = dateNow,
                UpdateDate = dateNow,
            };

            // Run synchronously to ensure that callers can expect a db record present after method runs
            using (var db = _databaseFactory.GetDatabase())
            {
                await db.InsertAsync(stockData).ConfigureAwait(false);
            }

            return stockData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<List<StockData>> GetAllStockAsync()
        {
            using (var db = _databaseFactory.GetDatabase())
            {
                return await db.StockData.ToListAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Increment or decrement stock by the supplied value
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <param name="value"></param>
        /// <param name="oldValue">Old stock value</param>
        /// <exception cref="StockException">
        /// If database and cache are out of sync, throws an exception that contains the value currently stored in database
        /// </exception>
        /// <returns></returns>
        public async Task<int> SetAsync(string uniqueId, int value, int oldValue)
        {
            var stockDataFromRepo = await GetStockByUniqueIdAsync(uniqueId).ConfigureAwait(false);

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
            using (var db = _databaseFactory.GetDatabase())
            {
                await db.UpdateAsync(stockDataFromRepo).ConfigureAwait(false);
                return stockDataFromRepo.Stock;
            }
        }

        /// <summary>
        /// Rollback scheduled stock reservation.
        /// </summary>
        /// <param name="jobId"></param>
        /// <exception cref="StockException"></exception>
        public async Task RollBackJob(string jobId)
        {
            using (var db = _databaseFactory.GetDatabase())
            {
                string hangfireArgument = await db.FromSql<string>(
                    "SELECT Arguments FROM [HangFire].[Job] WHERE Id = @0 AND StateName = @1",
                    jobId,
                    "Scheduled"
                    )
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                if (!string.IsNullOrEmpty(hangfireArgument))
                {
                    var arguments = JsonConvert.DeserializeObject<List<string>>(hangfireArgument);

                    var key = new Guid(JsonConvert.DeserializeObject<string>(arguments.FirstOrDefault()));
                    var stock = Convert.ToInt32(arguments.LastOrDefault());

                    await Ekom.API.Stock.Instance.IncrementStockAsync(key, stock).ConfigureAwait(false);
                }
            }
        }
    }
}
