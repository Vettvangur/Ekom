using Ekom.Mailchimp.Exceptions;
using System.Threading.RateLimiting;

namespace Ekom.Mailchimp.Dispatching;

internal sealed class MailchimpSubscriptionRateLimiter : IDisposable
{
    private readonly Dictionary<string, ActionLimits> _stores = new(StringComparer.OrdinalIgnoreCase);
    private readonly ActionLimits _fallback;

    public MailchimpSubscriptionRateLimiter(MailchimpOptions options)
    {
        _fallback = new ActionLimits(options.Subscriptions);
        // Only configuration creates partitions. Arbitrary aliases cannot grow limiter state or bypass the fallback budget.
        foreach (var alias in options.Stores.Select(x => x.Alias.Trim()).Distinct(StringComparer.OrdinalIgnoreCase))
            _stores.Add(alias, new ActionLimits(options.Subscriptions));
    }

    internal static string? GetAction(MailchimpWorkItem item) => item switch
    {
        SubscribeWorkItem => "subscribe",
        UnsubscribeWorkItem => "unsubscribe",
        _ => null,
    };

    public void Acquire(MailchimpWorkItem item)
    {
        var action = GetAction(item);
        if (action == null) return;
        var limits = _stores.GetValueOrDefault(item.StoreAlias.Trim()) ?? _fallback;
        var limiter = action == "subscribe" ? limits.Subscribe : limits.Unsubscribe;
        if (limiter == null) return;
        using var lease = limiter.AttemptAcquire();
        if (!lease.IsAcquired)
        {
            lease.TryGetMetadata(MetadataName.RetryAfter, out TimeSpan retryAfter);
            throw new MailchimpRateLimitExceededException(item.StoreAlias, action, retryAfter);
        }
    }

    public void Dispose()
    {
        _fallback.Dispose();
        foreach (var limits in _stores.Values) limits.Dispose();
    }

    private sealed class ActionLimits : IDisposable
    {
        public FixedWindowRateLimiter? Subscribe { get; }
        public FixedWindowRateLimiter? Unsubscribe { get; }

        public ActionLimits(MailchimpSubscriptionOptions options)
        {
            Subscribe = Create(options.SubscribeRateLimit);
            Unsubscribe = Create(options.UnsubscribeRateLimit);
        }

        private static FixedWindowRateLimiter? Create(MailchimpActionRateLimitOptions options)
            => !options.Enabled ? null : new FixedWindowRateLimiter(new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.PermitLimit,
                Window = TimeSpan.FromSeconds(options.WindowSeconds),
                AutoReplenishment = true,
                QueueLimit = 0,
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            });

        public void Dispose()
        {
            Subscribe?.Dispose();
            Unsubscribe?.Dispose();
        }
    }
}
