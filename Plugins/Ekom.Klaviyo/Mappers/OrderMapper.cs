using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Models;
using Ekom.Models;
using System.Text.Json.Nodes;

namespace Ekom.Klaviyo.Mappers;

internal static class OrderMapper
{
    public static KlaviyoPlacedOrder ToKlaviyoPlacedOrder(this IOrderInfo order, string host)
    {
        return new KlaviyoPlacedOrder
        {
            OrderId = order.UniqueId.ToString(),
            PlacedAt = order.CreateDate,
            Value = order.ChargedAmount.Value,
            Currency = order.StoreInfo.Currency.ISOCurrencySymbol,
            Customer = new KlaviyoCustomerIdentity
            {
                Email = order.CustomerInformation.Customer.Email,
                PhoneNumber = order.CustomerInformation.Customer.Phone,
                ExternalId = order.CustomerInformation.Customer.Email,
                FirstName = order.CustomerInformation.Customer.FirstName,
                LastName = order.CustomerInformation.Customer.LastName,
                Address = order.CustomerInformation.Customer.Address,
                ZipCode = order.CustomerInformation.Customer.ZipCode,
                City = order.CustomerInformation.Customer.City,
                Country = order.CustomerInformation.Customer.Country,
                Company = order.CustomerInformation.Customer.Company
            },
            StoreAlias = order.StoreInfo.Alias,
            Items = order.OrderLines.ToKlaviyoOrderLines(host).ToList(),
            PaymentProviderName = order.PaymentProvider?.Title,
            PaymentProviderValue = order.PaymentProvider?.Price.WithVat.Value,
            ShippingProviderName = order.ShippingProvider?.Title,
            ShippingProviderValue = order.ShippingProvider?.Price.WithVat.Value,
            TaxValue = order.Vat.Value,
            DiscountValue = order.DiscountAmount.Value,
            CheckoutUrl = null
        };
    }

    public static IEnumerable<KlaviyoPlacedOrder> ToKlaviyoPlacedOrders(this IEnumerable<IOrderInfo> orders, string host)
    {
        return orders.Select(x => x.ToKlaviyoPlacedOrder(host));
    }

    public static object ToPlacedOrderEvent(this KlaviyoPlacedOrder o)
    {
        var properties = new JsonObject
        {
            ["order_id"] = o.OrderId,
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
                        attributes = new
                        {
                            email = o.Customer.Email,
                            phone_number = o.Customer.PhoneNumber,
                            external_id = o.Customer.ExternalId,
                            first_name = o.Customer.FirstName,
                            last_name = o.Customer.LastName,
                            country = o.Customer.Country,
                            zip_code = o.Customer.ZipCode,
                            address = o.Customer.Address,
                            city = o.Customer.City,
                            company = o.Customer.Company
                        }
                    }
                },

                time = o.PlacedAt,
                properties
            }
        };
    }
}
