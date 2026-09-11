using Ekom.Models;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Logging;

namespace Ekom.Services;

internal class DatabaseService
{
    readonly DatabaseFactory _databaseFactory;
    readonly ILogger<DatabaseService> _logger;
    readonly StockReservationReadiness _reservationReadiness;
    public DatabaseService(DatabaseFactory databaseFactory, ILogger<DatabaseService> logger, StockReservationReadiness reservationReadiness)
    {
        _databaseFactory = databaseFactory;
        _logger = logger;
        _reservationReadiness = reservationReadiness;
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

            EnsureWarehouseStockTable(db, dbSchema);
            EnsureStockReservationTable();

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

    internal virtual void EnsureWarehouseStockTable()
    {
        using Repositories.DbContext db = _databaseFactory.GetDatabase();
        LinqToDB.SchemaProvider.ISchemaProvider sp = db.DataProvider.GetSchemaProvider();
        LinqToDB.SchemaProvider.DatabaseSchema dbSchema = sp.GetSchema(db);

        EnsureWarehouseStockTable(db, dbSchema);
    }

    internal virtual void EnsureStockReservationTable()
    {
        using var db = _databaseFactory.GetDatabase();
        db.CreateTable<StockReservationData>(tableOptions: TableOptions.CreateIfNotExists);
        db.CreateTable<CheckoutStockCompletionData>(tableOptions: TableOptions.CreateIfNotExists);
        db.CreateTable<CheckoutPreparationData>(tableOptions: TableOptions.CreateIfNotExists);
        // Separate idempotent index creation also repairs a partially completed schema setup.
        if (_databaseFactory.IsSqlServer)
        {
            db.Execute(@"
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EkomStockReservation_CreationKey' AND object_id = OBJECT_ID('EkomStockReservation'))
    CREATE UNIQUE INDEX IX_EkomStockReservation_CreationKey ON EkomStockReservation (CreationKey);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EkomStockReservation_Expiry' AND object_id = OBJECT_ID('EkomStockReservation'))
    CREATE INDEX IX_EkomStockReservation_Expiry ON EkomStockReservation (State, ExpiresUtc);
IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_EkomStockReservation_Completed' AND object_id = OBJECT_ID('EkomStockReservation'))
    CREATE INDEX IX_EkomStockReservation_Completed ON EkomStockReservation (CompletedUtc);");
        }
        else
        {
            db.Execute("CREATE UNIQUE INDEX IF NOT EXISTS IX_EkomStockReservation_CreationKey ON EkomStockReservation (CreationKey)");
            db.Execute("CREATE INDEX IF NOT EXISTS IX_EkomStockReservation_Expiry ON EkomStockReservation (State, ExpiresUtc)");
            db.Execute("CREATE INDEX IF NOT EXISTS IX_EkomStockReservation_Completed ON EkomStockReservation (CompletedUtc)");
        }
        // Readiness is set only after all required indexes exist; migration failures propagate.
        _reservationReadiness.SchemaReady();
    }

    private static void EnsureWarehouseStockTable(
        Repositories.DbContext db,
        LinqToDB.SchemaProvider.DatabaseSchema dbSchema)
    {
        if (!dbSchema.Tables.Any(x => x.TableName == "EkomWarehouseStock"))
        {
            db.CreateTable<WarehouseStockData>(tableOptions: TableOptions.CreateIfNotExists);
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
