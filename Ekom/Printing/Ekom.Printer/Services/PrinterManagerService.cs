using Ekom.Models;
using Ekom.Printer.Configuration;
using Ekom.Printer.Storage;
using Ekom.Services;
using LinqToDB;
using Microsoft.Extensions.Options;
using System.Data;
using System.Security.Cryptography;
using System.Text;

namespace Ekom.Printer.Services;

internal sealed record PrinterManagerJob(string Id, string OrderNumber, string StoreAlias, string StationAlias,
    string State, DateTime CreatedUtc, int Copies, bool HasHtml, string? ErrorCode);
internal sealed record PrinterManagerPage(IReadOnlyList<PrinterManagerJob> Jobs, int Total, int Page, int PageSize);
internal sealed record PrinterManagerRequest(string Action, Guid RequestId);

internal sealed class PrinterManagerService(DatabaseFactory factory, IManagerAccessService access,
    ISecurityService security, IOptions<PrinterOptions> options, TimeProvider clock)
{
    public bool CanManage => security.IsCurrentUserAdmin() ||
        security.GetUmbracoUserGroups().Intersect(options.Value.ManagerGroups, StringComparer.OrdinalIgnoreCase).Any();

    public async Task<PrinterManagerPage> ListAsync(string? state, string? station, string? search, int page,
        CancellationToken cancellationToken = default, string? store = null, string? currency = null)
    {
        await using var db = factory.GetDatabase();
        var allowed = access.GetAllowedStoreAliases().ToArray();
        var query = from job in db.GetTable<PrintJob>()
                    join order in db.OrderData on job.OrderId equals order.UniqueId
                    where Enumerable.Contains(allowed, order.StoreAlias)
                    select new { Job = job, order.OrderNumber, order.StoreAlias, order.Currency };
        if (store != null) query = query.Where(x => x.StoreAlias == store);
        if (!string.IsNullOrWhiteSpace(currency))
        {
            var code = currency.Trim().ToUpperInvariant();
            var values = System.Globalization.CultureInfo.GetCultures(System.Globalization.CultureTypes.SpecificCultures)
                .Where(c => new System.Globalization.RegionInfo(c.Name).ISOCurrencySymbol == code)
                .Select(c => c.Name.ToUpperInvariant()).Append(code).ToArray();
            query = query.Where(x => Enumerable.Contains(values, x.Currency.Trim().ToUpper()));
        }
        if (!string.IsNullOrWhiteSpace(station)) query = query.Where(x => x.Job.StationAlias == station);
        if (!string.IsNullOrWhiteSpace(search)) query = query.Where(x => x.OrderNumber.Contains(search));
        if (state == "attention") query = query.Where(x => x.Job.State == PrintJobState.RenderFailed || x.Job.State == PrintJobState.NeedsReview);
        else if (Enum.TryParse<PrintJobState>(state, out var parsed)) query = query.Where(x => x.Job.State == parsed);
        var total = await query.CountAsync(cancellationToken).ConfigureAwait(false);
        var rows = await query.OrderByDescending(x => x.Job.CreatedUtc).ThenBy(x => x.Job.Id)
            .Skip((page - 1) * 25).Take(25)
            .Select(x => new { x.Job.Id, x.OrderNumber, x.StoreAlias, x.Job.StationAlias, x.Job.State,
                x.Job.CreatedUtc, x.Job.Copies, HasHtml = x.Job.Html != null, x.Job.ErrorCode })
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return new PrinterManagerPage(rows.Select(x => new PrinterManagerJob(x.Id, x.OrderNumber, x.StoreAlias,
            x.StationAlias, x.State.ToString(), x.CreatedUtc, x.Copies, x.HasHtml, x.ErrorCode)).ToArray(), total, page, 25);
    }

    public async Task<PrintJob?> FindAsync(string id, CancellationToken cancellationToken = default)
    {
        await using var db = factory.GetDatabase();
        var allowed = access.GetAllowedStoreAliases().ToArray();
        return await (from job in db.GetTable<PrintJob>()
                      join order in db.OrderData on job.OrderId equals order.UniqueId
                      where job.Id == id && Enumerable.Contains(allowed, order.StoreAlias)
                      select job).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<string?> ActAsync(string id, PrinterManagerRequest request, string actor, CancellationToken cancellationToken = default)
    {
        if (!CanManage || await FindAsync(id, cancellationToken).ConfigureAwait(false) == null) return null;
        await using var db = factory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        // Persist the request ID so retrying a lost HTTP response cannot create another reprint.
        var replay = await db.GetTable<PrinterManagerAction>().FirstOrDefaultAsync(x => x.Id == request.RequestId, cancellationToken).ConfigureAwait(false);
        if (replay != null) return replay.JobId == id && replay.Action == request.Action && replay.Actor == actor ? replay.NewJobId ?? id : null;
        var job = await db.GetTable<PrintJob>().SingleAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false);
        string? newId = null;
        var now = clock.GetUtcNow().UtcDateTime;
        int changed;
        switch (request.Action)
        {
            case "retry" when job.State == PrintJobState.RenderFailed && job.SnapshotJson != null:
                changed = await db.GetTable<PrintJob>().Where(x => x.Id == id && x.State == PrintJobState.RenderFailed)
                    .Set(x => x.State, PrintJobState.PendingRender).Set(x => x.ErrorCode, (string?)null)
                    .UpdateAsync(cancellationToken).ConfigureAwait(false);
                if (changed != 1) return null;
                break;
            case "cancel" when job.State is PrintJobState.PendingRender or PrintJobState.Ready or PrintJobState.RenderFailed:
                changed = await db.GetTable<PrintJob>().Where(x => x.Id == id && x.State == job.State)
                    .Set(x => x.State, PrintJobState.Cancelled).Set(x => x.CompletedUtc, now)
                    .UpdateAsync(cancellationToken).ConfigureAwait(false);
                if (changed != 1) return null;
                break;
            case "resolve-printed" when job.State == PrintJobState.NeedsReview:
                changed = await db.GetTable<PrintJob>().Where(x => x.Id == id && x.State == PrintJobState.NeedsReview)
                    .Set(x => x.State, PrintJobState.ConfirmedPrinted).Set(x => x.CompletedUtc, now)
                    .UpdateAsync(cancellationToken).ConfigureAwait(false);
                if (changed != 1) return null;
                break;
            case "reprint" when (job.State is PrintJobState.Submitted or PrintJobState.ConfirmedPrinted) && job.Html != null:
            case "resolve-reprint" when job.State == PrintJobState.NeedsReview && job.Html != null:
                if (request.Action == "resolve-reprint")
                {
                    // Close the uncertain original before queuing its replacement in the same transaction.
                    changed = await db.GetTable<PrintJob>().Where(x => x.Id == id && x.State == PrintJobState.NeedsReview)
                        .Set(x => x.State, PrintJobState.Cancelled).Set(x => x.CompletedUtc, now)
                        .UpdateAsync(cancellationToken).ConfigureAwait(false);
                    if (changed != 1) return null;
                }
                newId = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("reprint:" + request.RequestId.ToString("N"))));
                await db.InsertAsync(new PrintJob
                {
                    Id = newId, OrderId = job.OrderId, StationAlias = job.StationAlias, Copies = job.Copies,
                    State = PrintJobState.Ready, CreatedUtc = now, Html = job.Html, SnapshotJson = job.SnapshotJson,
                }, token: cancellationToken).ConfigureAwait(false);
                break;
            default: return null;
        }
        await db.InsertAsync(new PrinterManagerAction
        {
            Id = request.RequestId, JobId = id, NewJobId = newId, Action = request.Action,
            Actor = actor, CreatedUtc = now,
        }, token: cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return newId ?? id;
    }

    public async Task<object?> HistoryAsync(string id, CancellationToken cancellationToken = default)
    {
        if (await FindAsync(id, cancellationToken).ConfigureAwait(false) == null) return null;
        await using var db = factory.GetDatabase();
        var actions = await db.GetTable<PrinterManagerAction>().Where(x => x.JobId == id || x.NewJobId == id)
            .OrderByDescending(x => x.CreatedUtc).Take(100)
            .Select(x => new { x.Action, x.Actor, x.CreatedUtc, x.NewJobId }).ToListAsync(cancellationToken).ConfigureAwait(false);
        var delivery = await db.GetTable<PrinterDelivery>().Where(x => x.JobId == id)
            .Select(x => new { x.ClaimedUtc, x.ReportedUtc, x.Submitted, x.SpoolJobId }).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return new { actions, delivery };
    }
}
