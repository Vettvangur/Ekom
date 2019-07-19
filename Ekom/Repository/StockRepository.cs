using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using Ekom.Services;
using log4net;
using System;
using System.Collections.Generic;
using Umbraco.Core;

namespace Ekom.Repository
{
    /// <summary>
    /// Handles database transactions for <see cref="StockData"/>
    /// </summary>
    class StockRepository : IStockRepository
    {
        readonly ILogger _logger;
        readonly ApplicationContext _appCtx;
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="config"></param>
        /// <param name="appCtx "></param>
        /// <param name="logFac"></param>
        public StockRepository(Configuration config, ApplicationContext appCtx, ILogger log)
        {
            _appCtx = appCtx;
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
        public StockData GetStockByUniqueId(string uniqueId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                var stockData = db.FirstOrDefault<StockData>("WHERE UniqueId = @0", uniqueId);

                return stockData ?? CreateNewStockRecord(uniqueId);
            }
        }

        public StockData CreateNewStockRecord(string uniqueId)
        {
            var dateNow = DateTime.Now;
            var stockData = new StockData
            {
                UniqueId = uniqueId,
                CreateDate = dateNow,
                UpdateDate = dateNow,
            };

            // Run synchronously to ensure that callers can expect a db record present after method runs
            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Insert(stockData);
            }

            return stockData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public IEnumerable<StockData> GetAllStock()
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<StockData>("");
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
        public int Set(string uniqueId, int value, int oldValue)
        {
            var stockDataFromRepo = GetStockByUniqueId(uniqueId);

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
            using (var db = _appCtx.DatabaseContext.Database)
            {
                db.Update(stockDataFromRepo);
                return stockDataFromRepo.Stock;
            }
        }
    }
}
