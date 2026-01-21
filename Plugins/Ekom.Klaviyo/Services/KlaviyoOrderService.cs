using Ekom.Klaviyo.Models;

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
    public ValueTask TrackCancelledOrderAsync(KlaviyoCancelledOrder payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask TrackFulfilledOrderAsync(KlaviyoFulfilledOrder payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask TrackPlacedOrderAsync(KlaviyoPlacedOrder payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask TrackRefundedOrderAsync(KlaviyoRefundedOrder payload, CancellationToken ct = default)
    {
        throw new NotImplementedException();
    }
}
