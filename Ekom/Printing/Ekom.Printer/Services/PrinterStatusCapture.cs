using Ekom.Models;
using Ekom.Printer.Configuration;
using Ekom.Printer.Rendering;
using Ekom.Printer.Storage;
using Ekom.Repositories;
using LinqToDB;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Ekom.Printer.Services;

internal sealed class PrinterStatusCapture(IOptions<PrinterOptions> options, TimeProvider clock) : IOrderStatusPersistenceHandler
{
    // Avoid computed presentation getters; capture the persisted order fields, not a live OrderInfo.
    internal static readonly JsonSerializerOptions SnapshotOptions = new() { IgnoreReadOnlyProperties = true };

    public async Task PersistAsync(DbContext database, OrderData order, string? previousStatus, CancellationToken cancellationToken = default)
    {
        if (!options.Value.Enabled || string.Equals(previousStatus, order.OrderStatusCol, StringComparison.Ordinal)) return;
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var station in options.Value.Stations)
        {
            var slip = station.PickingSlip;
            if (!slip.Enabled || slip.TriggerMode != PrinterTriggerMode.StatusChange || !slip.TriggerStatuses.Contains(order.OrderStatus) ||
                !station.StoreAliases.Contains(order.StoreAlias, StringComparer.OrdinalIgnoreCase)) continue;
            await CaptureAsync(database, order, station, now, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<PickingSlipCaptureResult> CaptureAsync(DbContext database, OrderData order,
        PrinterStationOptions station, DateTime now, CancellationToken cancellationToken)
    {
        var slip = station.PickingSlip;
        var identity = new PickingSlipContext(order.UniqueId, order.OrderNumber, order.StoreAlias,
            station.Alias, order.OrderStatus, now, Array.Empty<PickingSlipLine>());
        var id = PrinterJobStore.GetJobId(identity, slip.PrintPolicy);
        if (await database.GetTable<PrintJob>().AnyAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false) ||
            await database.GetTable<CapturedStatus>().AnyAsync(x => x.Id == id, cancellationToken).ConfigureAwait(false)) return new(id, false);
        await database.InsertAsync(new CapturedStatus
        {
            Id = id,
            StationAlias = station.Alias,
            Status = order.OrderStatus,
            PrintPolicy = slip.PrintPolicy,
            Copies = slip.Copies,
            CreatedUtc = now,
            RetryAfterUtc = now,
            OrderJson = JsonSerializer.Serialize(order, SnapshotOptions),
        }, token: cancellationToken).ConfigureAwait(false);
        return new(id, true);
    }
}
