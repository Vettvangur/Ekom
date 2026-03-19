namespace Ekom.Klaviyo.Models.Profiles;

public sealed record KlaviyoProfileUpdate(
    string StoreAlias,
    KlaviyoProfile Profile,
    string? ListId = null);
