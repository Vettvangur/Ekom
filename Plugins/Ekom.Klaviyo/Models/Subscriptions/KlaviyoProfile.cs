namespace Ekom.Klaviyo.Models.Subscriptions;

public sealed class KlaviyoProfile
{
    public required KlaviyoCustomer Customer { get; init; }
    public KlaviyoProfileAttributes? Attributes { get; init; }
}
