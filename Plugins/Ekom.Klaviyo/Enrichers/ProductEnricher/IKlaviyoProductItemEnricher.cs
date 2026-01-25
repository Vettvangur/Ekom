using Ekom.Klaviyo.Models;

namespace Ekom.Klaviyo.Enrichers.ProductEnricher;

public interface IKlaviyoProductItemEnricher
{
    ValueTask EnrichAsync(KlaviyoProductItem item, KlaviyoProductEnrichmentContext ctx, CancellationToken ct);
}
