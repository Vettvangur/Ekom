using Ekom.Models;
using Ekom.Printer.Configuration;
using Ekom.Printer.Rendering;
using Ekom.Printer.Services;
using Ekom.Services;
using LinqToDB;
using System.Text.Json;

namespace Ekom.Printer.Storage;

internal sealed class CapturedStatusStore(DatabaseFactory databaseFactory, PrinterJobStore jobs, TimeProvider clock)
{
    public async Task<IReadOnlyList<CapturedStatus>> GetPendingAsync(CancellationToken cancellationToken = default)
    {
        var now = clock.GetUtcNow().UtcDateTime;
        await using var db = databaseFactory.GetDatabase();
        return await db.GetTable<CapturedStatus>().Where(x => x.RetryAfterUtc <= now)
            .OrderBy(x => x.CreatedUtc).ThenBy(x => x.Id).Take(25).ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task MaterializeAsync(CapturedStatus captured, IPickingSlipContextFactory factory, CancellationToken cancellationToken = default)
    {
        var order = JsonSerializer.Deserialize<OrderData>(captured.OrderJson, PrinterStatusCapture.SnapshotOptions)
            ?? throw new InvalidOperationException("Missing captured order.");
        var context = factory.Create(new OrderInfo(order), captured.StationAlias, captured.Status) with { CreatedUtc = captured.CreatedUtc };
        var options = new PickingSlipOptions { PrintPolicy = captured.PrintPolicy, Copies = captured.Copies };
        if (context.TriggerStatus != captured.Status ||
            !string.Equals(PrinterJobStore.GetJobId(context, captured.PrintPolicy), captured.Id, StringComparison.Ordinal))
            throw new InvalidOperationException("Custom snapshot factory changed the captured order, store, station or status identity.");
        // Crash after enqueue is safe: the deterministic job key makes the next attempt a no-op.
        await jobs.EnqueueAsync(context, options, cancellationToken).ConfigureAwait(false);
        await using var db = databaseFactory.GetDatabase();
        await db.GetTable<CapturedStatus>().Where(x => x.Id == captured.Id).DeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeferAsync(string id, CancellationToken cancellationToken = default)
    {
        var retry = clock.GetUtcNow().UtcDateTime.AddMinutes(1);
        await using var db = databaseFactory.GetDatabase();
        await db.GetTable<CapturedStatus>().Where(x => x.Id == id)
            .Set(x => x.RetryAfterUtc, retry).Set(x => x.ErrorCode, "SnapshotCreationFailed")
            .UpdateAsync(cancellationToken).ConfigureAwait(false);
    }
}
