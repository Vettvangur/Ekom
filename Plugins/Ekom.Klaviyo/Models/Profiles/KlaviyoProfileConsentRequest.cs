namespace Ekom.Klaviyo.Models.Profiles;

public sealed record KlaviyoProfileConsentRequest(
    string StoreAlias,
    string Email,
    IReadOnlyList<KlaviyoProfileConsentChange>? Consents = null,
    string? PhoneNumber = null,
    string? FullName = null,
    string? FirstName = null,
    string? LastName = null);

public sealed record KlaviyoProfileSubscribeRequest(
    string StoreAlias,
    string Email,
    IReadOnlyList<KlaviyoProfileConsentChange>? Consents = null,
    string? ListId = null,
    string? PhoneNumber = null,
    string? FullName = null,
    string? FirstName = null,
    string? LastName = null);

public sealed record KlaviyoProfileUnsubscribeRequest(
    string StoreAlias,
    string Email);
