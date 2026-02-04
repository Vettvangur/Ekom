namespace Ekom.Klaviyo.Models.Subscriptions;

public sealed record KlaviyoConsentChange(
    KlaviyoConsentChannel Channel, 
    KlaviyoConsentState State,              
    string? Source = null,
    DateTimeOffset? TimestampUtc = null,
    string? ConsentTextVersion = null,
    string? Ip = null,
    string? UserAgent = null);
