using Ekom.Models;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.SqlClient;
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
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create tables");
        }

        // Required order index failures must propagate, including during fresh installation.
        EnsureOrderIndexes();
    }

    internal virtual void EnsureOrderIndexes()
    {
        using var db = _databaseFactory.GetDatabase();

        string primaryKeyQuery;
        string validUniqueIndexQuery;
        string namedIndexQuery;
        string createIndexSql;

        if (_databaseFactory.IsSqlServer)
        {
            primaryKeyQuery = @"
SELECT COUNT(*) FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('EkomOrders') AND i.is_primary_key = 1 AND i.is_disabled = 0
AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.key_ordinal > 0) = 1
AND EXISTS (SELECT 1 FROM sys.index_columns k JOIN sys.columns c ON c.object_id = k.object_id AND c.column_id = k.column_id
    WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.key_ordinal = 1 AND c.name = 'ReferenceId');";
            validUniqueIndexQuery = @"
SELECT i.name FROM sys.indexes i
WHERE i.object_id = OBJECT_ID('EkomOrders') AND i.is_unique = 1 AND i.has_filter = 0
AND i.is_disabled = 0 AND i.is_hypothetical = 0 AND i.ignore_dup_key = 0
AND (SELECT COUNT(*) FROM sys.index_columns k WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.key_ordinal > 0) = 1
AND EXISTS (SELECT 1 FROM sys.index_columns k JOIN sys.columns c ON c.object_id = k.object_id AND c.column_id = k.column_id
    WHERE k.object_id = i.object_id AND k.index_id = i.index_id AND k.key_ordinal = 1 AND c.name = 'UniqueId');";
            namedIndexQuery = "SELECT COUNT(*) FROM sys.indexes WHERE object_id = OBJECT_ID('EkomOrders') AND name = 'IX_EkomOrders_UniqueId';";
            createIndexSql = "CREATE UNIQUE NONCLUSTERED INDEX IX_EkomOrders_UniqueId ON EkomOrders (UniqueId);";
        }
        else if (_databaseFactory.IsSqlite)
        {
            primaryKeyQuery = @"
SELECT COUNT(*) FROM pragma_table_info('EkomOrders')
WHERE name = 'ReferenceId' COLLATE NOCASE AND pk = 1
AND (SELECT COUNT(*) FROM pragma_table_info('EkomOrders') WHERE pk > 0) = 1;";
            validUniqueIndexQuery = @"
SELECT i.name FROM pragma_index_list('EkomOrders') i
WHERE i.[unique] = 1 AND i.partial = 0
AND (SELECT COUNT(*) FROM pragma_index_info(i.name)) = 1
AND EXISTS (SELECT 1 FROM pragma_index_info(i.name) k WHERE k.name = 'UniqueId' COLLATE NOCASE);";
            // SQLite index names are database-wide, so also detect a name used on another table.
            namedIndexQuery = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_EkomOrders_UniqueId' COLLATE NOCASE;";
            createIndexSql = "CREATE UNIQUE INDEX IF NOT EXISTS IX_EkomOrders_UniqueId ON EkomOrders (UniqueId);";
        }
        else
        {
            throw new InvalidOperationException("Unsupported database provider for EkomOrders indexes.");
        }

        if (db.Execute<int>(primaryKeyQuery) != 1)
        {
            throw new InvalidOperationException("EkomOrders must have a primary key on ReferenceId. Its existing key layout has not been changed; repair the schema before retrying the migration.");
        }

        // Read the name first so another node creating a valid index cannot look like
        // a conflicting definition merely because our earlier metadata was stale.
        string validCanonicalIndexQuery = $"SELECT COUNT(*) FROM ({validUniqueIndexQuery.TrimEnd(';')}) eligible WHERE name = 'IX_EkomOrders_UniqueId'"
            + (_databaseFactory.IsSqlite ? " COLLATE NOCASE;" : ";");
        bool namedIndexExists = db.Execute<int>(namedIndexQuery) != 0;
        var validIndexes = db.Query<string>(validUniqueIndexQuery).ToList();
        if (namedIndexExists && db.Execute<int>(validCanonicalIndexQuery) == 0)
        {
            throw new InvalidOperationException("IX_EkomOrders_UniqueId exists with an incompatible definition. A full unique index on UniqueId is required; resolve the conflict before retrying the migration.");
        }

        if (validIndexes.Count != 0)
        {
            return;
        }

        if (db.Execute<int>("SELECT COUNT(*) FROM (SELECT UniqueId FROM EkomOrders GROUP BY UniqueId HAVING COUNT(*) > 1) duplicates;") != 0)
        {
            throw new InvalidOperationException("EkomOrders contains duplicate UniqueId values. No orders have been changed; resolve the duplicates before retrying the index migration.");
        }

        try
        {
            db.Execute(createIndexSql);
        }
        catch (SqlException ex) when (ex.Number == 1913)
        {
            // Another startup node may have created the index after our metadata check.
            // Accept only the exact required definition, never an unrelated creation failure.
            if (db.Execute<int>(validCanonicalIndexQuery) == 0)
            {
                throw;
            }
        }

        // SQLite's IF NOT EXISTS can also encounter an index installed by another node.
        if (db.Execute<int>(validCanonicalIndexQuery) == 0)
        {
            throw new InvalidOperationException("The required EkomOrders.UniqueId index could not be verified after creation. Check for an incompatible index definition before retrying the migration.");
        }

        _logger.LogInformation("Ensured required unique index on EkomOrders.UniqueId");
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
        db.CreateTable<CheckoutPaymentAttemptData>(tableOptions: TableOptions.CreateIfNotExists);
        db.CreateTable<CheckoutPaymentOperationData>(tableOptions: TableOptions.CreateIfNotExists);
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
