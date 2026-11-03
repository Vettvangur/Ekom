using System.Diagnostics.CodeAnalysis;

namespace Ekom.Mailchimp.Exceptions;

/// <summary>The action was not queued. The caller can retry after <see cref="RetryAfter"/>.</summary>
[SuppressMessage("Design", "CA1032", Justification = "The rejected Mailchimp action context is required to construct this exception.")]
public sealed class MailchimpRateLimitExceededException : InvalidOperationException
{
    public MailchimpRateLimitExceededException(string storeAlias, string action, TimeSpan retryAfter)
        : base($"Mailchimp {action} rate limit exceeded for store '{storeAlias}'. The request was not queued.")
    {
        StoreAlias = storeAlias;
        Action = action;
        RetryAfter = retryAfter;
    }

    public string StoreAlias { get; }
    public string Action { get; }
    public TimeSpan RetryAfter { get; }
}
