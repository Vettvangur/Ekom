using Ekom.Klaviyo.Models;
using Ekom.Models;

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
                LastName = order.CustomerInformation.Customer.LastName
            },
            StoreAlias = order.StoreInfo.Alias,
            Items = order.OrderLines.ToKlaviyoOrderLines(host).ToList()
        };
    }

    public static IEnumerable<KlaviyoPlacedOrder> ToKlaviyoPlacedOrders(this IEnumerable<IOrderInfo> orders, string host)
    {
        return orders.Select(x => x.ToKlaviyoPlacedOrder(host));
    }

    public static object ToPlacedOrderEvent(this KlaviyoPlacedOrder o)
    {
        return new
        {
            type = "event",
            attributes = new
            {
                metric = new { data = new { type = "metric", attributes = new { name = "Placed Order" } } },

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
                            last_name = o.Customer.LastName
                        }
                    }
                },

                time = o.PlacedAt,

                properties = new
                {
                    order_id = o.OrderId,
                    value = o.Value,
                    currency = o.Currency,
                    checkout_url = o.CheckoutUrl,
                    payment_method = o.PaymentMethod,
                    discount_value = o.DiscountValue,
                    shipping_value = o.ShippingValue,
                    tax_value = o.TaxValue,
                    items = o.Items.Select(i => new
                    {
                        product_id = i.ProductExternalId,
                        sku = i.Sku,
                        name = i.Name,
                        price = i.Price,
                        quantity = i.Quantity,
                        product_url = i.ProductUrl,
                        image_url = i.ImageUrl,
                        categories = i.Categories
                    }).ToList()
                }
            }
        };
    }
}
