using Ekom.Klaviyo.Dispatching.Events;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Services;

public interface IKlaviyoOrderService
{
    ValueTask TrackPlacedOrderAsync(KlaviyoPlacedOrder payload, CancellationToken ct = default);
    ValueTask TrackFulfilledOrderAsync(KlaviyoFulfilledOrder payload, CancellationToken ct = default);
    ValueTask TrackCancelledOrderAsync(KlaviyoCancelledOrder payload, CancellationToken ct = default);
    ValueTask TrackRefundedOrderAsync(KlaviyoRefundedOrder payload, CancellationToken ct = default);
}

public sealed class KlaviyoOrderService : IKlaviyoOrderService
{
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoOrderService> _logger;
    private readonly IKlaviyoEventsDispatcher _dispatcher;
    public KlaviyoOrderService(
        IOptions<KlaviyoOptions> opt,
        ILogger<KlaviyoOrderService> logger,
        IKlaviyoEventsDispatcher dispatcher)
    {
        _opt = opt.Value;
        _logger = logger;
        _dispatcher = dispatcher;
    }

    public ValueTask TrackCancelledOrderAsync(KlaviyoCancelledOrder payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask TrackFulfilledOrderAsync(KlaviyoFulfilledOrder payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask TrackPlacedOrderAsync(KlaviyoPlacedOrder order, CancellationToken ct = default)
    {
        if (!_opt.Enabled || !_opt.Events.Enabled) return ValueTask.CompletedTask;

        if (!order.Customer.HasIdentifier)
        {
            _logger.LogWarning(
                "Klaviyo: skipping Placed Order {OrderId} because no customer identifier was provided.",
                order.OrderId);
            return ValueTask.CompletedTask;
        }

        var work = new KlaviyoEventWork(
            Name: "PlacedOrder",
            Payload: order.ToPlacedOrderEvent(),
            OccurredAt: order.PlacedAt,
            StoreAlias: order.StoreAlias
        );

        return _dispatcher.EnqueueAsync(work, ct);
    }

    public ValueTask TrackRefundedOrderAsync(KlaviyoRefundedOrder payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
