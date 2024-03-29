using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using System.Linq.Expressions;

namespace Ekom.Repositories
{
    class OrderRepository
    {
        readonly ILogger _logger;
        readonly Configuration _config;
        readonly IMemoryCache _memoryCache;
        readonly DatabaseFactory _databaseFactory;
        /// <summary>
        /// ctor
        /// </summary>
        public OrderRepository(
            ILogger<OrderRepository> logger,
            Configuration config,
            DatabaseFactory databaseFactory,
            IMemoryCache memoryCache)
        {
            _logger = logger;
            _config = config;
            _databaseFactory = databaseFactory;
            _memoryCache = memoryCache;
        }

        public async Task<OrderData> GetOrderAsync(Guid uniqueId)
        {
            await using var db = _databaseFactory.GetDatabase();
            
            var data = await db.OrderData
                .Where(x => x.UniqueId == uniqueId)
                .SingleOrDefaultAsync()
                .ConfigureAwait(false);

            return data;
            
        }

        public async Task InsertOrderAsync(OrderData orderData)
        {
            await using var db = _databaseFactory.GetDatabase();

            var referenceId = (decimal)await db.InsertWithIdentityAsync(orderData).ConfigureAwait(false);

            orderData.ReferenceId = (int)referenceId;
        }

        public async Task UpdateOrderAsync(OrderData orderData)
        {
            await using var db = _databaseFactory.GetDatabase();
            
            await db.UpdateAsync(orderData).ConfigureAwait(false);
            //Clear cache after update.
            _memoryCache.Remove(orderData.UniqueId);
        }


