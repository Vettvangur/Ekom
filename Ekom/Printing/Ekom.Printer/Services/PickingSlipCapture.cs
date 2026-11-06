using Ekom.Models;
using Ekom.Printer.Configuration;
using Ekom.Services;
using Microsoft.Extensions.Options;
using System.Data;

namespace Ekom.Printer.Services;

internal sealed class PickingSlipCapture(DatabaseFactory databaseFactory, IOptions<PrinterOptions> options,
    TimeProvider clock) : IPickingSlipCapture
{
    public async Task<PickingSlipCaptureResult> CaptureAsync(OrderData savedOrder, string stationAlias,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(savedOrder);
        ArgumentException.ThrowIfNullOrWhiteSpace(stationAlias);
        if (savedOrder.UniqueId == Guid.Empty || string.IsNullOrWhiteSpace(savedOrder.StoreAlias) ||
            string.IsNullOrWhiteSpace(savedOrder.OrderNumber) || !Enum.TryParse<Ekom.Utilities.OrderStatus>(savedOrder.OrderStatusCol, out var status) || !Enum.IsDefined(status))
            throw new ArgumentException("A saved order with a valid identity and status is required.", nameof(savedOrder));
        var station = options.Value.Stations.SingleOrDefault(x => string.Equals(x.Alias, stationAlias, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException("The requested printer station is not configured.");
        if (!options.Value.Enabled || !station.PickingSlip.Enabled)
            throw new InvalidOperationException("Printing is disabled for the requested station. Capture was not accepted.");
        if (station.PickingSlip.TriggerMode != PrinterTriggerMode.ExplicitCapture ||
            station.PickingSlip.PrintPolicy != PrintPolicy.OncePerOrder || station.PickingSlip.TriggerStatuses.Count != 0)
            throw new InvalidOperationException("Explicit capture requires ExplicitCapture mode, OncePerOrder and an empty TriggerStatuses list.");
        if (!station.StoreAliases.Contains(savedOrder.StoreAlias, StringComparer.OrdinalIgnoreCase))
            throw new InvalidOperationException("The order store is not configured for the requested printer station.");

        await using var database = databaseFactory.GetDatabase();
        await using var transaction = await database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken).ConfigureAwait(false);
        var result = await PrinterStatusCapture.CaptureAsync(database, savedOrder, station,
            clock.GetUtcNow().UtcDateTime, cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return result;
    }
}
