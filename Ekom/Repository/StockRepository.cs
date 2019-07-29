using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Core.Scoping;

namespace Ekom.Repository
{
    /// <summary>
    /// Handles database transactions for <see cref="StockData"/>
    /// </summary>
    class StockRepository : IStockRepository
    {
        readonly ILogger _logger;
        readonly IScopeProvider _scopeProvider;
        /// <summary>
        /// ctor
        /// </summary>
        public StockRepository(Configuration config, IScopeProvider scopeProvider, ILogger logger)
        {
            _scopeProvider = scopeProvider;
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
            using (var db = _scopeProvider.CreateScope().Database)
            {
                var stockData = await db.FirstOrDefaultAsync<StockData>("WHERE UniqueId = @0", uniqueId)
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
            using (var db = _scopeProvider.CreateScope().Database)
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
            using (var db = _scopeProvider.CreateScope().Database)
            {
                return await db.FetchAsync<StockData>().ConfigureAwait(false);
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
                throw new StockException($"The database and cache are out of sync!")
                {
                    RepoValue = stockDataFromRepo.Stock,
                };
            }

            stockDataFromRepo.Stock = value;
            stockDataFromRepo.UpdateDate = DateTime.Now;

            // Called synchronously and hopefully contained by a locking construct
            using (var db = _scopeProvider.CreateScope().Database)
            {
                await db.UpdateAsync(stockDataFromRepo).ConfigureAwait(false);
                return stockDataFromRepo.Stock;
            }
        }
    }
}
