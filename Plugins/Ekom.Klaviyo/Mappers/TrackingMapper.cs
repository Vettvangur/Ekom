using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Models.Orders;
using Ekom.Klaviyo.Models.Tracking;
using System;
using System.Text.Json.Nodes;

namespace Ekom.Klaviyo.Mappers;

internal static class TrackingMapper
{
    internal static object ToTrackingEvent(this KlaviyoSearchEvent e, KlaviyoOptions opt)
    {
        var properties = new JsonObject
        {
            ["store_alias"] = e.StoreAlias
        };

        if (!string.IsNullOrWhiteSpace(e.Query))
            properties["query"] = e.Query;

        if (e.ResultsCount.HasValue)
            properties["results_count"] = e.ResultsCount.Value;

        AddDictionary(properties, "filters", e.Filters);

        CustomPropertiesMerger.MergeCustomProperties(properties, e.CustomProperties);

        return CreateEventPayload("Search", e.StoreAlias, e.EventId, e.OccurredAt, e.Customer, properties, opt);
    }

    internal static object ToTrackingEvent(this KlaviyoAddedToCartEvent e, KlaviyoOptions opt)
    {
        var properties = new JsonObject
        {
            ["store_alias"] = e.StoreAlias
        };

        if (!string.IsNullOrWhiteSpace(e.ProductId))
            properties["product_id"] = e.ProductId;

        if (!string.IsNullOrWhiteSpace(e.Sku))
            properties["sku"] = e.Sku;

        if (!string.IsNullOrWhiteSpace(e.ProductName))
            properties["product_name"] = e.ProductName;

        if (e.Quantity.HasValue)
            properties["quantity"] = e.Quantity.Value;

        if (e.Price.HasValue)
            properties["price"] = e.Price.Value;

        if (!string.IsNullOrWhiteSpace(e.PriceFormatted))
            properties["price_formatted"] = e.PriceFormatted;

        if (!string.IsNullOrWhiteSpace(e.Currency))
            properties["currency"] = e.Currency;

        if (!string.IsNullOrWhiteSpace(e.ProductUrl))
            properties["product_url"] = e.ProductUrl;

        CustomPropertiesMerger.MergeCustomProperties(properties, e.CustomProperties);

        return CreateEventPayload("Added to Cart", e.StoreAlias, e.EventId, e.OccurredAt, e.Customer, properties, opt);
    }

    internal static object ToTrackingEvent(this KlaviyoViewedCategoryEvent e, KlaviyoOptions opt)
    {
        var properties = new JsonObject
        {
            ["store_alias"] = e.StoreAlias
        };

        if (!string.IsNullOrWhiteSpace(e.CategoryId))
            properties["category_id"] = e.CategoryId;

        if (!string.IsNullOrWhiteSpace(e.CategoryName))
            properties["category_name"] = e.CategoryName;

        if (!string.IsNullOrWhiteSpace(e.CategoryUrl))
            properties["category_url"] = e.CategoryUrl;

        CustomPropertiesMerger.MergeCustomProperties(properties, e.CustomProperties);

        return CreateEventPayload("Viewed Category", e.StoreAlias, e.EventId, e.OccurredAt, e.Customer, properties, opt);
    }

    internal static object ToTrackingEvent(this KlaviyoViewedProductEvent e, KlaviyoOptions opt)
    {
        var properties = new JsonObject
        {
            ["store_alias"] = e.StoreAlias
        };

        if (!string.IsNullOrWhiteSpace(e.ProductId))
            properties["product_id"] = e.ProductId;

        if (!string.IsNullOrWhiteSpace(e.Sku))
            properties["sku"] = e.Sku;

        if (!string.IsNullOrWhiteSpace(e.ProductName))
            properties["product_name"] = e.ProductName;

        if (e.Price.HasValue)
            properties["price"] = e.Price.Value;

        if (!string.IsNullOrWhiteSpace(e.PriceFormatted))
            properties["price_formatted"] = e.PriceFormatted;

        if (!string.IsNullOrWhiteSpace(e.Currency))
            properties["currency"] = e.Currency;

        if (!string.IsNullOrWhiteSpace(e.ProductUrl))
            properties["product_url"] = e.ProductUrl;

        if (!string.IsNullOrWhiteSpace(e.ImageUrl))
            properties["image_url"] = e.ImageUrl;

        CustomPropertiesMerger.MergeCustomProperties(properties, e.CustomProperties);

        return CreateEventPayload("Viewed Product", e.StoreAlias, e.EventId, e.OccurredAt, e.Customer, properties, opt);
    }

    internal static object ToTrackingEvent(this KlaviyoActiveOnSiteEvent e, KlaviyoOptions opt)
    {
        var properties = new JsonObject
        {
            ["store_alias"] = e.StoreAlias
        };

