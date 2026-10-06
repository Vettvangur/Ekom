using Ekom.Repositories;
using Ekom.Services;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Analytics;

/// <summary>
/// Durable, store-scoped keyset jobs. Rebuilds revisit completed source orders and
/// previously projected incomplete orders; orphan projections require a separate sweep.
/// </summary>
public sealed class AnalyticsJobService
{
    private readonly DatabaseFactory _databaseFactory;
    private readonly AnalyticsSchema _schema;
    private readonly AnalyticsProjectionService _projection;
    private readonly AnalyticsOptions _options;
    private readonly ILogger<AnalyticsJobService> _logger;
    private readonly Guid _owner = Guid.NewGuid();
    private readonly SemaphoreSlim _tickGate = new(1, 1);
    private DateTime _nextScheduleCheckUtc;

    public AnalyticsJobService(DatabaseFactory databaseFactory, AnalyticsSchema schema,
        AnalyticsProjectionService projection, IOptions<AnalyticsOptions> options,
        ILogger<AnalyticsJobService> logger)
    {
        _databaseFactory = databaseFactory;
        _schema = schema;
        _projection = projection;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<AnalyticsJobData> StartRebuildAsync(string store, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(store) || store.Length > 100)
            throw new ArgumentException("A store alias of at most 100 characters is required.", nameof(store));
        if (!await IsReadyAsync(ct).ConfigureAwait(false))
            throw new InvalidOperationException("Analytics is disabled, misconfigured, or its schema is unavailable.");

        var reservation = Guid.NewGuid();
        if (!await AcquireAsync(reservation, ct).ConfigureAwait(false))
            throw new InvalidOperationException("An analytics job already owns the bulk lease.");
        try
        {
            using var db = OpenDatabase();
            if (await ActiveJobs(db).AnyAsync(ct).ConfigureAwait(false))
                throw new InvalidOperationException("An active analytics job already exists. Complete or cancel it first.");
            var job = await CreateJobAsync(db, store, "Rebuild", null, reservation, ct).ConfigureAwait(false);
            return job;
        }
        finally
        {
            await ReleaseAsync(reservation, CancellationToken.None).ConfigureAwait(false);
        }
    }

    public async Task<AnalyticsJobData?> GetAsync(Guid jobId, CancellationToken ct = default)
    {
        if (!await IsReadyAsync(ct).ConfigureAwait(false)) return null;
        using var db = OpenDatabase();
        return await db.GetTable<AnalyticsJobData>().FirstOrDefaultAsync(x => x.JobId == jobId, ct).ConfigureAwait(false);
    }

    /// <summary>Returns the latest 50 jobs for the required store, including scheduled and interrupted jobs.</summary>
    public async Task<IReadOnlyList<AnalyticsJobData>> ListAsync(string store, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(store) || store.Length > 100)
            throw new ArgumentException("A store alias of at most 100 characters is required.", nameof(store));
        if (!await IsReadyAsync(ct).ConfigureAwait(false)) return Array.Empty<AnalyticsJobData>();
        using var db = OpenDatabase();
        return await db.GetTable<AnalyticsJobData>().Where(x => x.StoreAlias == store)
            .OrderByDescending(x => x.StartedAtUtc).ThenByDescending(x => x.JobId)
            .Take(50).ToListAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> ControlAsync(Guid jobId, string command, CancellationToken ct = default)
    {
        if (command is not ("pause" or "resume" or "cancel"))
            throw new ArgumentException("Command must be pause, resume, or cancel.", nameof(command));
        if (!await IsReadyAsync(ct).ConfigureAwait(false)) return false;
        using var db = OpenDatabase();
        var job = await db.GetTable<AnalyticsJobData>().FirstOrDefaultAsync(x => x.JobId == jobId, ct).ConfigureAwait(false);
        if (job == null) return false;

        if (job.Status == "Running")
        {
            if (command == "resume") return false;
            // A cancel cannot be downgraded to pause by a racing request.
            return await db.GetTable<AnalyticsJobData>()
                .Where(x => x.JobId == jobId && x.Status == "Running" && x.Control == job.Control && x.Control != "Cancel")
                .Set(x => x.Control, command == "pause" ? "Pause" : "Cancel")
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow)
                .UpdateAsync(ct).ConfigureAwait(false) == 1;
        }

