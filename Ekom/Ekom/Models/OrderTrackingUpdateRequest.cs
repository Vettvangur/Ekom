namespace Ekom.Models;

public sealed class OrderTrackingUpdateRequest
{
    public string StoreAlias { get; set; } = string.Empty;
    public OrderConsent? Consent { get; set; }
    public required OrderTracking Tracking { get; set; }
}
