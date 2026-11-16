namespace Ekom.Models.Manager;

/// <summary>A tracking number entered or corrected by a manager user.</summary>
public sealed class OrderShipmentTrackingUpdateRequest
{
    /// <summary>Empty removes the tracking number.</summary>
    public string? TrackingNumber { get; init; }

    /// <summary>Where the shipment can be followed. Must be an http or https address.</summary>
    public string? TrackingUrl { get; init; }
}
