using Ekom.Printer.Storage;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Data.Sqlite;

namespace Ekom.Printer.Migrations;

/// <summary>Package-owned schema, serialized across website instances.</summary>
internal sealed class PrinterMigration(DatabaseFactory databaseFactory)
{
    // Avoid concurrent schema preparation in the SQLite native provider within this process.
    // Database transactions/application locks still coordinate separate website processes.
    private static readonly SemaphoreSlim SchemaGate = new(1, 1);

    public async Task ApplyAsync(CancellationToken cancellationToken = default)
    {
        await SchemaGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ApplyWithRetryAsync(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            SchemaGate.Release();
        }
    }

    private async Task ApplyWithRetryAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                await ApplyOnceAsync(cancellationToken).ConfigureAwait(false);
                return;
            }
            catch (SqliteException ex) when (attempt < 5 && ex.SqliteErrorCode is 5 or 6)
            {
                // Shared-cache schema locks can return SQLITE_LOCKED without honoring busy_timeout.
                // Retry the whole rolled-back migration, never a partially executed statement.
                await Task.Delay(TimeSpan.FromMilliseconds(50 * (1 << attempt)), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ApplyOnceAsync(CancellationToken cancellationToken)
    {
        await using var db = databaseFactory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        if (databaseFactory.IsSqlServer)
        {
            await db.ExecuteAsync("""
                DECLARE @result int;
                EXEC @result = sp_getapplock @Resource = 'Ekom.Printer.Schema',
                    @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 30000;
                IF @result < 0 THROW 51000, 'Could not acquire Ekom.Printer migration lock', 1;
                """, cancellationToken).ConfigureAwait(false);
        }
        // SQLite serializes schema writes; its busy timeout controls concurrent startup waiting.
        await db.CreateTableAsync<PrinterSchemaVersion>(tableOptions: TableOptions.CreateIfNotExists,
            token: cancellationToken).ConfigureAwait(false);
        var version = await db.GetTable<PrinterSchemaVersion>().FirstOrDefaultAsync(x => x.Id == 1, cancellationToken)
            .ConfigureAwait(false);
        if (version?.Version > 4)
            throw new InvalidOperationException("Ekom.Printer database schema is newer than this package.");
        if (version == null)
        {
            await db.CreateTableAsync<PrintJob>(tableOptions: TableOptions.CreateIfNotExists,
                token: cancellationToken).ConfigureAwait(false);
            await db.ExecuteAsync("CREATE INDEX IX_EkomPrinterJob_State_Created ON EkomPrinterJob (State, CreatedUtc)", cancellationToken)
                .ConfigureAwait(false);
            await db.InsertAsync(new PrinterSchemaVersion { Id = 1, Version = 1 }, token: cancellationToken)
                .ConfigureAwait(false);
        }
        if (version == null || version.Version < 2)
        {
            await db.CreateTableAsync<PrinterDelivery>(tableOptions: TableOptions.CreateIfNotExists,
                token: cancellationToken).ConfigureAwait(false);
            await db.CreateTableAsync<PrinterStation>(tableOptions: TableOptions.CreateIfNotExists,
                token: cancellationToken).ConfigureAwait(false);
            await db.ExecuteAsync("CREATE INDEX IX_EkomPrinterDelivery_Expires ON EkomPrinterDelivery (ExpiresUtc)", cancellationToken)
                .ConfigureAwait(false);
            await db.GetTable<PrinterSchemaVersion>().Where(x => x.Id == 1).Set(x => x.Version, 2)
                .UpdateAsync(cancellationToken).ConfigureAwait(false);
        }
        if (version == null || version.Version < 3)
        {
            await db.CreateTableAsync<CapturedStatus>(tableOptions: TableOptions.CreateIfNotExists,
                token: cancellationToken).ConfigureAwait(false);
            await db.ExecuteAsync("CREATE INDEX IX_EkomPrinterCapturedStatus_Retry ON EkomPrinterCapturedStatus (RetryAfterUtc)", cancellationToken)
                .ConfigureAwait(false);
            await db.GetTable<PrinterSchemaVersion>().Where(x => x.Id == 1).Set(x => x.Version, 3)
                .UpdateAsync(cancellationToken).ConfigureAwait(false);
        }
        if (version == null || version.Version < 4)
        {
            await db.CreateTableAsync<PrinterManagerAction>(tableOptions: TableOptions.CreateIfNotExists,
                token: cancellationToken).ConfigureAwait(false);
            await db.ExecuteAsync("CREATE INDEX IX_EkomPrinterManagerAction_Job ON EkomPrinterManagerAction (JobId, CreatedUtc)", cancellationToken)
                .ConfigureAwait(false);
            await db.GetTable<PrinterSchemaVersion>().Where(x => x.Id == 1).Set(x => x.Version, 4)
                .UpdateAsync(cancellationToken).ConfigureAwait(false);
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
