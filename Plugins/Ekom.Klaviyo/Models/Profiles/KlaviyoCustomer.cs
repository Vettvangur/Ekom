namespace Ekom.Klaviyo.Models.Profiles;

public sealed class KlaviyoCustomer
{
    /// <summary>
    /// Primary email identifier (most common for Klaviyo).
    /// </summary>
    public string? Email { get; init; }

    /// <summary>
    /// E.164 formatted phone number for SMS consent.
    /// </summary>
    public string? PhoneNumber { get; init; }

    /// <summary>
    /// system's unique ID (customer/member/user id).
    /// </summary>
    public string? ExternalId { get; init; }

    /// <summary>
    /// Klaviyo profile id (if known).
    /// </summary>
    public string? KlaviyoProfileId { get; init; }


    public bool HasIdentifier =>
        !string.IsNullOrWhiteSpace(Email) ||
        !string.IsNullOrWhiteSpace(PhoneNumber) ||
        !string.IsNullOrWhiteSpace(ExternalId) ||
        !string.IsNullOrWhiteSpace(KlaviyoProfileId);

    public void Validate()
    {
        if (!HasIdentifier)
            throw new InvalidOperationException(
                "KlaviyoCustomer requires at least one identifier (Email, PhoneNumber, ExternalId, or KlaviyoProfileId).");
    }
}
