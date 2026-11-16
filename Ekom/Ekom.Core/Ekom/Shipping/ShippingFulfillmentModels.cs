namespace Ekom.Shipping;

public sealed record ShipmentRecipient(
    string Name,
    string Email,
    string Phone,
    string Address,
    string PostalCode,
    string City,
    string CountryCode,
    string? NationalId = null);

public sealed record ShipmentItem(string Sku, string Name, int Quantity);

public sealed record ShipmentPickupLocation(
    string Id,
    string Name,
    string Address,
    string PostalCode,
    string City);

public sealed record ShipmentBookingRequest(
    string MerchantOrderId,
    string ServiceId,
    ShipmentRecipient Recipient,
    IReadOnlyList<ShipmentItem> Items,
    decimal Value,
    string Currency,
    string? PickupLocationId = null,
    string? BookingReference = null,
    ShipmentPickupLocation? PickupLocation = null);

public sealed record ShipmentBookingResult(
    string ShipmentId,
    string? BookingReference = null,
    string? TrackingNumber = null,
    IReadOnlyList<ShippingCreationDocument>? Documents = null)
{
    /// <summary>Where the recipient and staff can follow the shipment.</summary>
    public string? TrackingUrl { get; init; }

    /// <summary>The carrier's name of the booked service, when it differs from the selected one.</summary>
    public string? ServiceName { get; init; }

    /// <summary>The carrier's status of the new shipment, as the carrier names it.</summary>
    public string? CarrierStatus { get; init; }

    /// <summary>Carrier-specific fields to show to staff on the order's shipment.</summary>
    public IReadOnlyList<Models.Shipments.ShipmentDetail>? Details { get; init; }
}

public sealed record ShippingCreationDocument(
    string TypeCode,
    string Format,
    byte[] Content,
    string ContentType,
    string FileName,
    int? PackageReferenceNumber = null);

public sealed record StoredShippingDocument(
    string Reference,
    string TypeCode,
    string Format,
    string ContentType,
    string FileName,
    int? PackageReferenceNumber = null);

public sealed record StoreShippingDocumentsRequest(
    Guid OrderId,
    string CarrierAlias,
    string ShipmentId,
    IReadOnlyList<ShippingDocumentStorageItem> Documents);

public sealed record ShippingDocumentStorageItem(
    StoredShippingDocument Document,
    byte[] Content);

public sealed record ShippingLabel(
    byte[] Content,
    string ContentType,
    string FileName);

public sealed record ShippingTrackingResult(string Content, string ContentType = "application/json");

public sealed record ShippingShipmentDocument(
    byte[] Content,
    string ContentType,
    string FileName,
    string? Status = null);
