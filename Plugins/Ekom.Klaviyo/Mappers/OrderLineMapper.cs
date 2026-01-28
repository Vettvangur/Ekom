using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Models;
using Ekom.Models;

namespace Ekom.Klaviyo.Mappers;

public static class OrderLineMapper
{
    public static KlaviyoOrderLine ToKlaviyoOrderLine(this IOrderLine ol, KlaviyoOptions opt)
    {
        var product = API.Catalog.Instance.GetProduct(ol.ProductKey, ol.OrderInfo.StoreInfo.Alias);

        var categories = product?.Categories.Select(x => x.Title).ToList() ?? null;

        return new KlaviyoOrderLine
        {
            ProductExternalId = $"{ol.OrderInfo.StoreInfo.Alias}:{ol.ProductKey}",
            Sku = ol.Product?.SKU,
            Name = ol.Product?.Title ?? "",
            LineTotal = ol.Amount.Value,
            UnitPrice = (ol.Product?.Price.WithVat.Value ?? 0) / ol.Quantity,
            Quantity = ol.Quantity,
            ProductUrl = string.IsNullOrWhiteSpace(ol.Product?.Url) ? null : UrlBuilder.Combine(opt.SiteBaseUrl, ol.Product.Url),
            ImageUrl = string.IsNullOrWhiteSpace(ol.Product?.Images.FirstOrDefault()?.Url) ? null : UrlBuilder.Combine(opt.SiteBaseUrl, ol.Product.Images.FirstOrDefault()?.Url ?? ""),
            Categories = categories
        };
    }

    public static IEnumerable<KlaviyoOrderLine> ToKlaviyoOrderLines(this IEnumerable<IOrderLine> orderlines, KlaviyoOptions opt)
    {
        return orderlines.Select(x => x.ToKlaviyoOrderLine(opt));
    }
}
