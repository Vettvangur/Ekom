namespace Ekom.Klaviyo.Models.Profiles;

public sealed record KlaviyoProfileSubscriptionUpdate(
    string StoreAlias,
    KlaviyoProfile Profile,
    IReadOnlyList<KlaviyoProfileConsentChange>? Consents = null);
