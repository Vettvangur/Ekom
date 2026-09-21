using System.Collections.ObjectModel;

namespace Ekom.Mailchimp;

public sealed class MailchimpOptions
{
    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
    public string? ServerPrefix { get; set; }
    public string? AudienceId { get; set; }
    public string? EcommerceStoreId { get; set; }
    public Uri? SiteBaseUrl { get; set; }
    public MailchimpSubscriptionOptions Subscriptions { get; set; } = new();
    public MailchimpPurchaseOptions Purchases { get; set; } = new();
    public MailchimpTransactionalOptions Transactional { get; set; } = new();
    public MailchimpDispatcherOptions Dispatching { get; set; } = new();
    public Collection<MailchimpStoreOptions> Stores { get; } = [];
}

public sealed class MailchimpSubscriptionOptions
{
    public bool Enabled { get; set; } = true;
}

public sealed class MailchimpPurchaseOptions
{
    public bool Enabled { get; set; } = true;
    public bool TrackCompletedCheckouts { get; set; }
}

public sealed class MailchimpDispatcherOptions
{
    public int MaxQueueSize { get; set; } = 1_000;
    public int MaxConcurrency { get; set; } = 3;
    public int MaxAttempts { get; set; } = 4;
    public int InitialRetryDelaySeconds { get; set; } = 2;
}

public sealed class MailchimpTransactionalOptions
{
    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultFromEmail { get; set; }
    public string? DefaultFromName { get; set; }
    public string? DefaultReplyTo { get; set; }
    public MailchimpTransactionalDispatcherOptions Dispatching { get; set; } = new();
}

public sealed class MailchimpTransactionalDispatcherOptions
{
    public int MaxQueueSize { get; set; } = 1_000;
    public int MaxConcurrency { get; set; } = 3;
    public int MaxAttempts { get; set; } = 4;
    public int InitialRetryDelaySeconds { get; set; } = 2;
    public int MaxMessageBytes { get; set; } = 9 * 1024 * 1024;
    public long MaxQueueBytes { get; set; } = 64 * 1024 * 1024;
}

public sealed class MailchimpStoreTransactionalOptions
{
    public bool? Enabled { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultFromEmail { get; set; }
    public string? DefaultFromName { get; set; }
    public string? DefaultReplyTo { get; set; }
}

public sealed class MailchimpStoreOptions
{
    public required string Alias { get; set; }
    public string? ApiKey { get; set; }
    public string? ServerPrefix { get; set; }
    public string? AudienceId { get; set; }
    public string? EcommerceStoreId { get; set; }
    public Uri? SiteBaseUrl { get; set; }
    public MailchimpStoreTransactionalOptions Transactional { get; set; } = new();
}
