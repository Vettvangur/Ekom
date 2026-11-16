using Ekom.Models.Shipments;

namespace Ekom.Services;

/// <summary>Starts a shipment for an order.</summary>
public sealed record NewOrderShipment
{
    public required Guid OrderUniqueId { get; init; }
    public string? OrderNumber { get; init; }
    public string? StoreAlias { get; init; }

    /// <summary>Leave empty to have one generated.</summary>
    public Guid ShipmentId { get; init; }
    public ShipmentKind Kind { get; init; }
    public ShipmentState State { get; init; }
    public Guid? ProviderKey { get; init; }
    public string? ProviderName { get; init; }

    /// <summary>The carrier handling the shipment, e.g. "dropp". Empty for shipments entered by hand.</summary>
    public string? CarrierAlias { get; init; }
    public string? AccountReference { get; init; }
    public string? ServiceId { get; init; }
    public string? ServiceName { get; init; }
    public string? TrackingNumber { get; init; }
    public string? TrackingUrl { get; init; }
    public string? CarrierShipmentId { get; init; }
    public string? ShipmentReference { get; init; }
    public string? PickupLocationId { get; init; }
    public string? PickupLocationName { get; init; }
    public string? PickupLocationAddress { get; init; }
    public string? PickupLocationPostalCode { get; init; }
    public string? PickupLocationCity { get; init; }

    /// <summary>What the customer paid for shipping.</summary>
    public decimal? Amount { get; init; }

    /// <summary>ISO 4217 currency code.</summary>
    public string? Currency { get; init; }
    public int Attempts { get; init; }
    public string? LastError { get; init; }

    /// <summary>JSON list of references to stored labels and documents.</summary>
    public string? Documents { get; init; }

    /// <summary>Carrier-specific fields to show to staff.</summary>
    public IReadOnlyList<ShipmentDetail>? Details { get; init; }
    public string? Message { get; init; }

    /// <summary>The carrier alias when empty, otherwise "System".</summary>
    public string? Actor { get; init; }

    /// <summary>When the shipment was started. Defaults to now; set when copying a shipment that already exists elsewhere.</summary>
    public DateTime? CreatedUtc { get; init; }

    /// <summary>Set when copying an existing shipment, so the copy adds no activity log entry and raises no event.</summary>
    public bool Imported { get; init; }
}

/// <summary>
/// Something that happened to a shipment. Values left null keep what the shipment already has.
/// </summary>
public sealed record ShipmentUpdate
{
    public required Guid ShipmentId { get; init; }

    /// <summary><see cref="ShipmentEventType.State"/> when <see cref="State"/> is set, otherwise a note.</summary>
    public ShipmentEventType? Type { get; init; }

    /// <summary>The state the shipment moves to.</summary>
    public ShipmentState? State { get; init; }
    public string? TrackingNumber { get; init; }
    public string? TrackingUrl { get; init; }
    public string? CarrierShipmentId { get; init; }
    public string? ShipmentReference { get; init; }
    public string? ServiceName { get; init; }

    /// <summary>The carrier's own status code, such as "initial" or "delivered".</summary>
    public string? CarrierStatus { get; init; }
    public string? CarrierStatusText { get; init; }

    /// <summary>Where the carrier says the shipment is.</summary>
    public string? Location { get; init; }
    public int? Attempts { get; init; }

    /// <summary>
    /// Why the shipment failed. Stored as the shipment's last error whenever <see cref="State"/> is set,
    /// so a state change without an error clears it.
    /// </summary>
    public string? Error { get; init; }

    /// <summary>JSON list of references to stored labels and documents.</summary>
    public string? Documents { get; init; }

    /// <summary>Carrier-specific fields to show to staff. Replaces the shipment's details.</summary>
    public IReadOnlyList<ShipmentDetail>? Details { get; init; }
    public DateTime? ShippedUtc { get; init; }
    public DateTime? DeliveredUtc { get; init; }
    public DateTime? EstimatedDeliveryUtc { get; init; }
    public string? Message { get; init; }

    /// <summary>The carrier alias when empty.</summary>
    public string? Actor { get; init; }

    /// <summary>When the carrier says it happened. Defaults to now.</summary>
    public DateTime? OccurredUtc { get; init; }

