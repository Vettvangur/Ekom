using Ekom.Printer.Configuration;
using Ekom.Printer.Protocol;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Options;

namespace Ekom.Printer.Storage;

internal sealed class PrinterDeliveryStore(DatabaseFactory databaseFactory, IOptions<PrinterOptions> options, TimeProvider clock)
{
    public async Task<ClaimedPrintJob?> ClaimAsync(string stationAlias, CancellationToken cancellationToken = default)
    {
        await using var db = databaseFactory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var candidates = await db.GetTable<PrintJob>()
            .Where(x => x.StationAlias == stationAlias && x.State == PrintJobState.Ready && x.Html != null)
            .OrderBy(x => x.CreatedUtc).ThenBy(x => x.Id).Take(20).ToListAsync(cancellationToken).ConfigureAwait(false);
        foreach (var job in candidates)
        {
            var changed = await db.GetTable<PrintJob>().Where(x => x.Id == job.Id && x.State == PrintJobState.Ready)
                .Set(x => x.State, PrintJobState.Claimed).UpdateAsync(cancellationToken).ConfigureAwait(false);
            if (changed != 1) continue;
            var now = clock.GetUtcNow().UtcDateTime;
            var delivery = new PrinterDelivery
            {
                JobId = job.Id,
                ClaimToken = Guid.NewGuid(),
                StationAlias = stationAlias,
                ClaimedUtc = now,
                ExpiresUtc = now.AddSeconds(options.Value.ClaimTimeoutSeconds),
            };
            await db.InsertAsync(delivery, token: cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ClaimedPrintJob(job.Id, delivery.ClaimToken, delivery.ExpiresUtc,
                job.Html ?? throw new InvalidOperationException("Ready print job has no HTML."), job.Copies, new HtmlPrintPolicy());
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return null;
    }

    public async Task<bool> AcknowledgeAsync(string stationAlias, string jobId, PrintAcknowledgement acknowledgement,
        CancellationToken cancellationToken = default)
    {
        await using var db = databaseFactory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        var delivery = db.GetTable<PrinterDelivery>().Where(x => x.JobId == jobId && x.StationAlias == stationAlias &&
            x.ClaimToken == acknowledgement.ClaimToken);
        var now = clock.GetUtcNow().UtcDateTime;
        var updated = await delivery.Where(x => x.ReportedUtc == null)
            .Set(x => x.Submitted, acknowledgement.Submitted).Set(x => x.SpoolJobId, acknowledgement.SpoolJobId)
            .Set(x => x.ReportedUtc, now).UpdateAsync(cancellationToken).ConfigureAwait(false);
        if (updated == 0)
        {
            // Lost HTTP responses can be retried, but cannot change the original result.
            var previous = await delivery.FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return previous != null && previous.ReportedUtc != null && previous.Submitted == acknowledgement.Submitted &&
                string.Equals(previous.SpoolJobId, acknowledgement.SpoolJobId, StringComparison.Ordinal);
        }
        var state = acknowledgement.Submitted ? PrintJobState.Submitted : PrintJobState.NeedsReview;
        var changed = await db.GetTable<PrintJob>().Where(x => x.Id == jobId &&
                (x.State == PrintJobState.Claimed || x.State == PrintJobState.NeedsReview))
            .Set(x => x.State, state).Set(x => x.CompletedUtc, acknowledgement.Submitted ? (DateTime?)now : null)
            .Set(x => x.ErrorCode, acknowledgement.Submitted ? null : "AgentReportedUncertainPrint")
            .UpdateAsync(cancellationToken).ConfigureAwait(false);
        if (changed != 1)
        {
            // A reviewed job stays resolved, but accept its late report so the agent can clear
            // its durable journal and continue. The claim token was validated above.
            var resolved = await db.GetTable<PrinterManagerAction>().AnyAsync(x => x.JobId == jobId &&
                (x.Action == "resolve-printed" || x.Action == "resolve-reprint"), cancellationToken).ConfigureAwait(false);
            if (!resolved) return false;
        }
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }

    public async Task ExpireClaimsAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = databaseFactory.GetDatabase();
        await db.GetTable<PrintJob>().Where(job => job.State == PrintJobState.Claimed &&
                db.GetTable<PrinterDelivery>().Any(delivery => delivery.JobId == job.Id && delivery.ExpiresUtc < now))
            .Set(x => x.State, PrintJobState.NeedsReview).Set(x => x.ErrorCode, "ClaimExpired")
            .UpdateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task HeartbeatAsync(string stationAlias, AgentHeartbeat heartbeat, CancellationToken cancellationToken = default)
    {
        await using var db = databaseFactory.GetDatabase();
        await db.InsertOrReplaceAsync(new PrinterStation
        {
            Alias = stationAlias,
            LastSeenUtc = clock.GetUtcNow().UtcDateTime,
            ProtocolVersion = heartbeat.ProtocolVersion,
            AgentVersion = heartbeat.AgentVersion,
            PrinterName = heartbeat.PrinterName,
        }, token: cancellationToken).ConfigureAwait(false);
    }

    public async Task<StationHealth> GetHealthAsync(string stationAlias, CancellationToken cancellationToken = default)
    {
        await using var db = databaseFactory.GetDatabase();
        var station = await db.GetTable<PrinterStation>().FirstOrDefaultAsync(x => x.Alias == stationAlias, cancellationToken)
            .ConfigureAwait(false);
        var online = station != null && station.LastSeenUtc >= clock.GetUtcNow().UtcDateTime.AddSeconds(-options.Value.OfflineAfterSeconds);
        return new StationHealth(stationAlias, station?.LastSeenUtc, online,
            station != null && PrinterProtocol.IsCompatible(station.ProtocolVersion), station?.AgentVersion, station?.PrinterName);
    }

    public async Task CleanupAsync(CancellationToken cancellationToken = default)
    {
        var cutoff = clock.GetUtcNow().UtcDateTime.AddDays(-options.Value.HistoryRetentionDays);
        await using var db = databaseFactory.GetDatabase();
        await db.GetTable<PrinterManagerAction>().Where(x => x.CreatedUtc < cutoff)
            .DeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.GetTable<PrinterDelivery>().Where(delivery => db.GetTable<PrintJob>().Any(job =>
                job.Id == delivery.JobId && (job.State == PrintJobState.Submitted || job.State == PrintJobState.ConfirmedPrinted || job.State == PrintJobState.Cancelled) &&
                job.CompletedUtc < cutoff)).DeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.GetTable<PrinterStation>().Where(x => x.LastSeenUtc < cutoff).DeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
