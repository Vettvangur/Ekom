namespace Ekom.Klaviyo.Models;

public sealed record KlaviyoCustomerIdentity(
    string? Email = null,
    string? PhoneNumber = null,
    string? ExternalId = null,
    string? FirstName = null,
    string? LastName = null
)
{
    public bool HasIdentifier =>
        !string.IsNullOrWhiteSpace(Email) ||
        !string.IsNullOrWhiteSpace(PhoneNumber) ||
        !string.IsNullOrWhiteSpace(ExternalId);
}
