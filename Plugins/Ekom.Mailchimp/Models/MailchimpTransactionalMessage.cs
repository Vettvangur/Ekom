using System.Diagnostics.CodeAnalysis;

namespace Ekom.Mailchimp.Models;

public enum MailchimpTransactionalRecipientType
{
    To,
    Cc,
    Bcc,
}

public enum MailchimpTransactionalEnqueueStatus
{
    Queued,
    Disabled,
    ConfigurationMissing,
    InvalidRequest,
    QueueFull,
    Cancelled,
}

public sealed record MailchimpTransactionalEnqueueResult
{
    public required MailchimpTransactionalEnqueueStatus Status { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed record MailchimpTransactionalRecipient
{
    public required string Email { get; init; }
    public string? Name { get; init; }
    public MailchimpTransactionalRecipientType Type { get; init; }
    public IReadOnlyDictionary<string, object?> MergeVariables { get; init; }
        = new Dictionary<string, object?>();
}

public sealed record MailchimpTransactionalContent
{
    public required string Name { get; init; }
    public required string ContentType { get; init; }
    [SuppressMessage("Performance", "CA1819", Justification = "Binary content is copied before it enters the queue.")]
    public required byte[] Content { get; init; }
}

public abstract record MailchimpTransactionalMessageBase
{
    public required string StoreAlias { get; init; }
    public string? CorrelationId { get; init; }
    public string? FromEmail { get; init; }
    public string? FromName { get; init; }
    public string? ReplyTo { get; init; }
    public string? Subject { get; init; }
    public IReadOnlyList<MailchimpTransactionalRecipient> Recipients { get; init; }
        = [];
    public IReadOnlyDictionary<string, object?> GlobalMergeVariables { get; init; }
        = new Dictionary<string, object?>();
    public IReadOnlyDictionary<string, object?> Metadata { get; init; }
        = new Dictionary<string, object?>();
    public IReadOnlyList<string> Tags { get; init; } = [];
    public IReadOnlyList<MailchimpTransactionalContent> Attachments { get; init; } = [];
    public IReadOnlyList<MailchimpTransactionalContent> InlineImages { get; init; } = [];
}

public sealed record MailchimpTransactionalMessage : MailchimpTransactionalMessageBase
{
    public string? Html { get; init; }
    public string? Text { get; init; }
}

public sealed record MailchimpTransactionalTemplateMessage : MailchimpTransactionalMessageBase
{
    public required string TemplateName { get; init; }
    public IReadOnlyDictionary<string, string> TemplateContent { get; init; }
        = new Dictionary<string, string>();
}
