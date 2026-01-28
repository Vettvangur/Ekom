namespace Ekom.Klaviyo.Models;

public sealed class KlaviyoProfile
{
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ExternalId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Address { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Organisation { get; set; }
    public IDictionary<string, object?>? CustomProperties { get; set; }

    public bool HasIdentifier =>
        !string.IsNullOrWhiteSpace(Email) ||
        !string.IsNullOrWhiteSpace(PhoneNumber) ||
        !string.IsNullOrWhiteSpace(ExternalId);
}
