using System.Collections.ObjectModel;

namespace Ekom.Brevo;

public sealed class BrevoOptions
{
    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
    public Uri ApiBaseUrl { get; set; } = new("https://api.brevo.com/v3/");
    public BrevoSubscriptionOptions Subscriptions { get; set; } = new();
    public BrevoOrderOptions Orders { get; set; } = new();
    public BrevoTransactionalOptions Transactional { get; set; } = new();
    public BrevoDispatcherOptions Dispatching { get; set; } = new();
    public Collection<BrevoStoreOptions> Stores { get; } = [];
}

public sealed class BrevoStoreOptions
{
    public string Alias { get; set; } = "";
    public string? ApiKey { get; set; }
    public long? ListId { get; set; }
    public long? DoubleOptInTemplateId { get; set; }
    public Uri? DoubleOptInRedirectUrl { get; set; }
    public BrevoTransactionalStoreOptions Transactional { get; set; } = new();
}

public sealed class BrevoSubscriptionOptions
{
    public bool Enabled { get; set; } = true;
    public long? DefaultListId { get; set; }
    public bool DoubleOptIn { get; set; } = true;
    public long? DoubleOptInTemplateId { get; set; }
    public Uri? DoubleOptInRedirectUrl { get; set; }
}

public sealed class BrevoOrderOptions
{
    public bool Enabled { get; set; } = true;
    public bool TrackCompletedCheckouts { get; set; }
}

public sealed class BrevoTransactionalOptions
{
    public bool Enabled { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultFromEmail { get; set; }
    public string? DefaultFromName { get; set; }
    public string? DefaultReplyTo { get; set; }
    public BrevoDispatcherOptions Dispatching { get; set; } = new();
}

public sealed class BrevoTransactionalStoreOptions
{
    public bool? Enabled { get; set; }
    public string? ApiKey { get; set; }
    public string? DefaultFromEmail { get; set; }
    public string? DefaultFromName { get; set; }
    public string? DefaultReplyTo { get; set; }
}

public sealed class BrevoDispatcherOptions
{
    public int MaxQueueSize { get; set; } = 1000;
    public int MaxAttempts { get; set; } = 4;
    public int InitialRetryDelaySeconds { get; set; } = 2;
    public int MaxRetryDelaySeconds { get; set; } = 120;
    public int MaxMessageBytes { get; set; } = 9 * 1024 * 1024;
    public long MaxQueueBytes { get; set; } = 64 * 1024 * 1024;
}
