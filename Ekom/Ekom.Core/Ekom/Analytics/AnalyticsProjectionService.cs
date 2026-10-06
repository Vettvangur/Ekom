using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Data;

namespace Ekom.Analytics;

public sealed record AnalyticsRefreshResult(Guid OrderId, bool Success, bool Removed, string? Error);

public sealed class AnalyticsProjectionService
{
    // Fixed stripes prevent an unbounded per-order semaphore cache. Database locks below
    // remain authoritative across application instances.
    private static readonly SemaphoreSlim[] OrderGates = Enumerable.Range(0, 256).Select(_ => new SemaphoreSlim(1, 1)).ToArray();
    private readonly DatabaseFactory _databaseFactory;
    private readonly AnalyticsSchema _schema;
    private readonly AnalyticsSnapshotMapper _mapper;
    private readonly IOptions<AnalyticsOptions> _options;
    private readonly ActivityLogRepository _activityLog;
    private readonly ILogger<AnalyticsProjectionService> _logger;

    public AnalyticsProjectionService(DatabaseFactory databaseFactory, AnalyticsSchema schema, AnalyticsSnapshotMapper mapper,
        IOptions<AnalyticsOptions> options, ActivityLogRepository activityLog, ILogger<AnalyticsProjectionService> logger)
    {
        _databaseFactory = databaseFactory;
        _schema = schema;
        _mapper = mapper;
        _options = options;
        _activityLog = activityLog;
        _logger = logger;
    }

    public async Task<AnalyticsRefreshResult> RefreshAsync(Guid orderId, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!_options.Value.Enabled) return new(orderId, false, false, "Analytics is disabled.");
        if (!_options.Value.IsValid(out _)) return new(orderId, false, false, "Analytics configuration is invalid.");

        var gate = OrderGates[(int)((uint)orderId.GetHashCode() % (uint)OrderGates.Length)];
        await gate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (!await _schema.EnsureReadyAsync(ct).ConfigureAwait(false))
            {
                return await FailureAsync(orderId, "Analytics storage is unavailable.").ConfigureAwait(false);
            }

            await using var db = _databaseFactory.GetDatabase();
            db.CommandTimeout = _options.Value.DatabaseCommandTimeoutSeconds;
            // SQL Server's transaction-owned application lock is analytics-only. ReadCommitted
            // ensures the source read does not retain operational-order locks until commit.
            await using var transaction = await db.BeginTransactionAsync(
                _databaseFactory.IsSqlite ? IsolationLevel.Serializable : IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
            if (_databaseFactory.IsSqlServer)
            {
                int lockResult = await db.ExecuteAsync<int>(
                    "DECLARE @result int; EXEC @result = sys.sp_getapplock @Resource = @resource, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = @timeout; SELECT @result;",
                    ct, new DataParameter("resource", $"Ekom.Analytics.Order.{orderId:N}"),
                    new DataParameter("timeout", checked(_options.Value.DatabaseCommandTimeoutSeconds * 1000))).ConfigureAwait(false);
                if (lockResult < 0) throw new InvalidOperationException("Analytics order lock could not be acquired.");
            }
            else
            {
                // Acquire SQLite's single-writer lock before reading the current source snapshot,
                // even when this order has no analytics row yet. No operational row is written.
                await db.ExecuteAsync("UPDATE [EkomAnalyticsOrders] SET [OrderId] = [OrderId] WHERE [OrderId] = @orderId",
                    ct, new DataParameter("orderId", orderId)).ConfigureAwait(false);
            }

            OrderData? source = await db.GetTable<OrderData>().FirstOrDefaultAsync(x => x.UniqueId == orderId, ct).ConfigureAwait(false);
            bool removed = source == null || string.Equals(source.OrderStatusCol, nameof(OrderStatus.Incomplete), StringComparison.OrdinalIgnoreCase);
            // Build and validate every row before touching the previous projection.
            AnalyticsProjection? projection = removed ? null : _mapper.Map(source!, DateTime.UtcNow);

            await db.GetTable<AnalyticsPromotionData>().Where(x => x.OrderId == orderId).DeleteAsync(ct).ConfigureAwait(false);
            await db.GetTable<AnalyticsOrderLineData>().Where(x => x.OrderId == orderId).DeleteAsync(ct).ConfigureAwait(false);
            await db.GetTable<AnalyticsOrderData>().Where(x => x.OrderId == orderId).DeleteAsync(ct).ConfigureAwait(false);
            if (projection != null)
            {
                await db.InsertAsync(projection.Order, token: ct).ConfigureAwait(false);
                var bulkOptions = new BulkCopyOptions
                {
                    BulkCopyType = BulkCopyType.MultipleRows,
                    MaxBatchSize = 50,
                };
                if (projection.Lines.Count > 0)
                {
                    await db.BulkCopyAsync(bulkOptions, projection.Lines, ct).ConfigureAwait(false);
                }
                if (projection.Promotions.Count > 0)
                {
                    await db.BulkCopyAsync(bulkOptions, projection.Promotions, ct).ConfigureAwait(false);
                }
            }

            await transaction.CommitAsync(ct).ConfigureAwait(false);
            return new(orderId, true, removed, null);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Analytics projection refresh failed for order {OrderId}.", orderId);
            return await FailureAsync(orderId, "Analytics refresh failed.").ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }

    private async Task<AnalyticsRefreshResult> FailureAsync(Guid orderId, string error)
    {
        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(_options.Value.DatabaseCommandTimeoutSeconds));
            await _activityLog.InsertAsync(new[]
            {
                new OrderActivityLogWrite(orderId, error, "Analytics", DateTime.Now, OrderActivityLogType.Alert),
            }, timeout.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write analytics failure activity for order {OrderId}.", orderId);
        }
        return new(orderId, false, false, error);
    }
}