    /// <summary>
    /// Set by a carrier that can report the same update more than once, such as a webhook or a tracking poll,
    /// so the update is stored once. Every update is stored when empty.
    /// </summary>
    public string? IdempotencyKey { get; init; }

    /// <summary>The carrier response as JSON, without secrets.</summary>
    public string? RawData { get; init; }
}

/// <param name="Recorded">False when the same update was already stored.</param>
/// <param name="Shipment">The shipment after the update.</param>
/// <param name="Event">The stored event.</param>
public sealed record ShipmentRecordResult(bool Recorded, OrderShipment Shipment, OrderShipmentEvent? Event);

public sealed record OrderShipmentEventView(string Type, string? State, string? CarrierStatus, string? Location,
    string? Message, string? Actor, DateTime OccurredUtc);

/// <summary>One shipment of an order with what is known about it, as shown in the manager.</summary>
public sealed record OrderShipmentView
{
    public Guid ShipmentId { get; init; }
    public required string Kind { get; init; }
    public required string State { get; init; }

    /// <summary>The name of the shipping provider node, e.g. "Home delivery".</summary>
    public string? Provider { get; init; }
    public string? CarrierAlias { get; init; }
    public string? AccountReference { get; init; }
    public string? ServiceId { get; init; }
    public string? ServiceName { get; init; }
    public string? TrackingNumber { get; init; }
    public string? TrackingUrl { get; init; }
    public string? CarrierShipmentId { get; init; }
    public string? ShipmentReference { get; init; }
    public string? CarrierStatus { get; init; }
    public string? CarrierStatusText { get; init; }
    public int Attempts { get; init; }
    public string? LastError { get; init; }
    public string? PickupLocationId { get; init; }
    public string? PickupLocationName { get; init; }
    public string? PickupLocationAddress { get; init; }
    public string? PickupLocationPostalCode { get; init; }
    public string? PickupLocationCity { get; init; }
    public decimal? Amount { get; init; }
    public string? Currency { get; init; }
    public DateTime CreatedUtc { get; init; }
    public DateTime UpdatedUtc { get; init; }
    public DateTime? ShippedUtc { get; init; }
    public DateTime? DeliveredUtc { get; init; }
    public DateTime? EstimatedDeliveryUtc { get; init; }
    public IReadOnlyList<ShipmentDetail> Details { get; init; } = Array.Empty<ShipmentDetail>();

    /// <summary>What happened to the shipment, oldest first.</summary>
    public IReadOnlyList<OrderShipmentEventView> Events { get; init; } = Array.Empty<OrderShipmentEventView>();
}

/// <summary>An order's shipments, as shown in the manager.</summary>
/// <param name="Current">The latest shipment that was not deleted, otherwise the latest one.</param>
/// <param name="Shipments">Every shipment, newest first.</param>
public sealed record OrderShipmentsView(OrderShipmentView? Current, IReadOnlyList<OrderShipmentView> Shipments);

/// <summary>
/// The shipments of Ekom orders. The shipping framework and carriers create a shipment and record what happens
/// to it; Ekom keeps the history, the activity log and the manager view from those records.
/// </summary>
public interface IOrderShipments
{
    /// <summary>Stores a new shipment and its first event.</summary>
    Task<OrderShipment> CreateAsync(NewOrderShipment shipment, CancellationToken ct = default);

    /// <summary>Stores what happened to a shipment and updates the shipment with it.</summary>
    Task<ShipmentRecordResult> RecordAsync(ShipmentUpdate update, CancellationToken ct = default);

    Task<OrderShipment?> GetAsync(Guid shipmentId, CancellationToken ct = default);

    /// <summary>The order's latest outbound shipment that was not deleted, otherwise its latest outbound shipment.</summary>
    Task<OrderShipment?> GetCurrentAsync(Guid orderId, CancellationToken ct = default);

    Task<OrderShipmentsView> GetForOrderAsync(Guid orderId, CancellationToken ct = default);

    /// <summary>The current shipment of each of the orders, loaded together. Orders without a shipment are left out.</summary>
    Task<IReadOnlyDictionary<Guid, OrderShipmentView>> GetCurrentShipmentsAsync(IReadOnlyCollection<Guid> orderIds, CancellationToken ct = default);

    /// <summary>Finds the order of a tracking number, a carrier shipment id or a shipment reference.</summary>
    Task<Guid?> FindOrderAsync(string trackingNumberOrShipmentId, CancellationToken ct = default);
}
