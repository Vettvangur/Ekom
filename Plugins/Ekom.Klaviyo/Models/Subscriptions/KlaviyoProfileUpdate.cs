namespace Ekom.Klaviyo.Models.Subscriptions;

public sealed record KlaviyoProfileUpdate(
    string StoreAlias,
    KlaviyoProfile Profile,
    string? ListId = null);
