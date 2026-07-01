namespace Ekom.Klaviyo.Models.Events;

public sealed record KlaviyoCustomEvent
{
    public required string StoreAlias { get; init; }
    public required string EventName { get; init; }
    public required KlaviyoEventProfile Profile { get; init; }
    public object? Properties { get; init; }
    public DateTimeOffset OccurredAt { get; init; } = DateTimeOffset.UtcNow;
    public string? UniqueId { get; init; }
}

public sealed class KlaviyoEventProfile
{
    public string? Email { get; set; }
    public string? PhoneNumber { get; set; }
    public string? ExternalId { get; set; }
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? Address { get; set; }
    public string? Address2 { get; set; }
    public string? ZipCode { get; set; }
    public string? City { get; set; }
    public string? Country { get; set; }
    public string? Organisation { get; set; }
    public IReadOnlyDictionary<string, object?>? CustomProperties { get; init; }

    public bool HasIdentifier =>
        !string.IsNullOrWhiteSpace(Email) ||
        !string.IsNullOrWhiteSpace(PhoneNumber) ||
        !string.IsNullOrWhiteSpace(ExternalId);
}
