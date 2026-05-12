using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Models.Catalog;
using Ekom.Models;

namespace Ekom.Klaviyo.Mappers;

internal static class ProductFeedMapper
{
    public static KlaviyoProductFeedItem ToKlaviyoProductFeedItem(
        this IProduct product,
        KlaviyoOptions options,
        IReadOnlyDictionary<string, object?>? customAttributes = null)
    {
        var link = UrlBuilder.Combine(options.SiteBaseUrl, product.Url);

        var imageUrl = product.Images?.FirstOrDefault()?.Url;
        var imageLink = string.IsNullOrWhiteSpace(imageUrl)
            ? null
            : UrlBuilder.Combine(options.SiteBaseUrl, imageUrl + options.Catalog.ImageCrop);

        var price = options.Catalog.ShowPrice ? product.Price?.Value : null;

        IReadOnlyList<string>? categories = product.Categories.Select(x => x.Title).ToList();

        // Inventory: adapt to your model. If not available, leave null.
        decimal? inventoryQty = options.Catalog.ShowInventory ? product.Stock : null;

        // 1 = your example. If you have semantics, set it based on your system.
        int? inventoryPolicy = options.Catalog.InventoryPolicy;

        // Build default custom attributes and merge optional user-provided ones
        var mergedCustom = BuildDefaultCustomAttributes(product);

        if (customAttributes is not null)
        {
            foreach (var kv in customAttributes)
                mergedCustom[kv.Key] = kv.Value;
        }

        var description = !string.IsNullOrEmpty(product.Description) ? product.Description :
                          !string.IsNullOrEmpty(product.Summary) ? product.Summary : product.Title;

        return new KlaviyoProductFeedItem
        {
            Id = $"{product.Store.Alias}:{product.Key.ToString()}",
            Title = product.Title ?? string.Empty,
            Link = link,
            Description = description,
            Price = price,
            ImageLink = imageLink,
            Categories = categories,
            InventoryQuantity = inventoryQty,
            InventoryPolicy = inventoryPolicy,
            CustomAttributes = mergedCustom
        };
    }

    public static IEnumerable<KlaviyoProductFeedItem> ToKlaviyoProductFeedItems(
        this IEnumerable<IProduct> products,
        KlaviyoOptions options,
        Func<IProduct, IReadOnlyDictionary<string, object?>?>? customAttributesFactory = null)
        => products.Select(p =>
            p.ToKlaviyoProductFeedItem(
                options,
                customAttributesFactory?.Invoke(p)));

    private static Dictionary<string, object?> BuildDefaultCustomAttributes(IProduct product)
    {
        // Keep these stable and useful for downstream segmentation
        return new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["sku"] = product.SKU,
            ["store_alias"] = product.Store?.Alias,
            ["currency"] = product.Price?.Currency?.ISOCurrencySymbol ?? product.OriginalPrice?.Currency?.ISOCurrencySymbol,
            ["original_price"] = product.OriginalPrice?.Value,
            ["discount_price"] = product.Price?.Value,
            ["discount_amount"] = product.Price?.DiscountAmount?.Value,
            ["has_discount"] = product.Price?.HasDiscount ?? false,
            ["published"] = true,
            ["summary"] = product.Summary
        };
    }
}
