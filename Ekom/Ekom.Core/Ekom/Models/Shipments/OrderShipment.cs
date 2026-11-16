using LinqToDB.Mapping;
using System.Text.Json;

namespace Ekom.Models.Shipments;

/// <summary>Where a shipment is in its booking with the carrier.</summary>
public enum ShipmentState
{
    Pending = 0,
    Processing = 1,
    Submitting = 2,
    Created = 3,
    Failed = 4,

    /// <summary>The carrier may or may not have created the shipment. Never retried automatically.</summary>
    OutcomeUnknown = 5,
    Deleting = 6,
    Deleted = 7,

    /// <summary>The carrier may or may not have deleted the shipment.</summary>
    DeleteOutcomeUnknown = 8,
}

/// <summary>What a shipment is for.</summary>
public enum ShipmentKind
{
    Outbound = 0,
    Return = 1,
    ExtraParcel = 2,
}

/// <summary>What a shipment event records.</summary>
public enum ShipmentEventType
{
    /// <summary>The shipment moved to another <see cref="ShipmentState"/>.</summary>
    State = 0,

    /// <summary>The carrier reported progress, such as "out for delivery".</summary>
    Tracking = 1,

    /// <summary>A manager user changed the shipment, such as its tracking number.</summary>
    Manual = 2,

    /// <summary>Something worth knowing that changes nothing.</summary>
    Note = 3,
}

/// <summary>
/// One shipment of an Ekom order. An order can have several: a replacement after a deleted shipment,
/// an extra parcel or a return.
/// </summary>
[Table(Name = "EkomOrderShipments")]
public sealed class OrderShipment
{
    [PrimaryKey, Identity, NotNull]
    public int Sequence { get; set; }

    /// <summary>Identifies this shipment in Ekom.</summary>
    [Column, NotNull]
    public Guid ShipmentId { get; set; }

    /// <summary>The Ekom order (<see cref="OrderData.UniqueId"/>).</summary>
    [Column, NotNull]
    public Guid OrderUniqueId { get; set; }

    [Column(Length = 100)]
    public string? OrderNumber { get; set; }

    [Column(Length = 100)]
    public string? StoreAlias { get; set; }

    /// <summary>Database representation of <see cref="Kind"/>.</summary>
    [Column(Length = 32), NotNull]
    public string KindCol { get; set; } = nameof(ShipmentKind.Outbound);

    [Column(IsColumn = false)]
    public ShipmentKind Kind
    {
        get => Enum.TryParse<ShipmentKind>(KindCol, out var kind) ? kind : ShipmentKind.Outbound;
        set => KindCol = value.ToString();
    }

    /// <summary>Database representation of <see cref="State"/>.</summary>
    [Column(Length = 32), NotNull]
    public string StateCol { get; set; } = nameof(ShipmentState.Pending);

    [Column(IsColumn = false)]
    public ShipmentState State
    {
        get => Enum.TryParse<ShipmentState>(StateCol, out var state) ? state : ShipmentState.Pending;
        set => StateCol = value.ToString();
    }

    /// <summary>The shipping provider node the order was placed with.</summary>
    [Column]
    public Guid? ProviderKey { get; set; }

    [Column(Length = 200)]
    public string? ProviderName { get; set; }

    /// <summary>The carrier that handles the shipment, e.g. "dropp". Empty for shipments entered by hand.</summary>
    [Column(Length = 100)]
    public string? CarrierAlias { get; set; }

    [Column(Length = 200)]
    public string? AccountReference { get; set; }

    [Column(Length = 200)]
    public string? ServiceId { get; set; }

    [Column(Length = 200)]
    public string? ServiceName { get; set; }

    [Column(Length = 200)]
    public string? TrackingNumber { get; set; }

    [Column(Length = 2000)]
    public string? TrackingUrl { get; set; }

    /// <summary>The carrier's own id of the shipment.</summary>
    [Column(Length = 200)]
    public string? CarrierShipmentId { get; set; }

    /// <summary>The booking reference, such as a barcode reserved before the shipment was submitted.</summary>
    [Column(Length = 200)]
    public string? ShipmentReference { get; set; }

    /// <summary>The carrier's latest status code, as the carrier names it.</summary>
    [Column(Length = 100)]
    public string? CarrierStatus { get; set; }

    [Column(Length = 500)]
    public string? CarrierStatusText { get; set; }

    [Column, NotNull]
    public int Attempts { get; set; }

    [Column(Length = 2000)]
    public string? LastError { get; set; }

