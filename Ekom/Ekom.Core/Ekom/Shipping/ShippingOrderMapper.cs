using Ekom.Models;

namespace Ekom.Shipping;

internal sealed class ShippingOrderMapper : IShippingOrderMapper
{
    public ShipmentBookingRequest Map(
        IOrderInfo order,
        EkomShippingMethodConfiguration configuration,
        string? bookingReference)
    {
        ArgumentNullException.ThrowIfNull(order);
        var customer = order.CustomerInformation.Customer;
        var shipping = order.CustomerInformation.Shipping;
        var items = MapItems(order);
        var pickupLocationId = GetCustomValue(
            order,
            EkomShippingPropertyAliases.CustomPickupLocationId,
            "customshippingDroppLocationId");
        var pickupLocation = pickupLocationId is null
            ? null
            : new ShipmentPickupLocation(
                pickupLocationId,
                GetCustomValue(order, EkomShippingPropertyAliases.CustomPickupLocationName) ?? string.Empty,
                GetCustomValue(order, EkomShippingPropertyAliases.CustomPickupLocationAddress) ?? string.Empty,
                GetCustomValue(order, EkomShippingPropertyAliases.CustomPickupLocationPostalCode) ?? string.Empty,
                GetCustomValue(order, EkomShippingPropertyAliases.CustomPickupLocationCity) ?? string.Empty);

        return new ShipmentBookingRequest(
            order.OrderNumber,
            configuration.ServiceId,
            new ShipmentRecipient(
                Fallback(shipping.Name, customer.Name),
                Fallback(shipping.Email, customer.Email),
                Fallback(shipping.Phone, customer.Phone),
                Fallback(shipping.Address, customer.Address),
                Fallback(shipping.ZipCode, customer.ZipCode),
                Fallback(shipping.City, customer.City),
                Fallback(shipping.Country, customer.Country),
                customer.Value("customerSSN")),
            items,
            order.OrderLineTotal.Value,
            order.StoreInfo.Currency.ISOCurrencySymbol,
            pickupLocationId,
            bookingReference,
            pickupLocation);
    }

    private static IReadOnlyList<ShipmentItem> MapItems(IOrderInfo order)
    {
        var items = new Dictionary<string, ShipmentItem>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in order.OrderLines)
        {
            var sku = !string.IsNullOrWhiteSpace(line.Variant?.SKU)
                ? line.Variant.SKU
                : line.Product.SKU;
            if (string.IsNullOrWhiteSpace(sku))
            {
                throw new ShippingException("Shipment booking requires every order line to have a SKU.");
            }

            if (line.Quantity <= 0 || line.Quantity % 1 != 0)
            {
                throw new ShippingException("Shipment booking requires whole, positive item quantities.");
            }

            var quantity = decimal.ToInt32(line.Quantity);
            if (items.TryGetValue(sku, out var existing))
            {
                items[sku] = existing with { Quantity = existing.Quantity + quantity };
                continue;
            }

            var name = $"{sku} - {line.Product.Title}";
            if (!string.IsNullOrWhiteSpace(line.Variant?.Title))
            {
                name += $" - {line.Variant.Title}";
            }

            items[sku] = new ShipmentItem(sku, name, quantity);
        }

        if (items.Count == 0)
        {
            throw new ShippingException("Shipment booking requires at least one order line.");
        }

        return items.Values.ToArray();
    }

    private static string? GetCustomValue(IOrderInfo order, params string[] aliases)
    {
        foreach (var alias in aliases)
        {
            if (order.ShippingProvider.CustomData.TryGetValue(alias, out var value) &&
                !string.IsNullOrWhiteSpace(value))
            {
                return System.Net.WebUtility.HtmlDecode(value);
            }
        }

        return null;
    }

    private static string Fallback(string preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
}
