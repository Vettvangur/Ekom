namespace Ekom.Klaviyo.Models;

public sealed record KlaviyoProfile(
    string? Email = null,
    string? PhoneNumber = null,
    string? ExternalId = null,
    string? FirstName = null,
    string? LastName = null,
    string? Address = null,
    string? ZipCode = null,
    string? City = null,
    string? Country = null,
    string? Company = null,
    IDictionary<string, object?>? CustomProperties = null
)
{
    public bool HasIdentifier =>
        !string.IsNullOrWhiteSpace(Email) ||
        !string.IsNullOrWhiteSpace(PhoneNumber) ||
        !string.IsNullOrWhiteSpace(ExternalId);
}


