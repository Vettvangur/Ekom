using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using System.Collections.Concurrent;
using System.Threading.RateLimiting;

namespace Ekom.ActionFilters;

/// <summary>
/// Names of the per-client limits applied to Ekom's public endpoints.
/// </summary>
public static class EkomRateLimitPolicies
{
    public const string OrderAdd = "order-add";
    public const string OrderCoupon = "order-coupon";
    public const string OrderGiftcard = "order-giftcard";
    public const string CheckoutPay = "checkout-pay";
    public const string OrderUpdate = "order-update";
}

/// <summary>
/// Limits how often one client address may call an action. Enforced as an MVC filter so it
/// works without the host adding rate-limiting middleware to its pipeline.
/// The client is identified by <see cref="ConnectionInfo.RemoteIpAddress"/>: sites behind a
/// proxy or load balancer must enable forwarded headers, otherwise all visitors share one limit.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class EkomRateLimitAttribute : Attribute, IResourceFilter
{
    private static readonly ConcurrentDictionary<string, PartitionedRateLimiter<string>> Limiters = new(StringComparer.Ordinal);

    public EkomRateLimitAttribute(string policy)
    {
        Policy = policy ?? throw new ArgumentNullException(nameof(policy));
    }

    public string Policy { get; }

    public void OnResourceExecuting(ResourceExecutingContext context)
    {
        var client = context.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        if (!TryAcquire(Policy, client))
        {
            context.Result = new StatusCodeResult(StatusCodes.Status429TooManyRequests);
        }
    }

    public void OnResourceExecuted(ResourceExecutedContext context)
    {
    }

    internal static bool TryAcquire(string policy, string client)
    {
        var limiter = Limiters.GetOrAdd(policy, CreateLimiter);
        using RateLimitLease lease = limiter.AttemptAcquire(client);
        return lease.IsAcquired;
    }

    private static PartitionedRateLimiter<string> CreateLimiter(string policy)
    {
        return policy switch
        {
            EkomRateLimitPolicies.OrderAdd => FixedWindow(30),
            EkomRateLimitPolicies.CheckoutPay => FixedWindow(30),
            EkomRateLimitPolicies.OrderUpdate => FixedWindow(60),
            EkomRateLimitPolicies.OrderCoupon => TokenBucket(),
            EkomRateLimitPolicies.OrderGiftcard => TokenBucket(),
            _ => throw new InvalidOperationException($"Unknown Ekom rate limit policy '{policy}'."),
        };
    }

    private static PartitionedRateLimiter<string> FixedWindow(int permitsPerMinute)
        => PartitionedRateLimiter.Create<string, string>(client => RateLimitPartition.GetFixedWindowLimiter(
            client,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
            }));

    private static PartitionedRateLimiter<string> TokenBucket()
        => PartitionedRateLimiter.Create<string, string>(client => RateLimitPartition.GetTokenBucketLimiter(
            client,
            _ => new TokenBucketRateLimiterOptions
            {
                TokenLimit = 13,
                TokensPerPeriod = 10,
                ReplenishmentPeriod = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0,
                AutoReplenishment = true,
            }));
}
