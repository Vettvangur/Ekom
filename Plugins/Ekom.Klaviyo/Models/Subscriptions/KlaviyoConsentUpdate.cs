namespace Ekom.Klaviyo.Models.Subscriptions;

public sealed record KlaviyoConsentUpdate(
    string StoreAlias,
    KlaviyoProfile Profile,
    IReadOnlyList<KlaviyoConsentChange> Consents);
