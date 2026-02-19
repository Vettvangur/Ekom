namespace Ekom.Klaviyo.Models.Profiles;

public sealed record KlaviyoProfileIdentity(
    string? Email = null,
    string? PhoneNumber = null,
    string? ExternalId = null,
    string? KlaviyoProfileId = null)
{
    public bool HasIdentifier
        => !string.IsNullOrWhiteSpace(Email) ||
           !string.IsNullOrWhiteSpace(PhoneNumber) ||
           !string.IsNullOrWhiteSpace(ExternalId) ||
           !string.IsNullOrWhiteSpace(KlaviyoProfileId);
}