    [Column(Length = 200)]
    public string? PickupLocationId { get; set; }

    [Column(Length = 200)]
    public string? PickupLocationName { get; set; }

    [Column(Length = 500)]
    public string? PickupLocationAddress { get; set; }

    [Column(Length = 50)]
    public string? PickupLocationPostalCode { get; set; }

    [Column(Length = 200)]
    public string? PickupLocationCity { get; set; }

    /// <summary>What the customer paid for shipping.</summary>
    [Column(Precision = 18, Scale = 2)]
    public decimal? Amount { get; set; }

    /// <summary>ISO 4217 currency code.</summary>
    [Column(Length = 10)]
    public string? Currency { get; set; }

    /// <summary>JSON list of references to stored labels and documents. Never the documents themselves.</summary>
    [Column(Length = int.MaxValue)]
    public string? Documents { get; set; }

    /// <summary>JSON list of <see cref="ShipmentDetail"/>: carrier-specific fields shown to staff.</summary>
    [Column(Length = int.MaxValue)]
    public string? Details { get; set; }

    [Column, NotNull]
    public DateTime CreatedUtc { get; set; }

    [Column, NotNull]
    public DateTime UpdatedUtc { get; set; }

    [Column]
    public DateTime? ShippedUtc { get; set; }

    [Column]
    public DateTime? DeliveredUtc { get; set; }

    [Column]
    public DateTime? EstimatedDeliveryUtc { get; set; }

    public IReadOnlyList<ShipmentDetail> GetDetails()
        => string.IsNullOrWhiteSpace(Details)
            ? Array.Empty<ShipmentDetail>()
            : JsonSerializer.Deserialize<List<ShipmentDetail>>(Details) ?? (IReadOnlyList<ShipmentDetail>)Array.Empty<ShipmentDetail>();

    /// <summary>Reads <see cref="Documents"/> as the type the shipping framework stored.</summary>
    public IReadOnlyList<T> GetDocuments<T>()
        => string.IsNullOrWhiteSpace(Documents)
            ? Array.Empty<T>()
            : JsonSerializer.Deserialize<List<T>>(Documents) ?? (IReadOnlyList<T>)Array.Empty<T>();
}

/// <summary>
/// One thing that happened to a shipment. Rows are append-only, so earlier attempts and failures stay visible.
/// </summary>
[Table(Name = "EkomOrderShipmentEvents")]
public sealed class OrderShipmentEvent
{
    [PrimaryKey, Identity, NotNull]
    public int Id { get; set; }

    [Column, NotNull]
    public Guid ShipmentId { get; set; }

    [Column, NotNull]
    public Guid OrderUniqueId { get; set; }

    /// <summary>Database representation of <see cref="Type"/>.</summary>
    [Column(Length = 32), NotNull]
    public string TypeCol { get; set; } = nameof(ShipmentEventType.State);

    [Column(IsColumn = false)]
    public ShipmentEventType Type
    {
        get => Enum.TryParse<ShipmentEventType>(TypeCol, out var type) ? type : ShipmentEventType.State;
        set => TypeCol = value.ToString();
    }

    /// <summary>The shipment's state after this event.</summary>
    [Column(Length = 32)]
    public string? StateCol { get; set; }

    [Column(IsColumn = false)]
    public ShipmentState? State
        => Enum.TryParse<ShipmentState>(StateCol, out var state) ? state : null;

    [Column(Length = 100)]
    public string? CarrierStatus { get; set; }

    [Column(Length = 500)]
    public string? Location { get; set; }

    [Column(Length = 2000)]
    public string? Message { get; set; }

    /// <summary>The carrier alias, "System", or the manager user name.</summary>
    [Column(Length = 200)]
    public string? Actor { get; set; }

    /// <summary>When the carrier says it happened.</summary>
    [Column, NotNull]
    public DateTime OccurredUtc { get; set; }

    /// <summary>When Ekom stored it.</summary>
    [Column, NotNull]
    public DateTime ReceivedUtc { get; set; }

    /// <summary>A repeated carrier update with the same key is stored once.</summary>
    [Column(Length = 300), NotNull]
    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>The carrier's response as received, with secrets removed.</summary>
    [Column(Length = int.MaxValue)]
    public string? RawData { get; set; }
}

/// <summary>A carrier-specific field a carrier chooses to show on the order's shipment.</summary>
public sealed record ShipmentDetail(string Key, string Label, string? Value, bool Copyable = false);
