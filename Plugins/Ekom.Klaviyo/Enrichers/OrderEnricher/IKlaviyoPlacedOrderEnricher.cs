using Ekom.Klaviyo.Models.Orders;

namespace Ekom.Klaviyo.Enrichers.OrderEnricher;

public interface IKlaviyoPlacedOrderEnricher
{
    ValueTask EnrichAsync(KlaviyoPlacedOrder order, CancellationToken ct);
}
