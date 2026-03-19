using Ekom.Klaviyo.Models.Orders;

namespace Ekom.Klaviyo.Enrichers.OrderEnricher;

public interface IKlaviyoPlacedOrderEnricherRunner
{
    ValueTask ApplyAsync(KlaviyoPlacedOrder order, CancellationToken ct);
}

internal sealed class KlaviyoPlacedOrderEnricherRunner : IKlaviyoPlacedOrderEnricherRunner
{
    private readonly KlaviyoPlacedOrderEnrichmentPipeline _pipeline;

    public KlaviyoPlacedOrderEnricherRunner(KlaviyoPlacedOrderEnrichmentPipeline pipeline)
        => _pipeline = pipeline;

    public ValueTask ApplyAsync(KlaviyoPlacedOrder order, CancellationToken ct)
        => _pipeline.ApplyAsync(order, ct);
}