        if (!string.IsNullOrWhiteSpace(e.Url))
            properties["url"] = e.Url;

        if (!string.IsNullOrWhiteSpace(e.Referrer))
            properties["referrer"] = e.Referrer;

        if (e.DurationSeconds.HasValue)
            properties["duration_seconds"] = e.DurationSeconds.Value;

        CustomPropertiesMerger.MergeCustomProperties(properties, e.CustomProperties);

        return CreateEventPayload("Active on Site", e.StoreAlias, e.EventId, e.OccurredAt, e.Customer, properties, opt);
    }

    internal static object ToTrackingEvent(this KlaviyoStartedCheckoutEvent e, KlaviyoOptions opt)
    {
        var properties = new JsonObject
        {
            ["store_alias"] = e.StoreAlias,
            ["occurred_at"] = e.OccurredAt.ToKlaviyoDateTime()
        };

        if (!string.IsNullOrWhiteSpace(e.OrderId))
            properties["cart_id"] = e.OrderId;

        if (!string.IsNullOrWhiteSpace(e.OrderNumber))
            properties["order_number"] = e.OrderNumber;

        if (e.Value.HasValue)
            properties["value"] = e.Value.Value;

        if (!string.IsNullOrWhiteSpace(e.ValueFormatted))
            properties["value_formatted"] = e.ValueFormatted;

        if (!string.IsNullOrWhiteSpace(e.Currency))
            properties["currency"] = e.Currency;

        if (!string.IsNullOrWhiteSpace(e.CheckoutUrl))
            properties["checkout_url"] = e.CheckoutUrl;

        if (e.Items is { Count: > 0 })
            properties["items"] = e.Items.ToOrderLinesEvent();

        CustomPropertiesMerger.MergeCustomProperties(properties, e.CustomProperties);

        return CreateEventPayload("Started Checkout", e.StoreAlias, e.EventId, e.OccurredAt, e.Customer, properties, opt);
    }

    internal static object ToTrackingEvent(this KlaviyoCartEmptiedEvent e, KlaviyoOptions opt)
    {
        var properties = new JsonObject
        {
            ["store_alias"] = e.StoreAlias,
            ["occurred_at"] = e.OccurredAt.ToKlaviyoDateTime()
        };

        if (!string.IsNullOrWhiteSpace(e.OrderId))
            properties["cart_id"] = e.OrderId;

        if (!string.IsNullOrWhiteSpace(e.OrderNumber))
            properties["order_number"] = e.OrderNumber;

        if (!string.IsNullOrWhiteSpace(e.Currency))
            properties["currency"] = e.Currency;

        properties["item_count"] = 0;
        properties["cart_is_empty"] = true;

        CustomPropertiesMerger.MergeCustomProperties(properties, e.CustomProperties);

        return CreateEventPayload("Cart Emptied", e.StoreAlias, e.EventId, e.OccurredAt, e.Customer, properties, opt);
    }

    private static object CreateEventPayload(
        string metricName,
        string storeAlias,
        string? eventId,
        DateTimeOffset occurredAt,
        KlaviyoOrderProfile customer,
        JsonObject properties,
        KlaviyoOptions opt)
    {
        if (occurredAt == default)
            occurredAt = DateTimeOffset.UtcNow;

        var uniqueId = BuildUniqueId(storeAlias, metricName, eventId, opt.Testing);
        var metricNameWithTest = metricName + (opt.Testing ? " Test" : "");

        return new
        {
            type = "event",
            attributes = new
            {
                unique_id = uniqueId,
                metric = new
                {
                    data = new
                    {
                        type = "metric",
                        attributes = new { name = metricNameWithTest }
                    }
                },
                profile = new
                {
                    data = new
                    {
                        type = "profile",
                        attributes = customer.ToProfileAttributes()
                    }
                },
                time = occurredAt.ToKlaviyoDateTime(),
                properties
            }
        };
    }

    private static string BuildUniqueId(string storeAlias, string metricName, string? eventId, bool testing)
    {
        var id = string.IsNullOrWhiteSpace(eventId)
            ? Guid.NewGuid().ToString("N")
            : eventId;

        var suffix = testing ? ":Test" : string.Empty;

        return $"{storeAlias}:{metricName}:{id}{suffix}";
    }

    private static void AddDictionary(JsonObject properties, string key, IDictionary<string, object?>? dict)
    {
        if (dict is null || dict.Count == 0) return;

        var obj = new JsonObject();

        foreach (var (k, v) in dict)
        {
            if (string.IsNullOrWhiteSpace(k)) continue;
            if (v is null) continue;
            if (v is string s && string.IsNullOrWhiteSpace(s)) continue;

            obj[k] = JsonValue.Create(v);
        }

        if (obj.Count > 0)
            properties[key] = obj;
    }
}
