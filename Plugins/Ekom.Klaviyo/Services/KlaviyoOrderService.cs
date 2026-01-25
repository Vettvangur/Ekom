using Ekom.Klaviyo.Dispatching.Orders;
using Ekom.Klaviyo.Enrichers.OrderEnricher;
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
    private readonly IKlaviyoOrdersDispatcher _dispatcher;
    private readonly IKlaviyoPlacedOrderEnricherRunner _placedOrderEnrichers;

    public KlaviyoOrderService(
        IOptions<KlaviyoOptions> opt,
        ILogger<KlaviyoOrderService> logger,
        IKlaviyoOrdersDispatcher dispatcher,
        IKlaviyoPlacedOrderEnricherRunner placedOrderEnrichers)
    {
        _opt = opt.Value;
        _logger = logger;
        _dispatcher = dispatcher;
        _placedOrderEnrichers = placedOrderEnrichers;
    }

    public ValueTask TrackCancelledOrderAsync(KlaviyoCancelledOrder payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask TrackFulfilledOrderAsync(KlaviyoFulfilledOrder payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public async ValueTask TrackPlacedOrderAsync(KlaviyoPlacedOrder order, CancellationToken ct = default)
    {
        if (!_opt.Enabled || !_opt.Orders.Enabled) return;

        if (!order.Customer.HasIdentifier)
        {
            _logger.LogWarning("Klaviyo: skipping Placed Order {OrderId} because no customer identifier was provided.", order.OrderId);
            return;
        }

        await _placedOrderEnrichers.ApplyAsync(
            order,
            ct);

        var work = new KlaviyoOrderWork(
            Type: KlaviyoOrderEventType.PlacedOrder,
            EventPayload: order.ToPlacedOrderEvent(),
            OccurredAt: order.PlacedAt,
            StoreAlias: order.StoreAlias,
            OrderId: order.OrderId);

        await _dispatcher.EnqueueAsync(work, ct);
    }

    public ValueTask TrackRefundedOrderAsync(KlaviyoRefundedOrder payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
