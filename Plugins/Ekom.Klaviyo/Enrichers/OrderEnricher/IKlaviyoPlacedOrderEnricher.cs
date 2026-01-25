using Ekom.Klaviyo.Models;

namespace Ekom.Klaviyo.Enrichers.OrderEnricher;

public interface IKlaviyoPlacedOrderEnricher
{
    ValueTask EnrichAsync(KlaviyoPlacedOrder order, CancellationToken ct);
}
