using Ekom.Klaviyo.Models;

namespace Ekom.Klaviyo.Enrichers.ProductEnricher;

internal interface IKlaviyoProductItemEnricher
{
    ValueTask EnrichAsync(KlaviyoProductItem item, KlaviyoProductEnrichmentContext ctx, CancellationToken ct);
}
