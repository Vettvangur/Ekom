namespace Ekom.Shipping;

public sealed record ShippingLookupRequest(
    string StoreAlias,
    string CountryCode,
    string? PostalCode = null);

public sealed record ShippingService(
    string Id,
    string Name,
    bool RequiresPickupLocation);

public sealed record PickupLocation(
    string Id,
    string Name,
    string Address,
    string PostalCode,
    string City,
    double? Latitude = null,
    double? Longitude = null,
    string? ExternalId = null);

public sealed record ShippingSelection(
    string CarrierAlias,
    string AccountReference,
    string ServiceId,
    string? PickupLocationId = null);

public sealed record ValidatedShippingSelection(
    ShippingSelection Selection,
    ShippingService Service,
    PickupLocation? PickupLocation);
