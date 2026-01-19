using Ekom.Klaviyo.Models;
using Ekom.Models;

namespace Ekom.Klaviyo.Mappers;

internal static class ProductMapper
{
    public static KlaviyoProductItem ToKlaviyoCatalogItem(this IProduct product, bool isPublished)
    {
        return new KlaviyoProductItem
        {
            Id = product.Key,
            Title = product.Title,
            Price = product.OriginalPrice.Value,
            Sku = product.SKU,
            StoreAlias = product.Store.Alias,
            Currency = product.OriginalPrice.Currency.ISOCurrencySymbol,
            Url = product.Url,
            Description = product.Summary,
            ImageFullUrl = product.Images.FirstOrDefault()?.Url,
            Published = isPublished,
        };
    }

    public static IEnumerable<KlaviyoProductItem> ToKlaviyoCatalogItems(this IEnumerable<IProduct> products, bool isPublished)
    {
        return products.Select(x => x.ToKlaviyoCatalogItem(isPublished));
    }
}
