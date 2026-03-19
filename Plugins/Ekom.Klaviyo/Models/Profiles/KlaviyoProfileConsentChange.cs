namespace Ekom.Klaviyo.Models.Profiles;

public sealed record KlaviyoProfileConsentChange(
    KlaviyoProfileConsentChannel Channel, 
    KlaviyoProfileConsentState State,              
    string? Source = null,
    DateTimeOffset? TimestampUtc = null,
    string? ConsentTextVersion = null,
    string? Ip = null,
    string? UserAgent = null);
