using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Models.Orders;
using Ekom.Models;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Ekom.Klaviyo.Mappers;

public static class OrderLineMapper
{
    public static KlaviyoOrderLine ToKlaviyoOrderLine(this IOrderLine ol, KlaviyoOptions opt, string? culture = null)
    {
        using var cultureScope = KlaviyoCultureScope.Apply(culture);

        var product = API.Catalog.Instance.GetProduct(ol.ProductKey, ol.OrderInfo.StoreInfo.Alias);

        var categories = product?.Categories.Select(x => x.Title).ToList();

        var orderlineAmount = ol.Amount;
        var productPrice = ol.Product?.Price;
        var imageBaseUrl = string.IsNullOrWhiteSpace(opt.ImageBaseUrl) ? opt.SiteBaseUrl : opt.ImageBaseUrl;

        var orderLine = new KlaviyoOrderLine
        {
            ProductExternalId = $"{ol.OrderInfo.StoreInfo.Alias}:{ol.ProductKey}",
            Sku = ol.Product?.SKU,
            Name = ol.Product?.Title ?? "",
            LineTotal = orderlineAmount.WithVat.Value,
            UnitPrice = productPrice?.WithVat.Value ?? 0,
            LineTotalFormatted = orderlineAmount.WithVat.CurrencyString,
            UnitPriceFormatted = productPrice?.WithVat.CurrencyString ?? "",
            LineTotalWithOutVat = orderlineAmount.WithoutVat.Value,
            UnitPriceWithOutVat = productPrice?.WithoutVat.Value ?? 0,
            LineTotalWithOutVatFormatted = orderlineAmount.WithoutVat.CurrencyString,
            UnitPriceWithOutVatFormatted = productPrice?.WithoutVat.CurrencyString ?? "",
            VatValue = orderlineAmount.Vat.Value,
            VatValueFormatted = orderlineAmount.Vat.CurrencyString,
            VatPercentage = FormatVatPercentage(ol.Vat),
            Discount = ol.Amount.DiscountAmount.Value,
            DiscountFormatted = ol.Amount.DiscountAmount.CurrencyString,
            Quantity = ol.Quantity,
            ProductUrl = string.IsNullOrWhiteSpace(ol.Product?.Url) ? null : UrlBuilder.Combine(opt.SiteBaseUrl, ol.Product.Url),
            ImageUrl = string.IsNullOrWhiteSpace(ol.Product?.Images.FirstOrDefault()?.Url) ? null : UrlBuilder.Combine(imageBaseUrl, ol.Product.Images.FirstOrDefault()?.Url ?? ""),
            Categories = categories
        };

        if (ol.Variant != null)
        {
            orderLine.Variant = new KlaviyoVariantOrderLine()
            {
                Sku = ol.Variant.SKU,
                Name = ol.Variant.Title,
                ImageUrl = string.IsNullOrWhiteSpace(ol.Variant?.Images.FirstOrDefault()?.Url) ? null : UrlBuilder.Combine(imageBaseUrl, ol.Variant.Images.FirstOrDefault()?.Url ?? ""),
            };
        }

        return orderLine;
    }

    public static IEnumerable<KlaviyoOrderLine> ToKlaviyoOrderLines(this IEnumerable<IOrderLine> orderlines, KlaviyoOptions opt, string? culture = null)
    {
        return orderlines.Select(x => x.ToKlaviyoOrderLine(opt, culture));
    }
    internal static JsonArray ToOrderLinesEvent(this IEnumerable<KlaviyoOrderLine> lines)
    {
        var arr = new JsonArray();

        foreach (var i in lines ?? Enumerable.Empty<KlaviyoOrderLine>())
        {
            var item = new JsonObject
            {
                ["product_id"] = i.ProductExternalId,
                ["sku"] = i.Sku,
                ["name"] = i.Name,
                ["unit_price"] = i.UnitPrice,
                ["line_total"] = i.LineTotal,
                ["unit_price_formatted"] = i.UnitPriceFormatted,
                ["line_total_formatted"] = i.LineTotalFormatted,
                ["unit_price_without_vat"] = i.UnitPriceWithOutVat,
                ["line_total_without_vat"] = i.LineTotalWithOutVat,
                ["unit_price_without_vat_formatted"] = i.UnitPriceWithOutVatFormatted,
                ["line_total_without_vat_formatted"] = i.LineTotalWithOutVatFormatted,
                ["vat_value"] = i.VatValue,
                ["vat_value_formatted"] = i.VatValueFormatted,
                ["vat_percentage"] = i.VatPercentage,
                ["quantity"] = i.Quantity,
                ["product_url"] = i.ProductUrl,
                ["image_url"] = i.ImageUrl,
            };

            if (i.Variant != null)
            {
                var variant = new JsonObject
                {
                    ["sku"] = i.Variant.Sku,
                    ["name"] = i.Variant.Name,
                    ["image_url"] = i.Variant.ImageUrl,
                };

                item["variant"] = variant;
            }


            if (i.Categories is { Count: > 0 })
                item["categories"] = new JsonArray(i.Categories.Select(c => (JsonNode?)c).ToArray());

            CustomPropertiesMerger.MergeCustomProperties(item, i.CustomProperties);

            arr.Add(item);
        }

        return arr;
    }

    private static string FormatVatPercentage(decimal vat)
    {
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{vat * 100m:0.##}%");
    }

}
