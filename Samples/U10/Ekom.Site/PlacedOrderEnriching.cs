using Ekom.Klaviyo.Enrichers.OrderEnricher;
using Ekom.Klaviyo.Models.Orders;

namespace Ekom.Site;

public class CustomPlacedOrderEnriching : IKlaviyoPlacedOrderEnricher
{
    public ValueTask EnrichAsync(KlaviyoPlacedOrder order, CancellationToken ct)
    {
        order.CustomProperties["CustomKey"] = "CustomValue";
        return ValueTask.CompletedTask;
    }
}