        public async Task MigrateOrderTableToEkom10()
        {
            try
            {
                await using var db = _databaseFactory.GetDatabase();

                const string insertTempColumnSql = @"
                    DECLARE @result INT = 0; -- default to 0 (false)

                    BEGIN TRANSACTION;

                    IF EXISTS (
                        SELECT 1
                        FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'EkomOrders'
                        AND COLUMN_NAME = 'OrderStatusCol'
                        AND DATA_TYPE = 'int'
                    )
                    BEGIN
                        IF NOT EXISTS (
                            SELECT 1
                            FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_NAME = 'EkomOrders'
                            AND COLUMN_NAME = 'OrderStatusColTemp'
                        )
                        BEGIN
                            ALTER TABLE [dbo].[EkomOrders] ADD [OrderStatusColTemp] [nvarchar](4000) NULL;
                            SET @result = 1; -- set to 1 (true) if the column was created
                        END
                    END

                    COMMIT TRANSACTION;

                    -- Return the result
                    SELECT @result AS Result;";

                var insertTempColumn = await db.ExecuteAsync<int>(insertTempColumnSql);

                int affected1 = 0, affected2 = 0, affected3 = 0, affected4 = 0, affected5 = 0;

                if (insertTempColumn == 1)
                {
                    const string renameAndChangeToNvarcharSql = @"
                        BEGIN TRANSACTION;

                        UPDATE [dbo].[EkomOrders] SET [OrderStatusColTemp] = CAST([OrderStatusCol] AS nvarchar(4000));

                        COMMIT TRANSACTION;";

                    affected1 = await db.ExecuteAsync<int>(renameAndChangeToNvarcharSql);

                    const string changeDataStructureSql = @"
                        BEGIN TRANSACTION;

                        ALTER TABLE [dbo].[EkomOrders] DROP COLUMN [OrderStatusCol];
                        EXEC sp_rename 'dbo.EkomOrders.OrderStatusColTemp', 'OrderStatusCol', 'COLUMN';

                        UPDATE [dbo].[EkomOrders] SET [CustomerId] = 0 WHERE [CustomerId] IS NULL;
                        ALTER TABLE [dbo].[EkomOrders] ALTER COLUMN [CustomerId] [int] NOT NULL;

                        ALTER TABLE [dbo].[EkomOrders] ALTER COLUMN [TotalAmount] [decimal](18, 0) NOT NULL;

                        ALTER TABLE [dbo].[EkomOrders] ALTER COLUMN [CreateDate] [datetime2](7) NOT NULL;
                        ALTER TABLE [dbo].[EkomOrders] ALTER COLUMN [UpdateDate] [datetime2](7) NOT NULL;
                        ALTER TABLE [dbo].[EkomOrders] ALTER COLUMN [PaidDate] [datetime2](7) NULL;

                        COMMIT TRANSACTION;
                        ";

                    affected2 = await db.ExecuteAsync<int>(changeDataStructureSql);

                    const string updateOrderStatusColDataSql = @"BEGIN TRANSACTION;
                    IF EXISTS (
                        SELECT TOP 1 1
                        FROM [dbo].[EkomOrders]
                        WHERE [OrderStatusCol] IN ('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12')
                    )
                    BEGIN
                        UPDATE [dbo].[EkomOrders]
                        SET [OrderStatusCol] = 
                            CASE [OrderStatusCol]
                                WHEN '0' THEN 'Cancelled'
                                WHEN '1' THEN 'Closed'
                                WHEN '2' THEN 'PaymentFailed'
                                WHEN '3' THEN 'Incomplete'
                                WHEN '4' THEN 'OfflinePayment'
                                WHEN '5' THEN 'Pending'
                                WHEN '6' THEN 'ReadyForDispatch'
                                WHEN '7' THEN 'ReadyForDispatchWhenStockArrives'
                                WHEN '12' THEN 'ReadyForPickup'
                                WHEN '8' THEN 'Dispatched'
                                WHEN '9' THEN 'WaitingForPayment'
                                WHEN '10' THEN 'Returned'
                                WHEN '11' THEN 'Wishlist'
                                ELSE CAST([OrderStatusCol] AS nvarchar(4000))
                            END
                        WHERE [OrderStatusCol] IN ('0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '10', '11', '12');
                    END

                    COMMIT TRANSACTION;";

                    affected3 = await db.ExecuteAsync<int>(updateOrderStatusColDataSql);
                }

                const string stockUniqueIdMoreLengthSql = @"BEGIN TRANSACTION;
                        IF EXISTS (
	                        SELECT 1
	                        FROM INFORMATION_SCHEMA.COLUMNS
	                        WHERE TABLE_NAME = 'EkomStock'
	                        AND COLUMN_NAME = 'UniqueId'
	                        AND DATA_TYPE = 'nvarchar'
	                        AND CHARACTER_MAXIMUM_LENGTH = 39
                        )
                        Begin
	                        ALTER TABLE [dbo].[EkomStock] ALTER COLUMN [UniqueId] [Nvarchar](255) NOT NULL;
                        End
                        COMMIT TRANSACTION;";


                affected4 = await db.ExecuteAsync<int>(stockUniqueIdMoreLengthSql);

                const string CouponDateSql = @"BEGIN TRANSACTION;
                        IF NOT EXISTS(
                            SELECT *
                            FROM INFORMATION_SCHEMA.COLUMNS
                        WHERE TABLE_NAME = 'EkomCoupon' AND COLUMN_NAME = 'Date'
                            )
                        BEGIN
                            ALTER TABLE EkomCoupon
                        ADD[Date] DATETIME NOT NULL DEFAULT GETDATE()
                        END
                        COMMIT TRANSACTION;";

                affected5 = await db.ExecuteAsync<int>(CouponDateSql);

                if ((affected2 + affected3 + affected4 + affected5) > 0)
                {
                    _logger.LogInformation("Migrating Ekom Orders from version 8 to 10 finished. Affected lines: " + (affected1 + affected2 + affected3 + affected4 + affected5));
                }

            } catch(Exception ex)
            {
                _logger.LogError(ex, "Failed to run migration script for Order table");
            }
        }
        /// <summary>
        /// Get all Orders with the given OrderStatuses. Optionally filter further by any column.
        /// </summary>
        /// <param name="filter"></param>
        /// <param name="orderStatuses"></param>
        /// <returns></returns>
        public async Task<List<OrderData>> GetStatusOrdersAsync(
            Expression<Func<OrderData, bool>> filter = null,
            params OrderStatus[] orderStatuses
        )
        {
            await using var db = _databaseFactory.GetDatabase();

            var query = db.OrderData
                .Where(x => orderStatuses.Select(y => y.ToString()).Contains(x.OrderStatusCol));

            if (filter != null)
            {
                query = query.Where(filter);
            }

            return await query
                .ToListAsync()
                .ConfigureAwait(false);
        }
    }
}
