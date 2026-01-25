using Ekom.Klaviyo.Clients;
using Ekom.Klaviyo.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Dispatching.Orders;

public interface IKlaviyoOrdersDispatcher
{
    ValueTask EnqueueAsync(KlaviyoOrderWork work, CancellationToken ct = default);
}

/// <summary>
/// A single queued order-related Klaviyo event work item.
/// Payload is the inner { type, attributes } event object expected under { data = ... }.
/// </summary>
public sealed record KlaviyoOrderWork(
    KlaviyoOrderEventType Type,
    object EventPayload,
    DateTimeOffset OccurredAt,
    string StoreAlias,
    string OrderId);

internal sealed class KlaviyoOrdersDispatcher
    : BatchingChannelDispatcher<KlaviyoOrderWork>, IKlaviyoOrdersDispatcher
{
    private readonly IKlaviyoOrdersClient _ordersClient;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoOrdersDispatcher> _logger;

    public KlaviyoOrdersDispatcher(
        IKlaviyoOrdersClient ordersClient,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoOrdersDispatcher> logger)
        : base(
            name: "OrdersDispatcher",
            dispatch: options.Value.Orders.Dispatching,
            logger: logger)
    {
        _ordersClient = ordersClient;
        _opt = options.Value;
        _logger = logger;
    }

    public ValueTask EnqueueAsync(KlaviyoOrderWork work, CancellationToken ct = default)
        => base.EnqueueAsync(work, ct);

    protected override Task<List<KlaviyoOrderWork>> PrepareBatchAsync(List<KlaviyoOrderWork> drained, CancellationToken ct)
    {
        if (!_opt.Enabled || !_opt.Orders.Enabled)
            return Task.FromResult(new List<KlaviyoOrderWork>(0));

        return Task.FromResult(drained);
    }

    protected override async Task HandleChunkAsync(KlaviyoOrderWork[] chunk, CancellationToken ct)
    {
        if (chunk.Length == 0) return;

        var maxConcurrency = Math.Max(1, _opt.Orders.Dispatching.MaxConcurrency);

        using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

        var tasks = chunk.Select(async work =>
        {
            await semaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                await _ordersClient.TrackOrderEventAsync(work.EventPayload, work.StoreAlias, ct).ConfigureAwait(false);

                _logger.LogDebug(
                    "Klaviyo OrdersDispatcher sent {Type} OrderId={OrderId} OccurredAt={OccurredAt} StoreAlias={StoreAlias}.",
                    work.Type, work.OrderId, work.OccurredAt, work.StoreAlias);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks).ConfigureAwait(false);
        _logger.LogDebug("Klaviyo OrdersDispatcher sent {Count} order events in chunk.", chunk.Length);
    }
}

