using System.Diagnostics.CodeAnalysis;

namespace Ekom.Mailchimp.Exceptions;

/// <summary>The subscription action was not queued because the dispatcher has no available capacity.</summary>
[SuppressMessage("Design", "CA1032", Justification = "The rejected Mailchimp action context is required to construct this exception.")]
public sealed class MailchimpSubscriptionQueueFullException : InvalidOperationException
{
    public MailchimpSubscriptionQueueFullException(string storeAlias, string action)
        : base($"Mailchimp {action} could not be queued for store '{storeAlias}'. The dispatcher queue is full or closed.")
    {
        StoreAlias = storeAlias;
        Action = action;
    }

    public string StoreAlias { get; }
    public string Action { get; }
}
