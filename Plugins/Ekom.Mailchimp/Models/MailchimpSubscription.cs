namespace Ekom.Mailchimp.Models;

public enum MailchimpSubscriptionStatus
{
    Pending,
    Subscribed,
}

public sealed record MailchimpSubscribeRequest
{
    public required string StoreAlias { get; init; }
    public required string Email { get; init; }
    public required MailchimpSubscriptionStatus Status { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Language { get; init; }
    public IReadOnlyDictionary<string, object?> MergeFields { get; init; }
        = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
    public IReadOnlyCollection<string> Tags { get; init; } = [];
}

public sealed record MailchimpUnsubscribeRequest
{
    public required string StoreAlias { get; init; }
    public required string Email { get; init; }
}
