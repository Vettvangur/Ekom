using Ekom.Models;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Logging;

namespace Ekom.Services
{
    internal class DatabaseService
    {
        readonly DatabaseFactory _databaseFactory;
        readonly ILogger<DatabaseService> _logger;
        public DatabaseService(DatabaseFactory databaseFactory, ILogger<DatabaseService> logger)
        {
            _databaseFactory = databaseFactory;
            _logger = logger;
        }

        internal virtual void CreateTables()
        {
            try
            {
                using var db = _databaseFactory.GetDatabase();

                var sp = db.DataProvider.GetSchemaProvider();

                var dbSchema = sp.GetSchema(db);

                if (!dbSchema.Tables.Any(x => x.TableName == "EkomStock"))
                {
                    db.CreateTable<StockData>();
                }

                if (!dbSchema.Tables.Any(x => x.TableName == "EkomOrdersActivityLog"))
                {
                    db.CreateTable<OrderActivityLog>();
                }

                if (!dbSchema.Tables.Any(x => x.TableName == "EkomOrders"))
                {
                    db.CreateTable<OrderData>();

                    db.Execute($"ALTER TABLE EkomOrders ALTER COLUMN OrderInfo NVARCHAR(MAX)");
                    db.Execute($"ALTER TABLE [dbo].[EkomOrders] ADD CONSTRAINT [PK_EkomOrders] PRIMARY KEY NONCLUSTERED ([ReferenceId] ASC) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]");
                    db.Execute($"CREATE UNIQUE NONCLUSTERED INDEX [IX_EkomOrders_UniqueId] ON EkomOrders ( [UniqueId] ASC )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]");
                }

                if (!dbSchema.Tables.Any(x => x.TableName == "EkomCoupon"))
                {
                    db.CreateTable<CouponData>();
                }

                if (!dbSchema.Tables.Any(x => x.TableName == Configuration.DiscountStockTableName))
                {
                    db.CreateTable<DiscountStockData>();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create tables");
            }

        }
    }
}
