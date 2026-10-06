using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Analytics;

public sealed class AnalyticsSchema
{
    private readonly DatabaseFactory _databaseFactory;
    private readonly IOptions<AnalyticsOptions> _options;
    private readonly ILogger<AnalyticsSchema> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private volatile bool _ready;

    public AnalyticsSchema(DatabaseFactory databaseFactory, IOptions<AnalyticsOptions> options, ILogger<AnalyticsSchema> logger)
    {
        _databaseFactory = databaseFactory;
        _options = options;
        _logger = logger;
    }

    public async Task<bool> EnsureReadyAsync(CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_options.Value.Enabled || !_options.Value.IsValid(out _)) return false;
        if (!_databaseFactory.IsSqlServer && !_databaseFactory.IsSqlite) return false;
        if (_ready) return true;

        await _gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_ready) return true;
            await using var db = _databaseFactory.GetDatabase();
            db.CommandTimeout = _options.Value.DatabaseCommandTimeoutSeconds;
            await db.CreateTableAsync<AnalyticsOrderData>(tableOptions: TableOptions.CreateIfNotExists, token: ct).ConfigureAwait(false);
            await db.CreateTableAsync<AnalyticsOrderLineData>(tableOptions: TableOptions.CreateIfNotExists, token: ct).ConfigureAwait(false);
            await db.CreateTableAsync<AnalyticsPromotionData>(tableOptions: TableOptions.CreateIfNotExists, token: ct).ConfigureAwait(false);
            await db.CreateTableAsync<AnalyticsJobData>(tableOptions: TableOptions.CreateIfNotExists, token: ct).ConfigureAwait(false);
            await db.CreateTableAsync<AnalyticsLeaseData>(tableOptions: TableOptions.CreateIfNotExists, token: ct).ConfigureAwait(false);

            await EnsureIndexAsync(db, "EkomAnalyticsOrders", "IX_EkomAnalyticsOrders_Store_CreateDate", "[StoreAlias], [CreateDate]", ct).ConfigureAwait(false);
            await EnsureIndexAsync(db, "EkomAnalyticsOrders", "IX_EkomAnalyticsOrders_Store_PaidDate", "[StoreAlias], [PaidDate]", ct).ConfigureAwait(false);
            await EnsureIndexAsync(db, "EkomAnalyticsOrders", "IX_EkomAnalyticsOrders_Store_Customer", "[StoreAlias], [CustomerIdentityKey]", ct).ConfigureAwait(false);
            await EnsureIndexAsync(db, "EkomAnalyticsPromotionApplications", "IX_EkomAnalyticsPromotions_OrderId", "[OrderId]", ct).ConfigureAwait(false);
            await EnsureIndexAsync(db, "EkomAnalyticsPromotionApplications", "IX_EkomAnalyticsPromotions_Coupon", "[CouponCode]", ct).ConfigureAwait(false);
            await EnsureIndexAsync(db, "EkomAnalyticsJobs", "IX_EkomAnalyticsJobs_Status", "[Status], [UpdatedAtUtc]", ct).ConfigureAwait(false);

            string leaseSql = _databaseFactory.IsSqlite
                ? "INSERT INTO [EkomAnalyticsLease] ([Id]) SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM [EkomAnalyticsLease] WHERE [Id] = 1)"
                : "INSERT INTO [EkomAnalyticsLease] ([Id]) SELECT 1 WHERE NOT EXISTS (SELECT 1 FROM [EkomAnalyticsLease] WITH (UPDLOCK, HOLDLOCK) WHERE [Id] = 1)";
            await db.ExecuteAsync(leaseSql, ct).ConfigureAwait(false);
            _ready = true;
            return true;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analytics schema initialization failed.");
            return false;
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task<int> EnsureIndexAsync(DataConnection db, string table, string name, string columns, CancellationToken ct)
    {
        // All identifiers are fixed internal constants, never request values.
        string sql = _databaseFactory.IsSqlite
            ? $"CREATE INDEX IF NOT EXISTS [{name}] ON [{table}] ({columns})"
            : $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'{table}') AND name = N'{name}') CREATE INDEX [{name}] ON [{table}] ({columns})";
        return db.ExecuteAsync(sql, ct);
    }
}
