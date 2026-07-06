using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Models.Catalog;
using Ekom.Models;

namespace Ekom.Klaviyo.Mappers;

public static class ProductMapper
{
    public static KlaviyoProductItem ToKlaviyoCatalogItem(this IProduct product, bool isPublished, string host, string? imageHost = null)
    {
        var effectiveImageHost = string.IsNullOrWhiteSpace(imageHost) ? host : imageHost;

        return new KlaviyoProductItem
        {
            Id = product.Key,
            Title = product.Title,
            Price = product.OriginalPrice.Value,
            Sku = product.SKU,
            StoreAlias = product.Store.Alias,
            Currency = product.OriginalPrice.Currency.ISOCurrencySymbol,
            Url = UrlBuilder.Combine(host, product.Url),
            Description = product.Description,
            Summary = product.Summary,
            ImageFullUrl = UrlBuilder.Combine(effectiveImageHost, product.Images.FirstOrDefault()?.Url ?? ""),
            Published = isPublished,
        };
    }

    public static IEnumerable<KlaviyoProductItem> ToKlaviyoCatalogItems(this IEnumerable<IProduct> products, bool isPublished, string host, string? imageHost = null)
    {
        return products.Select(x => x.ToKlaviyoCatalogItem(isPublished, host, imageHost));
    }
}
