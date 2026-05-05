using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Models.Orders;
using Ekom.Klaviyo.Models.Profiles;
using Ekom.Models;
using System.Text.Json.Nodes;
using Umbraco.Extensions;

namespace Ekom.Klaviyo.Mappers;

public static class OrderMapper
{
    public static KlaviyoPlacedOrder ToKlaviyoPlacedOrder(this IOrderInfo order, KlaviyoOptions opt, DateTimeOffset placedAt)
    {

        var storeOptions = opt.Stores.FirstOrDefault(x => x.Alias.InvariantEquals(order.StoreInfo.Alias));

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

        var shippingProvider = order.ShippingProvider?.ToKlaviyoShippingProvider() ?? null;
        var paymentProvider = order.PaymentProvider?.ToKlaviyoPaymentProvider() ?? null;

        KlaviyoProfileSubscribeRequest? consent = null;

        var consentValue = order.CustomerInformation.Customer.Value("customerKlaviyoConsentToSubscribe");

        if (consentValue.IsBoolean())
        {
            consent = new KlaviyoProfileSubscribeRequest(
                StoreAlias: order.StoreInfo.Alias,
                Email: order.CustomerInformation.Customer.Email ?? string.Empty,
                Consents: new List<KlaviyoProfileConsentChange>
                {
                    new KlaviyoProfileConsentChange(
                        Channel: KlaviyoProfileConsentChannel.Email,
                        State: KlaviyoProfileConsentState.Subscribed,
                        Source: "checkout",
                        TimestampUtc: DateTimeOffset.UtcNow,
                        ConsentTextVersion: "checkout-v1", 
                        Ip: order.CustomerInformation.CustomerIpAddress),
                    new KlaviyoProfileConsentChange(
                        Channel: KlaviyoProfileConsentChannel.Sms,
                        State: KlaviyoProfileConsentState.Subscribed,
                        Source: "checkout",
                        TimestampUtc: DateTimeOffset.UtcNow,
                        ConsentTextVersion: "checkout-v1",
                        Ip: order.CustomerInformation.CustomerIpAddress)
                }
            );
        }

        return new KlaviyoPlacedOrder
        {
            OrderId = order.KlaviyoUniqueId(),
            OrderNumber = order.OrderNumber,
            PlacedAt = placedAt,
            CreatedAt = order.CreateDate,
            PaidAt = order.PaidDate,
            Value = order.ChargedAmount.Value,
            ValueFormatted = order.ChargedAmount.CurrencyString,
            Currency = order.StoreInfo.Currency.ISOCurrencySymbol,
            Customer = order.ToKlaviyoProfile(opt),
            ShipTo = klaviyoShipTo,
            StoreAlias = order.StoreInfo.Alias,
            Items = order.OrderLines.ToKlaviyoOrderLines(opt).ToList(),
            TaxValue = order.Vat.Value,
            DiscountValue = order.DiscountAmount.Value,
            CheckoutUrl = storeOptions?.CheckoutUrl,
            ShippingProvider = shippingProvider,
            PaymentProvider = paymentProvider,
            Consent = consent
        };
    }

    public static IEnumerable<KlaviyoPlacedOrder> ToKlaviyoPlacedOrders(this IEnumerable<IOrderInfo> orders, KlaviyoOptions opt, DateTimeOffset placedAt)
    {
        return orders.Select(x => x.ToKlaviyoPlacedOrder(opt, DateTimeOffset.UtcNow));
    }

    internal static object ToPlacedOrderEvent(this KlaviyoPlacedOrder o, KlaviyoOptions opt)
    {
        JsonObject? shippingTo = null;
        var itemSkus = o.Items
            .Select(x => x.Sku)
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var itemNames = o.Items
            .Select(x => x.Name)
            .Where(x => !string.IsNullOrWhiteSpace(x));
        var itemCount = o.Items.Sum(x => x.Quantity);

        if (o.ShipTo is not null)
        {
            shippingTo = new JsonObject();

            if (!string.IsNullOrWhiteSpace(o.ShipTo.Email))
                shippingTo["email"] = o.ShipTo.Email;

            if (!string.IsNullOrWhiteSpace(o.ShipTo.Phone))
                shippingTo["phone_number"] = o.ShipTo.Phone;

            if (!string.IsNullOrWhiteSpace(o.ShipTo.FirstName))
                shippingTo["first_name"] = o.ShipTo.FirstName;

            if (!string.IsNullOrWhiteSpace(o.ShipTo.LastName))
                shippingTo["last_name"] = o.ShipTo.LastName;

            if (!string.IsNullOrWhiteSpace(o.ShipTo.Address))
                shippingTo["address1"] = o.ShipTo.Address;

            if (!string.IsNullOrWhiteSpace(o.ShipTo.ZipCode))
                shippingTo["zip"] = o.ShipTo.ZipCode;

            if (!string.IsNullOrWhiteSpace(o.ShipTo.City))
                shippingTo["city"] = o.ShipTo.City;

            if (!string.IsNullOrWhiteSpace(o.ShipTo.Region))
                shippingTo["region"] = o.ShipTo.Region;

            if (!string.IsNullOrWhiteSpace(o.ShipTo.Country))
                shippingTo["country"] = o.ShipTo.Country;

            // Optional: if nothing useful was added, set to null
            if (shippingTo.Count == 0)
                shippingTo = null;
        }

        var uniqueId = $"{o.StoreAlias}:{o.OrderId}{(opt.Testing ? ":Test" : "")}";

        var properties = new JsonObject
        {
            ["order_id"] = o.OrderId,
            ["order_number"] = o.OrderNumber,
            ["value"] = o.Value,
            ["value_formatted"] = o.ValueFormatted,
            ["currency"] = o.Currency,
            ["placed_at"] = o.PlacedAt.ToKlaviyoDateTime(),
            ["created_at"] = o.CreatedAt.ToKlaviyoDateTime(),
            ["paid_at"] = o.PaidAt.ToKlaviyoDateTime(),
            ["checkout_url"] = o.CheckoutUrl,
            ["payment_method"] = o.PaymentProvider?.ToPaymentProviderEvent() ?? null,
            ["discount_value"] = o.DiscountValue,
            ["shipping_method"] = o.ShippingProvider?.ToShippingProviderEvent() ?? null,
            ["tax_value"] = o.TaxValue,
            ["shipping_to"] = shippingTo,
            ["items"] = o.Items.ToOrderLinesEvent(),
            ["item_skus_text"] = string.Join("|", itemSkus),
            ["item_names_text"] = string.Join("|", itemNames),
            ["item_count"] = itemCount,
            ["store_alias"] = o.StoreAlias
        };

        CustomPropertiesMerger.MergeCustomProperties(properties, o.CustomProperties);

        return new
        {
            type = "event",
            attributes = new
            {
                unique_id = uniqueId,
                value = o.Value,
                value_currency = o.Currency,
                metric = new
                {
                    data = new
                    {
                        type = "metric",
                        attributes = new { name = "Placed Order" + (opt.Testing ? " Test" : "") }
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

                time = o.PlacedAt.ToKlaviyoDateTime(),
                properties
            }
        };
    }
}
