namespace Ekom.Mailchimp.Models;

internal static class MailchimpPurchaseValidator
{
    internal static void Validate(MailchimpPurchase purchase)
    {
        ArgumentNullException.ThrowIfNull(purchase);
        ArgumentException.ThrowIfNullOrWhiteSpace(purchase.StoreAlias);
        ArgumentException.ThrowIfNullOrWhiteSpace(purchase.StoreName);
        ValidateId(purchase.OrderId, nameof(purchase.OrderId));
        ArgumentException.ThrowIfNullOrWhiteSpace(purchase.CurrencyCode);
        if (purchase.CurrencyCode.Length != 3)
        {
            throw new ArgumentException("The currency code must be a three-letter ISO 4217 code.", nameof(purchase));
        }

        ArgumentNullException.ThrowIfNull(purchase.Customer);
        ValidateId(purchase.Customer.Id, nameof(purchase.Customer.Id));
        ArgumentException.ThrowIfNullOrWhiteSpace(purchase.Customer.Email);
        if (purchase.Lines == null || purchase.Lines.Count == 0)
        {
            throw new ArgumentException("A purchase must contain at least one line.", nameof(purchase));
        }

        foreach (MailchimpPurchaseLine line in purchase.Lines)
        {
            ValidateId(line.Id, nameof(line.Id));
            ValidateId(line.ProductId, nameof(line.ProductId));
            ValidateId(line.ProductVariantId, nameof(line.ProductVariantId));
            ArgumentException.ThrowIfNullOrWhiteSpace(line.ProductTitle);
            ArgumentException.ThrowIfNullOrWhiteSpace(line.VariantTitle);
            if (line.Quantity <= 0)
            {
                throw new ArgumentException("Purchase line quantities must be greater than zero.", nameof(purchase));
            }
        }

        if (!string.IsNullOrWhiteSpace(purchase.TrackingCode)
            && !string.Equals(purchase.TrackingCode, "prec", StringComparison.Ordinal))
        {
            throw new ArgumentException("Mailchimp only supports 'prec' as an order tracking code.", nameof(purchase));
        }
    }

    private static void ValidateId(string value, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value, name);
        if (value.Length > 50)
        {
            throw new ArgumentException($"Mailchimp identifier '{name}' cannot exceed 50 characters.", name);
        }
    }
}
