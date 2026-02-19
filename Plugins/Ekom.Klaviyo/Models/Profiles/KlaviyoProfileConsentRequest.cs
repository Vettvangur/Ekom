namespace Ekom.Klaviyo.Models.Profiles;

public sealed record KlaviyoProfileConsentRequest(
    string StoreAlias,
    string Email,
    IReadOnlyList<KlaviyoProfileConsentChange> Consents);
