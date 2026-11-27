using Ekom.Brevo.Models;
using Ekom.Brevo.Services;
using Ekom.Brevo.Webhooks;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using LinqToDB.Data;
using Microsoft.Extensions.Options;
using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ekom.Brevo.Persistence;

internal sealed class BrevoEmailStore(DatabaseFactory factory, IOptions<BrevoOptions> options)
    : IBrevoDeliveryService, IDisposable
{
    private readonly SemaphoreSlim _initializing = new(1, 1);
    private bool _initialized;
    private BrevoDispatcherOptions Limits => options.Value.Transactional.Dispatching;

    internal static string NormalizeStore(string store)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(store);
        if (store.Length > 255) throw new ArgumentException("Store alias exceeds 255 characters.", nameof(store));
        return store.ToUpperInvariant();
    }

    internal static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    internal static string NormalizeMessageId(string value) => value.Trim().Trim('<', '>');

    internal async Task InitializeAsync(CancellationToken ct)
    {
        await _initializing.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initialized) return;
            await using var db = factory.GetDatabase();
            await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            if (factory.IsSqlServer)
                await db.ExecuteAsync("DECLARE @result int; EXEC @result = sp_getapplock @Resource = 'Ekom.Brevo.Schema', @LockMode = 'Exclusive', @LockOwner = 'Transaction'; IF @result < 0 THROW 50000, 'Brevo schema lock failed', 1;").ConfigureAwait(false);
            await db.CreateTableAsync<BrevoLockRow>(tableOptions: TableOptions.CreateIfNotExists, token: ct).ConfigureAwait(false);
            await db.InsertOrReplaceAsync(new BrevoLockRow { Id = 1 }, token: ct).ConfigureAwait(false);
            await db.CreateTableAsync<BrevoEmailRow>(tableOptions: TableOptions.CreateIfNotExists, token: ct).ConfigureAwait(false);
            await db.CreateTableAsync<BrevoEventRow>(tableOptions: TableOptions.CreateIfNotExists, token: ct).ConfigureAwait(false);
            foreach (var index in new[]
            {
                ("IX_EkomBrevoEmail_Due", "EkomBrevoEmail", "State, DueUtc"),
                ("IX_EkomBrevoEmail_Order", "EkomBrevoEmail", "Store, OrderId"),
                ("IX_EkomBrevoEmail_Dedup", "EkomBrevoEmail", "Store, DedupKey"),
                ("IX_EkomBrevoEmail_Message", "EkomBrevoEmail", "Store, MessageId"),
                ("IX_EkomBrevoEvent_Email", "EkomBrevoEvent", "EmailId"),
            })
            {
                string sql = factory.IsSqlServer
                    ? $"IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = '{index.Item1}' AND object_id = OBJECT_ID('{index.Item2}')) CREATE INDEX {index.Item1} ON {index.Item2} ({index.Item3})"
                    : $"CREATE INDEX IF NOT EXISTS {index.Item1} ON {index.Item2} ({index.Item3})";
                await db.ExecuteAsync(sql).ConfigureAwait(false);
            }
            await tx.CommitAsync(ct).ConfigureAwait(false);
            _initialized = true;
        }
        finally { _initializing.Release(); }
    }

    // Always acquire the write lock before any reads, including on SQLite. No network I/O occurs in these transactions.
    private async Task<T> WriteAsync<T>(Func<DbContext, Task<T>> action, CancellationToken ct)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using var db = factory.GetDatabase();
        await using var tx = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
        await db.GetTable<BrevoLockRow>().Where(x => x.Id == 1).Set(x => x.Marker, 0).UpdateAsync(ct).ConfigureAwait(false);
        T result = await action(db).ConfigureAwait(false);
        await tx.CommitAsync(ct).ConfigureAwait(false);
        return result;
    }

    internal Task<BrevoSubmission> EnqueueAsync(BrevoTransactionalMessage message, string id, string payload, CancellationToken ct)
    {
        string store = NormalizeStore(message.StoreAlias);
        if (message.OrderId?.Length > 255 || message.IdempotencyKey?.Length > 1000)
            throw new ArgumentException("OrderId or IdempotencyKey exceeds its length limit.", nameof(message));
        string hash = Hash(JsonSerializer.Serialize(new { Payload = payload, message.OrderId }));
        string? key = string.IsNullOrWhiteSpace(message.IdempotencyKey) ? null : Hash(message.IdempotencyKey);
        var body = JsonNode.Parse(payload)!.AsObject();
        body["headers"] = new JsonObject { ["X-Mailin-custom"] = "ekom-brevo:" + id };
        payload = body.ToJsonString();
        long bytes = Encoding.UTF8.GetByteCount(payload);
        if (bytes > Limits.MaxMessageBytes) throw new ArgumentException("Brevo payload exceeds MaxMessageBytes.", nameof(message));
        return WriteAsync(async db =>
        {
            var table = db.GetTable<BrevoEmailRow>();
            if (key != null)
            {
                var duplicate = await table.FirstOrDefaultAsync(x => x.Store == store && x.DedupKey == key, ct).ConfigureAwait(false);
                if (duplicate != null)
                {
                    if (duplicate.PayloadHash != hash) throw new InvalidOperationException("Brevo idempotency key was already used for different content.");
                    return new BrevoSubmission(BrevoSubmissionStatus.Duplicate, duplicate.Id);
                }
            }
            if (!await HasCapacityAsync(db, bytes, ct).ConfigureAwait(false)) return new BrevoSubmission(BrevoSubmissionStatus.QueueFull, id);
            var now = DateTime.UtcNow;
            await db.InsertAsync(new BrevoEmailRow
            {
                Id = id, Store = store, OrderId = message.OrderId, DedupKey = key, PayloadHash = hash,
                Payload = payload, Bytes = bytes, CreatedUtc = now, UpdatedUtc = now, DueUtc = now,
            }, token: ct).ConfigureAwait(false);
            return new BrevoSubmission(BrevoSubmissionStatus.Queued, id);
        }, ct);
    }

    private async Task<bool> HasCapacityAsync(DbContext db, long bytes, CancellationToken ct)
    {
        var active = db.GetTable<BrevoEmailRow>().Where(x => x.State == BrevoQueueState.Pending || x.State == BrevoQueueState.Sending);
        return await active.CountAsync(ct).ConfigureAwait(false) < Limits.MaxQueueSize
            && (await active.SumAsync(x => (long?)x.Bytes, ct).ConfigureAwait(false) ?? 0) <= Limits.MaxQueueBytes - bytes;
    }

    internal Task<BrevoEmailRow?> ClaimAsync(CancellationToken ct) => WriteAsync(async db =>
    {
        var now = DateTime.UtcNow;
        var table = db.GetTable<BrevoEmailRow>();
        // Expired sends may have reached Brevo. Never automatically replay them.
        await table.Where(x => x.State == BrevoQueueState.Sending && x.LeaseUntilUtc <= now)
            .Set(x => x.State, BrevoQueueState.Unknown).Set(x => x.UpdatedUtc, now).UpdateAsync(ct).ConfigureAwait(false);
        var row = await table.Where(x => x.State == BrevoQueueState.Pending && x.DueUtc <= now)
            .OrderBy(x => x.CreatedUtc).ThenBy(x => x.Id).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (row == null) return null;
        row.State = BrevoQueueState.Sending;
        row.Attempts++;
        row.LeaseId = Guid.NewGuid().ToString("N");
        row.LeaseUntilUtc = now.AddSeconds(options.Value.Transactional.Persistence.LeaseSeconds);
        row.UpdatedUtc = now;
        await db.UpdateAsync(row, token: ct).ConfigureAwait(false);
        return row;
    }, ct);

    internal Task<bool> FinishAsync(BrevoEmailRow work, BrevoDeliveryResult result, DateTime? retryUtc, CancellationToken ct)
        => WriteAsync(async db =>
        {
            var row = await db.GetTable<BrevoEmailRow>().FirstOrDefaultAsync(x => x.Id == work.Id, ct).ConfigureAwait(false);
            if (row == null || row.LeaseId != work.LeaseId
                || row.State is not (BrevoQueueState.Sending or BrevoQueueState.Unknown)) return false;
            row.State = retryUtc != null ? BrevoQueueState.Pending : result.Status switch
            {
                BrevoDeliveryStatus.Accepted => BrevoQueueState.Accepted,
                BrevoDeliveryStatus.Failed => BrevoQueueState.Failed,
                _ => BrevoQueueState.Unknown,
            };
            row.MessageId = result.MessageId == null ? row.MessageId : NormalizeMessageId(result.MessageId);
            row.HttpStatus = result.HttpStatus;
            row.DueUtc = retryUtc ?? row.DueUtc;
            row.UpdatedUtc = DateTime.UtcNow;
            row.LeaseId = null;
            row.LeaseUntilUtc = null;
            if (row.State == BrevoQueueState.Accepted) { row.Payload = ""; row.Bytes = 0; }
            await db.UpdateAsync(row, token: ct).ConfigureAwait(false);
            return true;
        }, ct);

    internal async Task<IReadOnlyList<BrevoEmailRow>> GetPendingCompletionsAsync(string[] ids, CancellationToken ct)
    {
        await InitializeAsync(ct).ConfigureAwait(false);
        await using var db = factory.GetDatabase();
        return await db.GetTable<BrevoEmailRow>().Where(x => Enumerable.Contains(ids, x.Id)
            && x.State != BrevoQueueState.Pending && x.State != BrevoQueueState.Sending).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<BrevoEmailStatus?> GetAsync(string storeAlias, string correlationId, CancellationToken cancellationToken = default)
    {
        string store = NormalizeStore(storeAlias);
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        await using var db = factory.GetDatabase();
        var row = await db.GetTable<BrevoEmailRow>().FirstOrDefaultAsync(x => x.Store == store && x.Id == correlationId, cancellationToken).ConfigureAwait(false);
        return row == null ? null : await SnapshotAsync(db, row, cancellationToken).ConfigureAwait(false);
    }

    public Task<IReadOnlyList<BrevoEmailStatus>> GetByOrderAsync(string storeAlias, string orderId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(orderId);
        return FindAsync(storeAlias, orderId, 100, cancellationToken);
    }

    public Task<IReadOnlyList<BrevoEmailStatus>> GetFailuresAsync(string storeAlias, int take = 100, CancellationToken cancellationToken = default)
    {
        if (take is < 1 or > 100) throw new ArgumentOutOfRangeException(nameof(take));
        return FindAsync(storeAlias, null, take, cancellationToken);
    }

    private async Task<IReadOnlyList<BrevoEmailStatus>> FindAsync(string storeAlias, string? orderId, int take, CancellationToken ct)
    {
        string store = NormalizeStore(storeAlias);
        await InitializeAsync(ct).ConfigureAwait(false);
        await using var db = factory.GetDatabase();
        var query = db.GetTable<BrevoEmailRow>().Where(x => x.Store == store);
        query = orderId == null ? query.Where(x => x.State == BrevoQueueState.Failed || x.State == BrevoQueueState.Unknown)
            : query.Where(x => x.OrderId == orderId);
        var rows = await query.OrderByDescending(x => x.CreatedUtc).Take(take).ToListAsync(ct).ConfigureAwait(false);
        var results = new List<BrevoEmailStatus>();
        foreach (var row in rows) results.Add(await SnapshotAsync(db, row, ct).ConfigureAwait(false));
        return results;
    }

    private static async Task<BrevoEmailStatus> SnapshotAsync(DbContext db, BrevoEmailRow row, CancellationToken ct)
    {
        var events = await db.GetTable<BrevoEventRow>().Where(x => x.EmailId == row.Id)
            .OrderBy(x => x.OccurredUtc).ThenBy(x => x.Id).ToListAsync(ct).ConfigureAwait(false);
        return new(row.Id, row.Store, row.OrderId, row.State, row.Attempts, Utc(row.CreatedUtc), Utc(row.UpdatedUtc),
            row.MessageId, row.HttpStatus, events.Select(x => new BrevoEmailEvent(x.Recipient, x.Event, Utc(x.OccurredUtc), x.Reason)).ToArray());
    }

    public Task<bool> RetryAsync(string storeAlias, string correlationId, CancellationToken cancellationToken = default)
    {
        string store = NormalizeStore(storeAlias);
        return WriteAsync(async db =>
        {
            var row = await db.GetTable<BrevoEmailRow>().FirstOrDefaultAsync(x => x.Store == store && x.Id == correlationId, cancellationToken).ConfigureAwait(false);
            if (row == null || row.State != BrevoQueueState.Failed || row.Payload.Length == 0
                || !await HasCapacityAsync(db, row.Bytes, cancellationToken).ConfigureAwait(false)) return false;
            row.State = BrevoQueueState.Pending;
            row.Attempts = 0;
            row.HttpStatus = null;
            row.DueUtc = row.UpdatedUtc = DateTime.UtcNow;
            await db.UpdateAsync(row, token: cancellationToken).ConfigureAwait(false);
            return true;
        }, cancellationToken);
    }

    internal Task<bool> RecordEventsAsync(string storeAlias, IReadOnlyList<BrevoWebhookEvent> events, CancellationToken ct)
    {
        string store = NormalizeStore(storeAlias);
        return WriteAsync(async db =>
        {
            foreach (var item in events)
            {
                var row = await db.GetTable<BrevoEmailRow>().FirstOrDefaultAsync(x => x.Store == store
                    && (x.MessageId == item.MessageId || (item.CorrelationId != null && x.Id == item.CorrelationId)), ct).ConfigureAwait(false);
                if (row == null) continue; // Other sends in the same Brevo account are outside this outbox.
                string id = Hash(JsonSerializer.Serialize(new { row.Id, item.MessageId, item.Recipient, item.Event, item.OccurredUtc }));
                if (await db.GetTable<BrevoEventRow>().AnyAsync(x => x.Id == id, ct).ConfigureAwait(false)) continue;
                await db.InsertAsync(new BrevoEventRow
                {
                    Id = id, EmailId = row.Id, Recipient = item.Recipient, Event = item.Event,
                    OccurredUtc = item.OccurredUtc, Reason = item.Reason,
                }, token: ct).ConfigureAwait(false);
                // Keep early webhook correlation even if the send response is lost.
                if (row.MessageId == null)
                    await db.GetTable<BrevoEmailRow>().Where(x => x.Id == row.Id)
                        .Set(x => x.MessageId, item.MessageId).UpdateAsync(ct).ConfigureAwait(false);
            }
            return true;
        }, ct);
    }

    internal Task<bool> PurgeAsync(CancellationToken ct) => WriteAsync(async db =>
    {
        var cutoff = DateTime.UtcNow.AddDays(-options.Value.Transactional.Persistence.RetentionDays);
        var expired = db.GetTable<BrevoEmailRow>().Where(x => x.State != BrevoQueueState.Pending
            && x.State != BrevoQueueState.Sending && x.UpdatedUtc < cutoff);
        await db.GetTable<BrevoEventRow>().Where(x => expired.Any(e => e.Id == x.EmailId)).DeleteAsync(ct).ConfigureAwait(false);
        await expired.DeleteAsync(ct).ConfigureAwait(false);
        return true;
    }, ct);

    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    public void Dispose() => _initializing.Dispose();
}