        var valid = command switch
        {
            "pause" => job.Status == "Pending",
            "resume" => job.Status is "Paused" or "Interrupted",
            "cancel" => job.Status is "Pending" or "Paused" or "Interrupted",
            _ => false,
        };
        if (!valid) return false;

        var reservation = Guid.NewGuid();
        if (!await AcquireAsync(reservation, ct).ConfigureAwait(false)) return false;
        try
        {
            var status = command switch { "pause" => "Paused", "resume" => "Pending", _ => "Cancelled" };
            return await db.GetTable<AnalyticsJobData>()
                .Where(x => x.JobId == jobId && x.Status == job.Status &&
                    db.GetTable<AnalyticsLeaseData>().Any(l => l.Id == 1 && l.Owner == reservation && l.ExpiresAtUtc > LeaseClockUtc()))
                .Set(x => x.Status, status)
                .Set(x => x.Control, (string?)null)
                .Set(x => x.LeaseOwner, (Guid?)null)
                .Set(x => x.LeaseUntilUtc, (DateTime?)null)
                .Set(x => x.UpdatedAtUtc, DateTime.UtcNow)
                .Set(x => x.CompletedAtUtc, command == "cancel" ? DateTime.UtcNow : (DateTime?)null)
                .UpdateAsync(ct).ConfigureAwait(false) == 1;
        }
        finally
        {
            await ReleaseAsync(reservation, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>Processes at most one batch. Interrupted jobs require an explicit resume.</summary>
    public async Task<bool> TickAsync(CancellationToken ct = default)
    {
        if (!await IsReadyAsync(ct).ConfigureAwait(false)) return false;
        await _tickGate.WaitAsync(ct).ConfigureAwait(false);
        AnalyticsJobData? job = null;
        var retainLease = false;
        try
        {
            if (!await AcquireAsync(_owner, ct).ConfigureAwait(false)) return false;
            using var db = OpenDatabase();
            var now = DateTime.UtcNow;
            // Taking an expired lease never silently resumes the former owner's job.
            await db.GetTable<AnalyticsJobData>()
                .Where(x => x.Status == "Running" &&
                    db.GetTable<AnalyticsLeaseData>().Any(l => l.Id == 1 && l.Owner == _owner && l.ExpiresAtUtc > LeaseClockUtc()) &&
                    (x.LeaseOwner != _owner || x.LeaseUntilUtc == null || x.LeaseUntilUtc <= LeaseClockUtc()))
                .Set(x => x.Status, "Interrupted")
                .Set(x => x.UpdatedAtUtc, now)
                .Set(x => x.LeaseOwner, (Guid?)null)
                .Set(x => x.LeaseUntilUtc, (DateTime?)null)
                .UpdateAsync(ct).ConfigureAwait(false);

            job = await ActiveJobs(db).OrderBy(x => x.StartedAtUtc).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (job == null)
            {
                job = await ScheduleDueAsync(db, ct).ConfigureAwait(false);
                if (job == null) return false;
            }
            if (job.Status is "Paused" or "Interrupted") return false;

            if (job.Status == "Pending")
            {
                var changed = await db.GetTable<AnalyticsJobData>()
                    .Where(x => x.JobId == job.JobId && x.Status == "Pending" &&
                        db.GetTable<AnalyticsLeaseData>().Any(l => l.Id == 1 && l.Owner == _owner && l.ExpiresAtUtc > LeaseClockUtc()))
                    .Set(x => x.Status, "Running")
                    .Set(x => x.LeaseOwner, (Guid?)_owner)
                    .Set(x => x.LeaseUntilUtc, x => (DateTime?)LeaseClockUtc().AddSeconds(_options.LeaseDuration.TotalSeconds))
                    .Set(x => x.UpdatedAtUtc, now)
                    .UpdateAsync(ct).ConfigureAwait(false);
                if (changed != 1) return false;
                job.Status = "Running";
                job.LeaseOwner = _owner;
            }

            if (!await RenewAsync(job.JobId, ct).ConfigureAwait(false)) return false;
            if (await ApplyControlAsync(job.JobId, ct).ConfigureAwait(false)) return true;

            retainLease = await ProcessBatchAsync(db, job, ct).ConfigureAwait(false);
            return true;
        }
        catch (Exception exception)
        {
            if (job != null)
            {
                try
                {
                    await FinishAsync(job.JobId, "Interrupted", CancellationToken.None).ConfigureAwait(false);
                }
                catch (Exception cleanupException)
                {
                    _logger.LogWarning(cleanupException, "Could not interrupt analytics job {JobId}; lease expiry will fence it", job.JobId);
                }
            }
            if (exception is OperationCanceledException && ct.IsCancellationRequested) throw;
            _logger.LogError(exception, "Analytics job {JobId} interrupted; explicit resume is required", job?.JobId);
            return false;
        }
        finally
        {
            try
            {
                if (!retainLease) await ReleaseAsync(_owner, CancellationToken.None).ConfigureAwait(false);
            }
            finally
            {
                _tickGate.Release();
            }
        }
    }

    private async Task<bool> IsReadyAsync(CancellationToken ct)
    {
        if (!_options.Enabled) return false;
        if (!_options.IsValid(out var error))
        {
            _logger.LogWarning("Analytics jobs disabled because configuration is invalid: {Error}", error);
            return false;
        }
        return await _schema.EnsureReadyAsync(ct).ConfigureAwait(false);
    }

    // Returns whether the caller should retain the global lease between batches.
    private async Task<bool> ProcessBatchAsync(DbContext db, AnalyticsJobData job, CancellationToken ct)
    {
        using var batchCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        using var heartbeatCts = new CancellationTokenSource();
        var heartbeat = HeartbeatAsync(job.JobId, batchCts, heartbeatCts.Token);
        try
        {
            var candidates = db.OrderData.Where(x => x.StoreAlias == job.StoreAlias &&
                x.ReferenceId > job.LastReferenceId && x.ReferenceId <= job.MaximumReferenceId &&
                (x.OrderStatusCol != nameof(OrderStatus.Incomplete) ||
                    db.GetTable<AnalyticsOrderData>().Any(a => a.OrderId == x.UniqueId)));
            if (job.WindowStart is DateTime windowStart)
                candidates = candidates.Where(x => x.CreateDate >= windowStart || x.UpdateDate >= windowStart || x.PaidDate >= windowStart);
            var orders = await candidates.OrderBy(x => x.ReferenceId)
                .Select(x => new { x.ReferenceId, x.UniqueId })
                .Take(_options.BatchSize).ToListAsync(batchCts.Token).ConfigureAwait(false);
            foreach (var order in orders)
            {
                batchCts.Token.ThrowIfCancellationRequested();
                // The independent heartbeat renews ownership during processing.
                // Avoid two additional lease writes for every projected order;
                // checkpoints still renew and verify ownership before advancing.
                var result = await _projection.RefreshAsync(order.UniqueId, batchCts.Token).ConfigureAwait(false);
                job.ProcessedCount++;
                if (result.Success) job.SucceededCount++;
                else job.FailedCount++;
                if (result.Removed) job.RemovedCount++;
                job.LastReferenceId = order.ReferenceId;
            }
            batchCts.Token.ThrowIfCancellationRequested();
            await SaveCheckpointAsync(db, job, batchCts.Token).ConfigureAwait(false);
            if (await ApplyControlAsync(job.JobId, batchCts.Token).ConfigureAwait(false)) return false;
            if (orders.Count != 0) return true;
            return !await FinishAsync(job.JobId, "Completed", batchCts.Token).ConfigureAwait(false);
        }
        finally
        {
            await heartbeatCts.CancelAsync().ConfigureAwait(false);
            await heartbeat.ConfigureAwait(false);
        }
    }

    private async Task SaveCheckpointAsync(DbContext db, AnalyticsJobData job, CancellationToken ct)
    {
        if (!await RenewAsync(job.JobId, ct).ConfigureAwait(false))
            throw new InvalidOperationException("Analytics bulk lease was lost before checkpoint.");
        var changed = await OwnedJob(db, job.JobId)
            .Set(x => x.LastReferenceId, job.LastReferenceId)
            .Set(x => x.ProcessedCount, job.ProcessedCount)
            .Set(x => x.SucceededCount, job.SucceededCount)
            .Set(x => x.FailedCount, job.FailedCount)
            .Set(x => x.RemovedCount, job.RemovedCount)
            .Set(x => x.UpdatedAtUtc, DateTime.UtcNow)
            .UpdateAsync(ct).ConfigureAwait(false);
        if (changed != 1) throw new InvalidOperationException("Analytics job ownership was lost before checkpoint.");
    }

    internal async Task InterruptOwnedAsync(CancellationToken ct)
    {
        if (!_options.Enabled || !_options.IsValid(out _)) return;
        await _tickGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            using var db = OpenDatabase();
            var jobs = await db.GetTable<AnalyticsJobData>()
                .Where(x => x.Status == "Running" && x.LeaseOwner == _owner)
                .Select(x => x.JobId).ToListAsync(ct).ConfigureAwait(false);
            foreach (var jobId in jobs)
                await FinishAsync(jobId, "Interrupted", ct).ConfigureAwait(false);
            await ReleaseAsync(_owner, ct).ConfigureAwait(false);
        }
        finally
        {
            _tickGate.Release();
        }
    }

    private DbContext OpenDatabase()
    {
        var db = _databaseFactory.GetDatabase();
        db.CommandTimeout = _options.DatabaseCommandTimeoutSeconds;
        return db;
    }

    private static IQueryable<AnalyticsJobData> ActiveJobs(DbContext db) =>
        db.GetTable<AnalyticsJobData>().Where(x => x.Status == "Pending" || x.Status == "Running" ||
            x.Status == "Paused" || x.Status == "Interrupted");

    // Keep UTC expressions inside the SQL predicate: a command that waits for a lock
    // must not renew or checkpoint using a timestamp captured before that wait.
    private IQueryable<AnalyticsJobData> OwnedJob(DbContext db, Guid jobId) =>
        db.GetTable<AnalyticsJobData>().Where(x => x.JobId == jobId && x.Status == "Running" &&
            x.LeaseOwner == _owner && x.LeaseUntilUtc > LeaseClockUtc() &&
            db.GetTable<AnalyticsLeaseData>().Any(l => l.Id == 1 && l.Owner == _owner && l.ExpiresAtUtc > LeaseClockUtc()));

    [Sql.Expression(ProviderName.SqlServer, "SYSUTCDATETIME()", ServerSideOnly = true)]
    [Sql.Expression(ProviderName.SQLite, "strftime('%Y-%m-%d %H:%M:%f', 'now')", ServerSideOnly = true)]
    internal static DateTime LeaseClockUtc() => throw new InvalidOperationException("The lease clock must execute in the database.");

    private async Task<bool> AcquireAsync(Guid owner, CancellationToken ct)
    {
        using var db = OpenDatabase();
        return await db.GetTable<AnalyticsLeaseData>()
            .Where(x => x.Id == 1 && (x.Owner == null || x.ExpiresAtUtc == null || x.ExpiresAtUtc <= LeaseClockUtc() ||
                (x.Owner == owner && x.ExpiresAtUtc > LeaseClockUtc())))
            .Set(x => x.Owner, (Guid?)owner)
            .Set(x => x.ExpiresAtUtc, x => (DateTime?)LeaseClockUtc().AddSeconds(_options.LeaseDuration.TotalSeconds))
            .UpdateAsync(ct).ConfigureAwait(false) == 1;
    }

    private async Task<bool> RenewAsync(Guid jobId, CancellationToken ct)
    {
        using var db = OpenDatabase();
        var changed = await db.GetTable<AnalyticsLeaseData>()
            .Where(x => x.Id == 1 && x.Owner == _owner && x.ExpiresAtUtc > LeaseClockUtc())
            .Set(x => x.ExpiresAtUtc, x => (DateTime?)LeaseClockUtc().AddSeconds(_options.LeaseDuration.TotalSeconds))
            .UpdateAsync(ct).ConfigureAwait(false);
        if (changed != 1) return false;
        return await OwnedJob(db, jobId)
            .Set(x => x.LeaseUntilUtc, x => (DateTime?)LeaseClockUtc().AddSeconds(_options.LeaseDuration.TotalSeconds))
            .UpdateAsync(ct).ConfigureAwait(false) == 1;
    }

    private async Task ReleaseAsync(Guid owner, CancellationToken ct)
    {
        using var db = OpenDatabase();
        await db.GetTable<AnalyticsLeaseData>().Where(x => x.Id == 1 && x.Owner == owner)
            .Set(x => x.Owner, (Guid?)null).Set(x => x.ExpiresAtUtc, (DateTime?)null)
            .UpdateAsync(ct).ConfigureAwait(false);
    }

    private async Task HeartbeatAsync(Guid jobId, CancellationTokenSource batchCts, CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromTicks(Math.Max(1, _options.LeaseDuration.Ticks / 3)));
        try
        {
            while (await timer.WaitForNextTickAsync(ct).ConfigureAwait(false))
            {
                if (await RenewAsync(jobId, ct).ConfigureAwait(false)) continue;
                await batchCts.CancelAsync().ConfigureAwait(false);
                return;
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { }
        catch (Exception exception)
        {
            _logger.LogWarning(exception, "Analytics job {JobId} heartbeat failed", jobId);
            await batchCts.CancelAsync().ConfigureAwait(false);
        }
    }

    private async Task<bool> ApplyControlAsync(Guid jobId, CancellationToken ct)
    {
        using var db = OpenDatabase();
        var job = await OwnedJob(db, jobId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (job?.Control is not ("Pause" or "Cancel")) return false;
        var status = job.Control == "Cancel" ? "Cancelled" : "Paused";
        return await FinishAsync(jobId, status, ct, job.Control).ConfigureAwait(false);
    }

    private async Task<bool> FinishAsync(Guid jobId, string status, CancellationToken ct, string? control = null)
    {
        using var db = OpenDatabase();
        var now = DateTime.UtcNow;
        var query = OwnedJob(db, jobId);
        if (control != null) query = query.Where(x => x.Control == control);
        // If a control arrived after the last read, do not overwrite it with completion.
        if (status == "Completed") query = query.Where(x => x.Control == null);
        return await query.Set(x => x.Status, status)
            .Set(x => x.UpdatedAtUtc, now)
            .Set(x => x.CompletedAtUtc, status == "Completed" || status == "Cancelled" ? (DateTime?)now : null)
            .Set(x => x.LeaseOwner, (Guid?)null)
            .Set(x => x.LeaseUntilUtc, (DateTime?)null)
            .Set(x => x.Control, (string?)null)
            .UpdateAsync(ct).ConfigureAwait(false) == 1;
    }

    private async Task<AnalyticsJobData> CreateJobAsync(DbContext db, string store, string kind, DateTime? windowStart, Guid owner, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var maximum = await db.OrderData.Where(x => x.StoreAlias == store)
            .Select(x => (int?)x.ReferenceId).MaxAsync(ct).ConfigureAwait(false) ?? 0;
        var job = new AnalyticsJobData
        {
            JobId = Guid.NewGuid(),
            StoreAlias = store,
            Kind = kind,
            StartedAtUtc = now,
            UpdatedAtUtc = now,
            MaximumReferenceId = maximum,
            WindowStart = windowStart,
        };
        var inserted = await db.GetTable<AnalyticsLeaseData>()
            .Where(x => x.Id == 1 && x.Owner == owner && x.ExpiresAtUtc > LeaseClockUtc())
            .InsertAsync(db.GetTable<AnalyticsJobData>(), x => new AnalyticsJobData
            {
                JobId = job.JobId,
                StoreAlias = job.StoreAlias,
                Kind = job.Kind,
                Status = job.Status,
                StartedAtUtc = job.StartedAtUtc,
                UpdatedAtUtc = job.UpdatedAtUtc,
                MaximumReferenceId = job.MaximumReferenceId,
                LastReferenceId = 0,
                ProcessedCount = 0,
                SucceededCount = 0,
                FailedCount = 0,
                RemovedCount = 0,
                WindowStart = job.WindowStart,
            }, ct).ConfigureAwait(false);
        if (inserted != 1) throw new InvalidOperationException("Analytics bulk lease was lost before job creation.");
        return job;
    }

    private async Task<AnalyticsJobData?> ScheduleDueAsync(DbContext db, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        if (!_options.ScheduledRefreshEnabled || now < _nextScheduleCheckUtc) return null;
        var aliases = await db.OrderData.Select(x => x.StoreAlias).Distinct().OrderBy(x => x)
            .ToListAsync(ct).ConfigureAwait(false);
        var threshold = now - _options.RefreshInterval;
        foreach (var alias in aliases)
        {
            if (string.IsNullOrWhiteSpace(alias)) continue;
            var recent = await db.GetTable<AnalyticsJobData>()
                .AnyAsync(x => x.StoreAlias == alias && x.Kind == "Scheduled" &&
                    (x.StartedAtUtc >= threshold || x.CompletedAtUtc >= threshold), ct).ConfigureAwait(false);
            if (!recent)
                return await CreateJobAsync(db, alias, "Scheduled", DateTime.Now - _options.LookbackWindow, _owner, ct).ConfigureAwait(false);
        }
        _nextScheduleCheckUtc = now + _options.RefreshInterval;
        return null;
    }
}
