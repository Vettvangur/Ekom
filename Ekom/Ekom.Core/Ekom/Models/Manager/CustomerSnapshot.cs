using System.Text.Json;

namespace Ekom.Models.Manager;

public sealed class CustomerSnapshot
{
    public Guid OrderId { get; set; }
    public string OrderNumber { get; set; } = "";
    public DateTime Date { get; set; }
    public bool Unavailable { get; set; }
    public Dictionary<string, string> Fields { get; } = new();

    public static CustomerSnapshot Parse(OrderData order)
    {
        var result = new CustomerSnapshot { OrderId = order.UniqueId, OrderNumber = order.OrderNumber ?? order.ReferenceId.ToString(), Date = order.CreateDate };
        try
        {
            using var doc = JsonDocument.Parse(order.OrderInfo ?? "{}");
            var root = doc.RootElement;
            foreach (var (label, path) in new (string, string)[]
            {
                ("Phone", "CustomerInformation.Customer.Phone"),
                ("SSN", "CustomerInformation.Customer.Properties.customerSSN"),
                ("Billing address", "CustomerInformation.Customer.Address"),
                ("Billing apartment", "CustomerInformation.Customer.Apartment"),
                ("Billing postcode", "CustomerInformation.Customer.ZipCode"),
                ("Billing city", "CustomerInformation.Customer.City"),
                ("Billing country", "CustomerInformation.Customer.Country"),
                ("Shipping name", "CustomerInformation.Shipping.Name"),
                ("Shipping address", "CustomerInformation.Shipping.Address"),
                ("Shipping apartment", "CustomerInformation.Shipping.Apartment"),
                ("Shipping postcode", "CustomerInformation.Shipping.ZipCode"),
                ("Shipping city", "CustomerInformation.Shipping.City"),
                ("Shipping country", "CustomerInformation.Shipping.Country"),
                ("Last payment method", "PaymentProvider.Title"),
                ("Last shipping method", "ShippingProvider.Title"),
                ("Marketing consent (order snapshot)", "Consent.Marketing"),
            })
            {
                var value = Read(root, path);
                if (!string.IsNullOrWhiteSpace(value)) result.Fields[label] = value;
            }
        }
        catch (JsonException)
        {
            result.Unavailable = true;
        }
        return result;
    }

    private static string? Read(JsonElement element, string path)
    {
        foreach (string part in path.Split('.'))
        {
            if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(part, out element)) return null;
        }
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString(),
            JsonValueKind.True => "Yes",
            JsonValueKind.False => "No",
            _ => null,
        };
    }
}
