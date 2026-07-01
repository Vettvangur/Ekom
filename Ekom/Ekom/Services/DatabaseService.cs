using Ekom.Models;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Logging;

namespace Ekom.Services;

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
            using Repositories.DbContext db = _databaseFactory.GetDatabase();

            LinqToDB.SchemaProvider.ISchemaProvider sp = db.DataProvider.GetSchemaProvider();

            LinqToDB.SchemaProvider.DatabaseSchema dbSchema = sp.GetSchema(db);

            if (!dbSchema.Tables.Any(x => x.TableName == "EkomStock"))
            {
                db.CreateTable<StockData>(tableOptions: TableOptions.CreateIfNotExists);
            }

            if (!dbSchema.Tables.Any(x => x.TableName == "EkomOrdersActivityLog"))
            {
                db.CreateTable<OrderActivityLog>(tableOptions: TableOptions.CreateIfNotExists);
            }

            if (!dbSchema.Tables.Any(x => x.TableName == "EkomCoupon"))
            {
                db.CreateTable<CouponData>(tableOptions: TableOptions.CreateIfNotExists);
            }

            if (!dbSchema.Tables.Any(x => x.TableName == Configuration.DiscountStockTableName))
            {
                db.CreateTable<DiscountStockData>(tableOptions: TableOptions.CreateIfNotExists);
            }

            if (!dbSchema.Tables.Any(x => x.TableName == "EkomOrders"))
            {
                db.CreateTable<OrderData>(tableOptions: TableOptions.CreateIfNotExists);

                if (_databaseFactory.IsSqlServer)
                {
                    db.Execute($"ALTER TABLE EkomOrders ALTER COLUMN OrderInfo NVARCHAR(MAX)");
                    db.Execute($"ALTER TABLE [dbo].[EkomOrders] ADD CONSTRAINT [PK_EkomOrders] PRIMARY KEY NONCLUSTERED ([ReferenceId] ASC) WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON, OPTIMIZE_FOR_SEQUENTIAL_KEY = OFF) ON [PRIMARY]");
                    db.Execute($"CREATE UNIQUE NONCLUSTERED INDEX [IX_EkomOrders_UniqueId] ON EkomOrders ( [UniqueId] ASC )WITH (PAD_INDEX = OFF, STATISTICS_NORECOMPUTE = OFF, SORT_IN_TEMPDB = OFF, IGNORE_DUP_KEY = OFF, DROP_EXISTING = OFF, ONLINE = OFF, ALLOW_ROW_LOCKS = ON, ALLOW_PAGE_LOCKS = ON) ON [PRIMARY]");
                }
                else if (_databaseFactory.IsSqlite)
                {
                    db.Execute($"CREATE UNIQUE INDEX IF NOT EXISTS IX_EkomOrders_UniqueId ON EkomOrders (UniqueId)");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tables");
        }

    }

    internal virtual void EnsureOrderActivityLogTypeColumn()
    {
        try
        {
            using Repositories.DbContext db = _databaseFactory.GetDatabase();

            if (_databaseFactory.IsSqlServer)
            {
                db.Execute(@"
IF NOT EXISTS (
    SELECT 1
    FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'EkomOrdersActivityLog'
      AND COLUMN_NAME = 'LogType'
)
BEGIN
    ALTER TABLE [dbo].[EkomOrdersActivityLog]
    ADD [LogType] int NOT NULL
        CONSTRAINT [DF_EkomOrdersActivityLog_LogType] DEFAULT (0);
END");

                return;
            }

            if (_databaseFactory.IsSqlite)
            {
                var hasColumn = db.Execute<int>(@"
SELECT COUNT(1)
FROM pragma_table_info('EkomOrdersActivityLog')
WHERE name = 'LogType';");

                if (hasColumn == 0)
                {
                    db.Execute("ALTER TABLE EkomOrdersActivityLog ADD COLUMN LogType INTEGER NOT NULL DEFAULT 0;");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to ensure activity log type column exists");
        }
    }
}
