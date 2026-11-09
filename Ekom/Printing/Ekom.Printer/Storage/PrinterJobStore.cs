using Ekom.Printer.Configuration;
using Ekom.Printer.Rendering;
using Ekom.Services;
using LinqToDB;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Ekom.Printer.Storage;

internal sealed class PrinterJobStore(DatabaseFactory databaseFactory)
{
    internal static string GetJobId(PickingSlipContext context, PrintPolicy policy)
    {
        // Serialize components to avoid collisions from delimiters inside store/station names.
        var identity = JsonSerializer.Serialize(new
        {
            context.OrderId,
            Store = context.StoreAlias.ToUpperInvariant(),
            Station = context.StationAlias.ToUpperInvariant(),
            Status = policy == PrintPolicy.OncePerStatus ? (int?)context.TriggerStatus : null,
        });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identity)));
    }

    public async Task<bool> EnqueueAsync(PickingSlipContext context, PickingSlipOptions options,
        CancellationToken cancellationToken = default)
    {
        var job = new PrintJob
        {
            Id = GetJobId(context, options.PrintPolicy),
            OrderId = context.OrderId,
            StationAlias = context.StationAlias,
            Copies = options.Copies,
            CreatedUtc = context.CreatedUtc,
            SnapshotJson = JsonSerializer.Serialize(context),
            State = PrintJobState.PendingRender,
        };
        await using var db = databaseFactory.GetDatabase();
        try
        {
            await db.InsertAsync(job, token: cancellationToken).ConfigureAwait(false);
            return true;
        }
        catch (SqliteException ex) when (ex.SqliteExtendedErrorCode is 1555 or 2067)
        {
            return false;
        }
        catch (SqlException ex) when (ex.Number is 2601 or 2627)
        {
            return false;
        }
    }

    public async Task<IReadOnlyList<PrintJob>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        await using var db = databaseFactory.GetDatabase();
        return await db.GetTable<PrintJob>().Where(x => x.State == PrintJobState.PendingRender)
            .OrderBy(x => x.CreatedUtc).Take(25).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveHtmlAsync(string jobId, string html, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(html)) throw new InvalidOperationException("Picking-slip renderer returned empty HTML.");
        await using var db = databaseFactory.GetDatabase();
        // Concurrent renderers may run, but only the first result becomes the immutable document.
        await db.GetTable<PrintJob>().Where(x => x.Id == jobId && x.State == PrintJobState.PendingRender)
            .Set(x => x.Html, html).Set(x => x.State, PrintJobState.Ready)
            .UpdateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MarkRenderFailedAsync(string jobId, CancellationToken cancellationToken = default)
    {
        await using var db = databaseFactory.GetDatabase();
        await db.GetTable<PrintJob>().Where(x => x.Id == jobId && x.State == PrintJobState.PendingRender)
            .Set(x => x.State, PrintJobState.RenderFailed).Set(x => x.ErrorCode, "HtmlRenderingFailed")
            .UpdateAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task CleanupAsync(PrinterOptions options, DateTime nowUtc, CancellationToken cancellationToken = default)
    {
        var htmlCutoff = nowUtc.AddDays(-options.HtmlRetentionDays);
        var historyCutoff = nowUtc.AddDays(-options.HistoryRetentionDays);
        await using var db = databaseFactory.GetDatabase();
        var terminal = db.GetTable<PrintJob>()
            .Where(x => x.State == PrintJobState.Submitted || x.State == PrintJobState.ConfirmedPrinted || x.State == PrintJobState.Cancelled);
        await terminal.Where(x => x.CompletedUtc < htmlCutoff)
            .Set(x => x.Html, (string?)null).Set(x => x.SnapshotJson, (string?)null)
            .UpdateAsync(cancellationToken).ConfigureAwait(false);
        // Keep a minimal deduplication tombstone after history expires. Deleting it would reprint old orders.
        await terminal.Where(x => x.CompletedUtc < historyCutoff)
            .Set(x => x.OrderId, Guid.Empty).Set(x => x.StationAlias, string.Empty)
            .Set(x => x.ErrorCode, (string?)null).Set(x => x.Copies, 0)
            .UpdateAsync(cancellationToken).ConfigureAwait(false);
    }
}
