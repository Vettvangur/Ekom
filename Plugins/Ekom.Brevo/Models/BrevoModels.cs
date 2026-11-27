namespace Ekom.Brevo.Models;

public enum BrevoSubmissionStatus
{
    Queued,
    Disabled,
    ConfigurationMissing,
    QueueFull,
    Duplicate,
}
public enum BrevoDeliveryStatus
{
    Accepted,
    Failed,
    Unknown,
    Cancelled,
}

/// <summary>Accepted means the Brevo API accepted the request, not that an email reached its recipient.</summary>
public sealed record BrevoDeliveryResult(BrevoDeliveryStatus Status, string? MessageId = null, int? HttpStatus = null);

/// <summary>Completion is available for queued work. Invalid requests throw before queue admission.</summary>
public sealed record BrevoSubmission(BrevoSubmissionStatus Status, string CorrelationId, Task<BrevoDeliveryResult>? Completion = null);

public sealed record BrevoContact
{
    public required string StoreAlias { get; init; }
    public required string Email { get; init; }
    public string? ExternalId { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Phone { get; init; }
    public IReadOnlyDictionary<string, object?> Attributes { get; init; } = new Dictionary<string, object?>();
}

public sealed record BrevoList(long Id, string Name);
public sealed record BrevoEmailAddress(string Email, string? Name = null);
public sealed record BrevoAttachment(string Name, ReadOnlyMemory<byte> Content);

public sealed record BrevoTransactionalMessage
{
    /// <summary>Optional Ekom order identifier for durable delivery lookup.</summary>
    public string? OrderId { get; init; }
    /// <summary>Optional store-scoped caller key. Reusing it with different content throws; retained for the configured retention period.</summary>
    public string? IdempotencyKey { get; init; }
    public required string StoreAlias { get; init; }
    public BrevoEmailAddress? Sender { get; init; }
    public BrevoEmailAddress? ReplyTo { get; init; }
    public IReadOnlyList<BrevoEmailAddress> To { get; init; } = [];
    public IReadOnlyList<BrevoEmailAddress> Cc { get; init; } = [];
    public IReadOnlyList<BrevoEmailAddress> Bcc { get; init; } = [];
    public string? Subject { get; init; }
    public string? HtmlContent { get; init; }
    public string? TextContent { get; init; }
    public long? TemplateId { get; init; }
    public IReadOnlyDictionary<string, object?> Parameters { get; init; } = new Dictionary<string, object?>();
    public IReadOnlyList<BrevoAttachment> Attachments { get; init; } = [];
}
