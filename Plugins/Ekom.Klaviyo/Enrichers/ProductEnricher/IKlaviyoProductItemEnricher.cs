using Ekom.Klaviyo.Models.Catalog;

namespace Ekom.Klaviyo.Enrichers.ProductEnricher;

public interface IKlaviyoProductItemEnricher
{
    ValueTask EnrichAsync(KlaviyoProductItem item, KlaviyoProductEnrichmentContext ctx, CancellationToken ct);
}
