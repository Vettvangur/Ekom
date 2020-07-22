using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Models.Data;
using System;
using System.Collections.Generic;
using System.Data;
using System.Threading.Tasks;
using Umbraco.Core.Logging;
using Umbraco.Core.Persistence;
using Umbraco.Core.Scoping;

namespace Ekom.Repository
{
    class DiscountStockRepository : IDiscountStockRepository
    {
        readonly ILogger _logger;
        readonly IScopeProvider _scopeProvider;
        readonly IUmbracoDatabaseFactory _umbracoDatabaseFactory;
        /// <summary>
        /// ctor
        /// </summary>
        public DiscountStockRepository(
            Configuration config,
            IScopeProvider scopeProvider,
            IUmbracoDatabaseFactory umbracoDatabaseFactory,
            ILogger logger)
        {
            _scopeProvider = scopeProvider;
            _logger = logger;
            _umbracoDatabaseFactory = umbracoDatabaseFactory;
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
            using (var scope = _scopeProvider.CreateScope())
            {
                var stockData = await scope.Database.Query<DiscountStockData>()
                    .Where(x => x.UniqueId == uniqueId)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

                scope.Complete();
                return stockData ?? await CreateNewStockRecordAsync(uniqueId).ConfigureAwait(false);
            }
        }

        public async Task<DiscountStockData> CreateNewStockRecordAsync(string uniqueId)
        {
            var dateNow = DateTime.Now;
            var stockData = new DiscountStockData
            {
                UniqueId = uniqueId,
                CreateDate = dateNow,
                UpdateDate = dateNow,
            };

            // Run synchronously to ensure that callers can expect a db record present after method runs
            using (var db = _scopeProvider.CreateScope())
            {
                await db.Database.InsertAsync(stockData).ConfigureAwait(false);
                db.Complete();
            }

            return stockData;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task<IEnumerable<DiscountStockData>> GetAllStockAsync()
        {
            using (var scope = _scopeProvider.CreateScope())
            {
                var data = await scope.Database.FetchAsync<DiscountStockData>()
                    .ConfigureAwait(false);
                scope.Complete();
                return data;
            }
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
            var stockDataFromRepo = await GetStockByUniqueIdAsync(uniqueId).ConfigureAwait(false);

            if (stockDataFromRepo.Stock + value < 0)
            {
                throw new NotEnoughStockException($"Not enough stock available for {uniqueId}.")
                {
                    RepoValue = stockDataFromRepo.Stock,
                };
            }

            stockDataFromRepo.Stock += value;
            stockDataFromRepo.UpdateDate = DateTime.Now;

            // Called synchronously and hopefully contained by a locking construct
            using (var db = _umbracoDatabaseFactory.CreateDatabase())
            {
                // Update stock with locking
                var sql = db.SqlContext.Sql()
                    .Append("BEGIN")
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

                var result = await db.SingleAsync<int>(sql).ConfigureAwait(false);

                if (result != 0)
                {
                    throw new NotEnoughStockException($"Not enough stock available for {uniqueId}.");
                }
            }
        }
    }
}
