using Ekom.Events;
using Ekom.Models;
using Ekom.Models.Shipments;
using Ekom.Repositories;
using LinqToDB;
using Microsoft.Extensions.Logging;
using System.Data;
using System.Globalization;
using System.Text.Json;

namespace Ekom.Services;

internal sealed class OrderShipmentService : IOrderShipments
{
    private readonly DatabaseFactory _factory;
    private readonly ILogger<OrderShipmentService> _logger;

    public OrderShipmentService(DatabaseFactory factory, ILogger<OrderShipmentService> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task<OrderShipment> CreateAsync(NewOrderShipment shipment, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(shipment);
        if (shipment.OrderUniqueId == Guid.Empty)
            throw new ArgumentException("A shipment must belong to an order.", nameof(shipment));

        var now = DateTime.UtcNow;
        var created = shipment.CreatedUtc?.ToUniversalTime() ?? now;
        var row = new OrderShipment
        {
            ShipmentId = shipment.ShipmentId == Guid.Empty ? Guid.NewGuid() : shipment.ShipmentId,
            OrderUniqueId = shipment.OrderUniqueId,
            OrderNumber = shipment.OrderNumber,
            StoreAlias = shipment.StoreAlias,
            Kind = shipment.Kind,
            State = shipment.State,
            ProviderKey = shipment.ProviderKey,
            ProviderName = shipment.ProviderName,
            CarrierAlias = shipment.CarrierAlias,
            AccountReference = shipment.AccountReference,
            ServiceId = shipment.ServiceId,
            ServiceName = shipment.ServiceName,
            TrackingNumber = shipment.TrackingNumber,
            TrackingUrl = shipment.TrackingUrl,
            CarrierShipmentId = shipment.CarrierShipmentId,
            ShipmentReference = shipment.ShipmentReference,
            PickupLocationId = shipment.PickupLocationId,
            PickupLocationName = shipment.PickupLocationName,
            PickupLocationAddress = shipment.PickupLocationAddress,
            PickupLocationPostalCode = shipment.PickupLocationPostalCode,
            PickupLocationCity = shipment.PickupLocationCity,
            Amount = shipment.Amount,
            Currency = shipment.Currency,
            Attempts = shipment.Attempts,
            LastError = Truncate(shipment.LastError, 2000),
            Documents = shipment.Documents,
            Details = shipment.Details is { Count: > 0 } ? JsonSerializer.Serialize(shipment.Details) : null,
            CreatedUtc = created,
            UpdatedUtc = created,
        };
        var entry = new OrderShipmentEvent
        {
            ShipmentId = row.ShipmentId,
            OrderUniqueId = row.OrderUniqueId,
            Type = ShipmentEventType.State,
            StateCol = row.StateCol,
            Message = Truncate(shipment.Message ?? shipment.LastError, 2000),
            Actor = Actor(shipment.Actor, row),
            OccurredUtc = created,
            ReceivedUtc = now,
            IdempotencyKey = $"{row.ShipmentId:N}:created",
        };

        await using (var db = _factory.GetDatabase())
        {
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            if (shipment.Imported)
            {
                // Two requests can open the same old order at once; the shipment is still copied once.
                var copied = await db.OrderShipments.FirstOrDefaultAsync(x => x.ShipmentId == row.ShipmentId, ct).ConfigureAwait(false);
                if (copied != null)
                {
                    await transaction.CommitAsync(ct).ConfigureAwait(false);
                    return copied;
                }
            }
            row.Sequence = Convert.ToInt32(await db.InsertWithIdentityAsync(row, token: ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
            entry.Id = Convert.ToInt32(await db.InsertWithIdentityAsync(entry, token: ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
            if (!shipment.Imported)
            {
                await InsertActivityLogAsync(db, row, entry, null, ct).ConfigureAwait(false);
            }
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }

        _logger.LogInformation(
            "Shipment stored. OrderNumber: {OrderNumber} OrderId: {OrderUniqueId} ShipmentId: {ShipmentId} Carrier: {CarrierAlias} State: {ShipmentState} Imported: {Imported}",
            row.OrderNumber, row.OrderUniqueId, row.ShipmentId, row.CarrierAlias, row.State, shipment.Imported);

        if (!shipment.Imported)
        {
            await OrderEvents.OnShipmentRecordedAsync(this, new ShipmentRecordedEventArgs { Shipment = row, Event = entry }, ct).ConfigureAwait(false);
        }
        return row;
    }

    public async Task<ShipmentRecordResult> RecordAsync(ShipmentUpdate update, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        OrderShipment shipment;
        OrderShipmentEvent entry;
        ShipmentState previousState;
        await using (var db = _factory.GetDatabase())
        {
            await using var transaction = await db.BeginTransactionAsync(IsolationLevel.Serializable, ct).ConfigureAwait(false);
            shipment = await db.OrderShipments.SingleOrDefaultAsync(x => x.ShipmentId == update.ShipmentId, ct).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Shipment {update.ShipmentId} does not exist.");
            previousState = shipment.State;

            var key = string.IsNullOrWhiteSpace(update.IdempotencyKey)
                ? $"{shipment.ShipmentId:N}:{Guid.NewGuid():N}"
                : update.IdempotencyKey;
            var existing = await db.OrderShipmentEvents.FirstOrDefaultAsync(x => x.IdempotencyKey == key, ct).ConfigureAwait(false);
            if (existing != null)
            {
                await transaction.CommitAsync(ct).ConfigureAwait(false);
                return new ShipmentRecordResult(false, shipment, existing);
            }

            var now = DateTime.UtcNow;
            var previousTracking = shipment.TrackingNumber;
            Apply(shipment, update, now);
            await db.UpdateAsync(shipment, token: ct).ConfigureAwait(false);

            entry = new OrderShipmentEvent
            {
                ShipmentId = shipment.ShipmentId,
                OrderUniqueId = shipment.OrderUniqueId,
                Type = update.Type ?? (update.State.HasValue ? ShipmentEventType.State : ShipmentEventType.Note),
                StateCol = shipment.StateCol,
                CarrierStatus = update.CarrierStatus,
                Location = Truncate(update.Location, 500),
                Message = Truncate(update.Message ?? update.Error, 2000),
                Actor = Actor(update.Actor, shipment),
                OccurredUtc = update.OccurredUtc?.ToUniversalTime() ?? now,
                ReceivedUtc = now,
                IdempotencyKey = key,
                RawData = update.RawData,
            };
            entry.Id = Convert.ToInt32(await db.InsertWithIdentityAsync(entry, token: ct).ConfigureAwait(false), CultureInfo.InvariantCulture);
            await InsertActivityLogAsync(db, shipment, entry, previousTracking, ct).ConfigureAwait(false);
            await transaction.CommitAsync(ct).ConfigureAwait(false);
        }

        _logger.Log(entry.Type == ShipmentEventType.State && IsAlert(shipment.State) ? LogLevel.Warning : LogLevel.Information,
            "Shipment {ShipmentEvent}. OrderNumber: {OrderNumber} OrderId: {OrderUniqueId} ShipmentId: {ShipmentId} Carrier: {CarrierAlias} State: {ShipmentState} TrackingNumber: {TrackingNumber}",
            entry.Type, shipment.OrderNumber, shipment.OrderUniqueId, shipment.ShipmentId, shipment.CarrierAlias, shipment.State, shipment.TrackingNumber);

        await OrderEvents.OnShipmentRecordedAsync(this, new ShipmentRecordedEventArgs
        {
            Shipment = shipment,
            Event = entry,
            PreviousState = previousState,
        }, ct).ConfigureAwait(false);

        return new ShipmentRecordResult(true, shipment, entry);
    }

    public async Task<OrderShipment?> GetAsync(Guid shipmentId, CancellationToken ct = default)
    {
        await using var db = _factory.GetDatabase();
        return await db.OrderShipments.SingleOrDefaultAsync(x => x.ShipmentId == shipmentId, ct).ConfigureAwait(false);
    }

    public async Task<OrderShipment?> GetCurrentAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var db = _factory.GetDatabase();
        var rows = await db.OrderShipments.Where(x => x.OrderUniqueId == orderId).ToListAsync(ct).ConfigureAwait(false);
        return Current(rows);
    }

    public async Task<OrderShipmentsView> GetForOrderAsync(Guid orderId, CancellationToken ct = default)
    {
        await using var db = _factory.GetDatabase();
        var shipments = await db.OrderShipments.Where(x => x.OrderUniqueId == orderId).ToListAsync(ct).ConfigureAwait(false);
        var events = await db.OrderShipmentEvents.Where(x => x.OrderUniqueId == orderId).ToListAsync(ct).ConfigureAwait(false);
        return BuildView(shipments, events);
    }

    public async Task<IReadOnlyDictionary<Guid, OrderShipmentView>> GetCurrentShipmentsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(orderIds);
        var result = new Dictionary<Guid, OrderShipmentView>();
        if (orderIds.Count == 0) return result;

        await using var db = _factory.GetDatabase();
        // Batches keep the id lists within what the database accepts in one query.
        foreach (var chunk in orderIds.Distinct().Chunk(500))
        {
            // A list, so the provider translates Contains to SQL on every target framework.
            var batch = chunk.ToList();
            var shipments = await db.OrderShipments.Where(x => batch.Contains(x.OrderUniqueId)).ToListAsync(ct).ConfigureAwait(false);
            foreach (var group in shipments.GroupBy(x => x.OrderUniqueId))
            {
                if (Current(group.ToList()) is { } current)
                {
                    result[group.Key] = ToView(current, Array.Empty<OrderShipmentEvent>());
                }
            }
        }
        return result;
    }

    public async Task<Guid?> FindOrderAsync(string trackingNumberOrShipmentId, CancellationToken ct = default)
    {
        var value = trackingNumberOrShipmentId?.Trim();
        if (string.IsNullOrEmpty(value)) return null;

        await using var db = _factory.GetDatabase();
        if (Guid.TryParse(value, out var shipmentId))
        {
            var byId = await db.OrderShipments.Where(x => x.ShipmentId == shipmentId)
                .Select(x => (Guid?)x.OrderUniqueId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
            if (byId != null) return byId;
        }
        return await db.OrderShipments
            .Where(x => x.TrackingNumber == value || x.CarrierShipmentId == value || x.ShipmentReference == value)
            .OrderByDescending(x => x.Sequence)
            .Select(x => (Guid?)x.OrderUniqueId).FirstOrDefaultAsync(ct).ConfigureAwait(false);
    }

    internal static OrderShipmentsView BuildView(IReadOnlyCollection<OrderShipment> shipments, IReadOnlyCollection<OrderShipmentEvent> events)
    {
        var byShipment = events.ToLookup(x => x.ShipmentId);
        var current = Current(shipments);
        var views = shipments.OrderByDescending(x => x.Sequence)
            .Select(x => ToView(x, byShipment[x.ShipmentId].ToList())).ToList();
        return new OrderShipmentsView(current == null ? null : views.First(x => x.ShipmentId == current.ShipmentId), views);
    }

    private static OrderShipment? Current(IReadOnlyCollection<OrderShipment> shipments)
    {
        var outbound = shipments.Where(x => x.Kind == ShipmentKind.Outbound).OrderByDescending(x => x.Sequence).ToList();
        return outbound.FirstOrDefault(x => x.State != ShipmentState.Deleted)
            ?? outbound.FirstOrDefault()
            ?? shipments.OrderByDescending(x => x.Sequence).FirstOrDefault();
    }

    private static OrderShipmentView ToView(OrderShipment shipment, IReadOnlyCollection<OrderShipmentEvent> events) => new()
    {
        ShipmentId = shipment.ShipmentId,
        Kind = shipment.Kind.ToString(),
        State = shipment.State.ToString(),
        Provider = shipment.ProviderName,
        CarrierAlias = shipment.CarrierAlias,
        AccountReference = shipment.AccountReference,
        ServiceId = shipment.ServiceId,
        ServiceName = shipment.ServiceName,
        TrackingNumber = shipment.TrackingNumber,
        TrackingUrl = shipment.TrackingUrl,
        CarrierShipmentId = shipment.CarrierShipmentId,
        ShipmentReference = shipment.ShipmentReference,
        CarrierStatus = shipment.CarrierStatus,
        CarrierStatusText = shipment.CarrierStatusText,
        Attempts = shipment.Attempts,
        LastError = shipment.LastError,
        PickupLocationId = shipment.PickupLocationId,
        PickupLocationName = shipment.PickupLocationName,
        PickupLocationAddress = shipment.PickupLocationAddress,
        PickupLocationPostalCode = shipment.PickupLocationPostalCode,
        PickupLocationCity = shipment.PickupLocationCity,
        Amount = shipment.Amount,
        Currency = shipment.Currency,
        CreatedUtc = Utc(shipment.CreatedUtc),
        UpdatedUtc = Utc(shipment.UpdatedUtc),
        ShippedUtc = Utc(shipment.ShippedUtc),
        DeliveredUtc = Utc(shipment.DeliveredUtc),
        EstimatedDeliveryUtc = Utc(shipment.EstimatedDeliveryUtc),
        Details = shipment.GetDetails(),
        Events = events.OrderBy(x => x.OccurredUtc).ThenBy(x => x.Id)
            .Select(x => new OrderShipmentEventView(x.Type.ToString(), x.StateCol, x.CarrierStatus, x.Location, x.Message, x.Actor, Utc(x.OccurredUtc)))
            .ToList(),
    };

    private static void Apply(OrderShipment shipment, ShipmentUpdate update, DateTime now)
    {
        if (update.State is { } state)
        {
            shipment.State = state;
            shipment.LastError = Truncate(update.Error, 2000);
        }
        if (update.TrackingNumber != null) shipment.TrackingNumber = Empty(update.TrackingNumber);
        if (update.TrackingUrl != null) shipment.TrackingUrl = Empty(update.TrackingUrl);
        if (update.CarrierShipmentId != null) shipment.CarrierShipmentId = Empty(update.CarrierShipmentId);
        if (update.ShipmentReference != null) shipment.ShipmentReference = Empty(update.ShipmentReference);
        if (update.ServiceName != null) shipment.ServiceName = Empty(update.ServiceName);
        if (update.CarrierStatus != null) shipment.CarrierStatus = Empty(update.CarrierStatus);
        if (update.CarrierStatusText != null) shipment.CarrierStatusText = Truncate(Empty(update.CarrierStatusText), 500);
        if (update.Attempts is { } attempts) shipment.Attempts = attempts;
        if (update.Documents != null) shipment.Documents = update.Documents;
        if (update.Details != null) shipment.Details = update.Details.Count > 0 ? JsonSerializer.Serialize(update.Details) : null;
        if (update.ShippedUtc is { } shipped) shipment.ShippedUtc = shipped.ToUniversalTime();
        if (update.DeliveredUtc is { } delivered) shipment.DeliveredUtc = delivered.ToUniversalTime();
        if (update.EstimatedDeliveryUtc is { } estimated) shipment.EstimatedDeliveryUtc = estimated.ToUniversalTime();
        shipment.UpdatedUtc = now;
    }

    /// <summary>Times are stored in UTC; marking them so lets the manager show them in the viewer's time zone.</summary>
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private static DateTime? Utc(DateTime? value) => value is { } time ? Utc(time) : null;

    /// <summary>An empty string clears a value; null leaves it alone.</summary>
    private static string? Empty(string value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? Truncate(string? value, int length)
        => value == null || value.Length <= length ? value : value[..length];

    private static string Actor(string? actor, OrderShipment shipment)
        => !string.IsNullOrWhiteSpace(actor) ? actor
            : !string.IsNullOrWhiteSpace(shipment.CarrierAlias) ? shipment.CarrierAlias
            : "System";

    private static bool IsAlert(ShipmentState state)
        => state is ShipmentState.Failed or ShipmentState.OutcomeUnknown or ShipmentState.DeleteOutcomeUnknown;

    private static async Task InsertActivityLogAsync(DbContext db, OrderShipment shipment, OrderShipmentEvent entry,
        string? previousTracking, CancellationToken ct)
    {
        var message = ActivityMessage(shipment, entry, previousTracking);
        if (message == null) return;
        await db.InsertAsync(new OrderActivityLog
        {
            UniqueID = Guid.NewGuid(),
            Key = shipment.OrderUniqueId,
            Date = DateTime.Now,
            UserName = entry.Actor ?? string.Empty,
            LogType = entry.Type == ShipmentEventType.State && IsAlert(shipment.State) ? OrderActivityLogType.Alert
                : entry.Type == ShipmentEventType.State && shipment.State == ShipmentState.Created ? OrderActivityLogType.Success
                : OrderActivityLogType.Info,
            Log = message,
        }, token: ct).ConfigureAwait(false);
    }

    /// <summary>The activity log line for an event, or null for steps staff do not need to see.</summary>
    internal static string? ActivityMessage(OrderShipment shipment, OrderShipmentEvent entry, string? previousTracking)
    {
        var carrier = string.IsNullOrWhiteSpace(shipment.CarrierAlias) ? shipment.ProviderName : shipment.CarrierAlias;
        var with = string.IsNullOrWhiteSpace(carrier) ? string.Empty : $" with {carrier}";
        var tracking = string.IsNullOrWhiteSpace(shipment.TrackingNumber) ? string.Empty : $" Tracking number: {shipment.TrackingNumber}.";
        switch (entry.Type)
        {
            case ShipmentEventType.Manual:
                if (!string.Equals(previousTracking, shipment.TrackingNumber, StringComparison.Ordinal))
                {
                    return string.IsNullOrWhiteSpace(shipment.TrackingNumber)
                        ? "Shipment tracking number removed."
                        : $"Shipment tracking number set to {shipment.TrackingNumber}.";
                }
                return string.IsNullOrWhiteSpace(entry.Message) ? "Shipment updated." : $"Shipment updated: {entry.Message}";
            case ShipmentEventType.State:
                return shipment.State switch
                {
                    ShipmentState.Created => $"Shipment created{with}.{tracking}",
                    ShipmentState.Deleted => $"Shipment deleted{(string.IsNullOrWhiteSpace(carrier) ? string.Empty : $" from {carrier}")}.{tracking}",
                    ShipmentState.Failed or ShipmentState.OutcomeUnknown
                        => $"Shipment creation entered state {shipment.State}: {entry.Message}",
                    ShipmentState.DeleteOutcomeUnknown
                        => $"Shipment deletion entered state {shipment.State}: {entry.Message}",
                    _ => null,
                };
            default:
                return null;
        }
    }
}
