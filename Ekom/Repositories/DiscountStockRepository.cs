using Ekom.Exceptions;
using Ekom.Models;
using Ekom.Services;
using LinqToDB;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Repositories
{
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
            using (var db = _databaseFactory.GetDatabase())
            {
                var stockData = await db.DiscountStockData
                    .Where(x => x.UniqueId == uniqueId)
                    .FirstOrDefaultAsync()
                    .ConfigureAwait(false);

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
        public async Task<IEnumerable<DiscountStockData>> GetAllStockAsync()
        {
            using (var db = _databaseFactory.GetDatabase())
            {
                var data = await db.DiscountStockData.ToListAsync()
                    .ConfigureAwait(false);
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
            using (var cnn = _databaseFactory.GetSqlConnection())
            using (var command = cnn.CreateCommand())
            {
                var cmdText = new StringBuilder();

                cmdText.AppendLine("BEGIN");

                // Update stock with locking
                cmdText.AppendLine("BEGIN");
                cmdText.AppendLine("SET TRANSACTION ISOLATION LEVEL SERIALIZABLE;");
                cmdText.AppendLine("SET DEADLOCK_PRIORITY LOW;");
                cmdText.AppendLine("DECLARE @status int = 0;");
                cmdText.AppendLine("DECLARE @stock int = 0;");
                cmdText.AppendLine($"SELECT TOP 1 @stock = Stock FROM @DiscountStockTableName ");
                cmdText.AppendLine($"WHERE UniqueId = @{nameof(uniqueId)}");
                cmdText.AppendLine("BEGIN TRANSACTION");
                cmdText.AppendLine("IF (@stock + @0 >= 0)");
                cmdText.AppendLine("BEGIN");
                cmdText.AppendLine("UPDATE @DiscountStockTableName");
                cmdText.AppendLine("SET Stock = @stock + @0");
                cmdText.AppendLine($"WHERE UniqueId = @{nameof(uniqueId)}");
                cmdText.AppendLine("END");
                cmdText.AppendLine("ELSE");
                cmdText.AppendLine("SET @status = 1");
                cmdText.AppendLine("COMMIT TRANSACTION");
                cmdText.AppendLine("RETURN @status");
                cmdText.AppendLine("END");

                command.Parameters.AddWithValue($"@{nameof(uniqueId)}", uniqueId);
                command.Parameters.AddWithValue("@0", value);
                command.Parameters.AddWithValue("@DiscountStockTableName", Configuration.DiscountStockTableName);

                SqlParameter returnValue = new SqlParameter();
                returnValue.Direction = ParameterDirection.ReturnValue;
                command.Parameters.Add(returnValue);

                await command.ExecuteNonQueryAsync().ConfigureAwait(false);

                if ((int)returnValue.Value != 0)
                {
                    throw new NotEnoughStockException($"Not enough stock available for {uniqueId}.");
                }
            }
        }
    }
}
