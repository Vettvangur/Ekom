namespace Ekom.Klaviyo.Models.Subscriptions;

public sealed record KlaviyoSubscriptionUpdate(
    string StoreAlias,
    KlaviyoProfile Profile,
    IReadOnlyList<KlaviyoConsentChange>? Consents = null);
