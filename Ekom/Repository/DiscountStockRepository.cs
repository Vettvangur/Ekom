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
    class DiscountStockRepository : IDiscountStockRepository
    {
        ILog _log;
        ApplicationContext _appCtx;
        /// <summary>
        /// ctor
        /// </summary>
        /// <param name="config"></param>
        /// <param name="appCtx "></param>
        /// <param name="logFac"></param>
        public DiscountStockRepository(Configuration config, ApplicationContext appCtx, ILogFactory logFac)
        {
            _appCtx = appCtx;
            _log = logFac.GetLogger(typeof(DiscountStockRepository));
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
        public DiscountStockData GetStockByUniqueId(string uniqueId)
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                var stockData = db.FirstOrDefault<DiscountStockData>("WHERE UniqueId = @0", uniqueId);

                return stockData ?? CreateNewStockRecord(uniqueId);
            }
        }

        public DiscountStockData CreateNewStockRecord(string uniqueId)
        {
            var dateNow = DateTime.Now;
            var stockData = new DiscountStockData
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
        public IEnumerable<DiscountStockData> GetAllStock()
        {
            using (var db = _appCtx.DatabaseContext.Database)
            {
                return db.Query<DiscountStockData>("");
            }
        }

        /// <summary>
        /// Increment or decrement stock by the supplied value
        /// </summary>
        /// <param name="uniqueId"></param>
        /// <param name="value">Increment or decrement stock by this value</param>
        /// <exception cref="StockException">
        /// If database and cache are out of sync, throws an exception that contains the value currently stored in database
        /// </exception>
        /// <returns></returns>
        public void Update(string uniqueId, int value)
        {
            // We start pessimistic, checking before attempting update.
            // This also takes care of ensuring a DiscountStockData record exists.
            var stockDataFromRepo = GetStockByUniqueId(uniqueId);

            if (stockDataFromRepo.Stock + value < 0)
            {
                throw new StockException($"Not enough stock available for {uniqueId}.")
                {
                    RepoValue = stockDataFromRepo.Stock,
                };
            }

            stockDataFromRepo.Stock += value;
            stockDataFromRepo.UpdateDate = DateTime.Now;

            // Update stock with locking
            var sql = Umbraco.Core.Persistence.Sql.Builder.Append("BEGIN")
                .Append("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;")
                .Append("SET DEADLOCK_PRIORITY LOW;")
                .Append("DECLARE @status int = 0;")
                .Append("DECLARE @stock int = 0;")
                .Select("TOP 1 @stock = Stock")
                .From(Configuration.DiscountStockTableName)
                .Where("UniqueId = @0", uniqueId)
                .Append("BEGIN TRANSACTION")
                .Append("IF (@stock + @0 >= 0)", value)
                .Append("BEGIN")
                .Append("UPDATE @0", Configuration.DiscountStockTableName)
                .Append("SET Stock = @stock + @0", value)
                .Where("UniqueId = @0", uniqueId)
                .Append("END")
                .Append("ELSE")
                .Append("SET @status = 1")
                .Append("COMMIT TRANSACTION")
                .Append("RETURN @status")
                .Append("END")
            ;

            // Called synchronously and hopefully contained by a locking construct
            using (var db = _appCtx.DatabaseContext.Database)
            {
                var result = db.Single<int>(sql);

                if (result != 0)
                {
                    throw new StockException($"Not enough stock available for {uniqueId}.");
                }
            }
        }
    }
}
