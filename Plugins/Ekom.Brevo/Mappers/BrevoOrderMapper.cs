using Ekom.Models;
using System.Globalization;
using System.Text.Json.Nodes;

namespace Ekom.Brevo.Mappers;

public static class BrevoOrderMapper
{
    public static JsonObject ToBrevoOrder(this IOrderInfo order)
    {
        ArgumentNullException.ThrowIfNull(order);
        var customer = order.CustomerInformation.Customer;
        BrevoConfiguration.ValidateEmail(customer.Email);
        ArgumentException.ThrowIfNullOrWhiteSpace(order.StoreInfo.Alias);
        var products = new JsonArray();
        foreach (var line in order.OrderLines)
        {
            if (line.Quantity <= 0) throw new InvalidOperationException("Brevo order lines require positive quantities.");
            var product = new JsonObject
            {
                ["productId"] = ScopedId(order.StoreInfo.Alias, line.ProductKey),
                ["price"] = line.Amount.WithVat.Value / line.Quantity,
                ["quantityFloat"] = line.Quantity,
                ["sku"] = line.Variant?.SKU ?? line.Product.SKU,
            };
            if (line.VariantKey.HasValue) product["variantId"] = ScopedId(order.StoreInfo.Alias, line.VariantKey.Value);
            products.Add(product);
        }
        if (products.Count == 0) throw new InvalidOperationException("Brevo orders require at least one product.");
        var billing = Address(customer.Address, customer.Apartment, customer.City, customer.Country,
            customer.ZipCode, customer.Region ?? customer.State);
        var shippingCustomer = order.CustomerInformation.Shipping;
        var shipping = order.CustomerInformation.IsBillingSameAsShipping ? billing.DeepClone() : Address(
            shippingCustomer.Address, shippingCustomer.Apartment, shippingCustomer.City, shippingCustomer.Country,
            shippingCustomer.ZipCode, shippingCustomer.Region);
        if (!string.IsNullOrWhiteSpace(customer.Phone)) billing["phone"] = customer.Phone;
        shipping["name"] = order.CustomerInformation.IsBillingSameAsShipping ? customer.Name : shippingCustomer.Name;
        string shippingPhone = order.CustomerInformation.IsBillingSameAsShipping ? customer.Phone : shippingCustomer.Phone;
        if (!string.IsNullOrWhiteSpace(shippingPhone)) shipping["phone"] = shippingPhone;
        return new JsonObject
        {
            ["id"] = ScopedId(order.StoreInfo.Alias, order.UniqueId),
            ["storeId"] = order.StoreInfo.Alias.ToUpperInvariant(),
            ["createdAt"] = Utc(order.CreateDate),
            ["updatedAt"] = Utc(order.PaidDate ?? order.UpdateDate),
            ["status"] = "completed",
            ["amount"] = order.GrandTotal.Value,
            ["identifiers"] = new JsonObject { ["email_id"] = customer.Email },
            ["products"] = products,
            ["billing"] = billing,
            ["shipping"] = shipping,
            ["metaInfo"] = new JsonObject
            {
                ["currency"] = order.StoreInfo.Currency.ISOCurrencySymbol,
                ["tax"] = order.ChargedVat.Value,
                ["shipping"] = order.ShippingProvider?.Price.WithVat.Value ?? 0,
                ["discount"] = order.DiscountAmountWithVat.Value,
                ["orderNumber"] = Convert.ToString(order.OrderNumber, CultureInfo.InvariantCulture),
            },
        };
    }

    internal static string ScopedId(string store, Guid id) => $"{store.ToUpperInvariant()}:{id:N}";

    private static JsonObject Address(string? address, string? apartment, string? city, string? country, string? postCode, string? region)
        => new()
        {
            ["address"] = string.Join(" ", new[] { address, apartment }.Where(x => !string.IsNullOrWhiteSpace(x))),
            ["city"] = city,
            ["country"] = country,
            ["postCode"] = postCode,
            ["region"] = region,
        };

    private static string Utc(DateTime value) => value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
}
