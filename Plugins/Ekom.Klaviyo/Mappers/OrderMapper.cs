using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Models;
using Ekom.Models;
using System.Text.Json.Nodes;

namespace Ekom.Klaviyo.Mappers;

internal static class OrderMapper
{
    public static KlaviyoPlacedOrder ToKlaviyoPlacedOrder(this IOrderInfo order, KlaviyoOptions opt)
    {

        var klaviyoShipTo = new KlaviyoShipTo()
        {
            Email = order.CustomerInformation.Customer.Email,
            Phone = order.CustomerInformation.Customer.Phone,
            FirstName = order.CustomerInformation.Customer.FirstName,
            LastName = order.CustomerInformation.Customer.LastName,
            Address = order.CustomerInformation.Customer.Address,
            ZipCode = order.CustomerInformation.Customer.ZipCode,
            City = order.CustomerInformation.Customer.City,
            Country = order.CustomerInformation.Customer.Country,
            Region = order.CustomerInformation.Customer.Region,
        };

        if (!order.CustomerInformation.IsBillingSameAsShipping)
        {
            klaviyoShipTo = new KlaviyoShipTo()
            {
                Email = order.CustomerInformation.Shipping.Email,
                Phone = order.CustomerInformation.Shipping.Phone,
                FirstName = order.CustomerInformation.Shipping.FirstName,
                LastName = order.CustomerInformation.Shipping.LastName,
                Address = order.CustomerInformation.Shipping.Address,
                ZipCode = order.CustomerInformation.Shipping.ZipCode,
                City = order.CustomerInformation.Shipping.City,
                Country = order.CustomerInformation.Shipping.Country,
                Region = order.CustomerInformation.Shipping.Region,
            };
        }

        return new KlaviyoPlacedOrder
        {
            OrderId = order.UniqueId.ToString(),
            OrderNumber = order.OrderNumber,
            PlacedAt = order.CreateDate,
            Value = order.ChargedAmount.Value,
            Currency = order.StoreInfo.Currency.ISOCurrencySymbol,
            Customer = order.ToKlaviyoProfile(opt),
            ShipTo = klaviyoShipTo,
            StoreAlias = order.StoreInfo.Alias,
            Items = order.OrderLines.ToKlaviyoOrderLines(opt.SiteBaseUrl).ToList(),
            PaymentProviderName = order.PaymentProvider?.Title,
            PaymentProviderValue = order.PaymentProvider?.Price.WithVat.Value,
            ShippingProviderName = order.ShippingProvider?.Title,
            ShippingProviderValue = order.ShippingProvider?.Price.WithVat.Value,
            TaxValue = order.Vat.Value,
            DiscountValue = order.DiscountAmount.Value,
            CheckoutUrl = null
        };
    }

    public static IEnumerable<KlaviyoPlacedOrder> ToKlaviyoPlacedOrders(this IEnumerable<IOrderInfo> orders, KlaviyoOptions opt)
    {
        return orders.Select(x => x.ToKlaviyoPlacedOrder(opt));
    }

    public static object ToPlacedOrderEvent(this KlaviyoPlacedOrder o)
    {
        JsonObject? shippingTo = null;

        if (o.ShipTo is not null)
        {
            shippingTo = new JsonObject
            {
                ["email"] = o.ShipTo.Email,
                ["phone_number"] = o.ShipTo.Phone,
                ["first_name"] = o.ShipTo.FirstName,
                ["last_name"] = o.ShipTo.LastName,
                ["address1"] = o.ShipTo.Address,
                ["zip"] = o.ShipTo.ZipCode,
                ["city"] = o.ShipTo.City,
                ["region"] = o.ShipTo.Region,
                ["country"] = o.ShipTo.Country,
            };
        }

        var properties = new JsonObject
        {
            ["order_id"] = o.OrderId,
            ["order_number"] = o.OrderNumber,
            ["value"] = o.Value,
            ["currency"] = o.Currency,
            ["checkout_url"] = o.CheckoutUrl,
            ["payment_method"] = new JsonObject
            {
                ["name"] = o.PaymentProviderName,
                ["price"] = o.PaymentProviderValue
            },
            ["discount_value"] = o.DiscountValue,
            ["shipping_method"] = new JsonObject
            {
                ["name"] = o.ShippingProviderName,
                ["price"] = o.ShippingProviderValue
            },
            ["tax_value"] = o.TaxValue,
            ["shipping_to"] = shippingTo,
            ["items"] = new JsonArray(
            o.Items.Select(i =>
            {
                var item = new JsonObject
                {
                    ["product_id"] = i.ProductExternalId,
                    ["sku"] = i.Sku,
                    ["name"] = i.Name,
                    ["unit_price"] = i.UnitPrice,
                    ["line_total"] = i.LineTotal,
                    ["quantity"] = i.Quantity,
                    ["product_url"] = i.ProductUrl,
                    ["image_url"] = i.ImageUrl,
                    ["categories"] = i.Categories is null
                        ? null
                        : new JsonArray(i.Categories.Select(c => (JsonNode?)c).ToArray())
                };

                CustomPropertiesMerger.MergeCustomProperties(item, i.CustomProperties);

                return item;
            }).ToArray()
            )
        };

        CustomPropertiesMerger.MergeCustomProperties(properties, o.CustomProperties);

        return new
        {
            type = "event",
            attributes = new
            {
                metric = new
                {
                    data = new
                    {
                        type = "metric",
                        attributes = new { name = "Placed Order" }
                    }
                },

                profile = new
                {
                    data = new
                    {
                        type = "profile",
                        attributes = o.Customer.ToProfileAttributes()
                    }
                },

                time = o.PlacedAt,
                properties
            }
        };
    }
}
