using Ekom.Mailchimp.Helpers;
using Ekom.Mailchimp.Models;
using Ekom.Models;

namespace Ekom.Mailchimp.Mappers;

public static class MailchimpOrderMapper
{
    private const string MailchimpTrackingPropertyName = "Mailchimp";
    private const string CampaignIdPropertyName = "CampaignId";
    private const string TrackingCodePropertyName = "TrackingCode";

    public static MailchimpPurchase ToMailchimpPurchase(this IOrderInfo order, MailchimpOptions options)
    {
        ArgumentNullException.ThrowIfNull(order);
        ArgumentNullException.ThrowIfNull(options);

        Customer customer = order.CustomerInformation.Customer;
        if (string.IsNullOrWhiteSpace(customer.Email))
        {
            throw new InvalidOperationException($"Order '{order.UniqueId}' has no customer email address.");
        }

        string storeAlias = order.StoreInfo.Alias;
        MailchimpStoreOptions? storeOptions = options.Stores.FirstOrDefault(x =>
            string.Equals(x.Alias, storeAlias, StringComparison.OrdinalIgnoreCase));
        Uri? siteBaseUrl = storeOptions?.SiteBaseUrl ?? options.SiteBaseUrl;
        MailchimpAddress billingAddress = ToAddress(customer);
        MailchimpAddress shippingAddress = order.CustomerInformation.IsBillingSameAsShipping
            ? billingAddress
            : ToAddress(order.CustomerInformation.Shipping);

        return new MailchimpPurchase
        {
            StoreAlias = storeAlias,
            StoreName = storeAlias,
            OrderId = order.UniqueId.ToString("N"),
            OrderNumber = order.OrderNumber,
            CurrencyCode = order.StoreInfo.Currency.ISOCurrencySymbol,
            OrderTotal = order.ChargedAmount.Value,
            TaxTotal = order.ChargedVat.Value,
            ShippingTotal = order.ShippingProvider?.Price.WithVat.Value ?? 0,
            DiscountTotal = order.DiscountAmount.Value,
            ProcessedAt = ToDateTimeOffset(order.PaidDate ?? order.UpdateDate),
            Customer = new MailchimpPurchaseCustomer
            {
                Id = MailchimpSubscriberHash.Create(customer.Email),
                Email = customer.Email.Trim(),
                FirstName = NullIfWhiteSpace(customer.FirstName),
                LastName = NullIfWhiteSpace(customer.LastName),
                MarketingOptIn = bool.TryParse(customer.Value("customerMailchimpConsentToSubscribe"), out bool consent) && consent,
                Address = billingAddress,
            },
            BillingAddress = billingAddress,
            ShippingAddress = shippingAddress,
            CampaignId = ResolveCampaignId(order.Tracking, customer, order.Consent?.Marketing == true),
            TrackingCode = ResolveTrackingCode(order.Tracking, customer, order.Consent?.Marketing == true),
            Lines = order.OrderLines.Select(x => ToPurchaseLine(x, siteBaseUrl)).ToArray(),
        };
    }

    private static MailchimpPurchaseLine ToPurchaseLine(IOrderLine line, Uri? siteBaseUrl)
    {
        string productId = line.ProductKey.ToString("N");
        string variantId = line.VariantKey.HasValue
            ? line.VariantKey.Value.ToString("N")
            : $"{productId}:default";
        if (line.Quantity != decimal.Truncate(line.Quantity)
            || line.Quantity is < int.MinValue or > int.MaxValue)
        {
            throw new InvalidOperationException(
                $"Order line '{line.Key}' has quantity '{line.Quantity}', but Mailchimp requires an integer quantity.");
        }

        int quantity = decimal.ToInt32(line.Quantity);
        decimal unitPrice = quantity == 0 ? 0 : line.Amount.WithVat.Value / quantity;
        string? imageUrl = line.Variant?.Images.FirstOrDefault()?.Url
            ?? line.Product.Images.FirstOrDefault()?.Url;
        decimal catalogPrice = line.Variant?.Price.WithVat.Value ?? line.Product.Price.WithVat.Value;

        return new MailchimpPurchaseLine
        {
            Id = line.Key.ToString("N"),
            ProductId = productId,
            ProductVariantId = variantId,
            ProductTitle = line.Product.Title,
            VariantTitle = line.Variant?.Title ?? line.Product.Title,
            Sku = NullIfWhiteSpace(line.Variant?.SKU) ?? NullIfWhiteSpace(line.Product.SKU),
            Quantity = quantity,
            Price = unitPrice,
            CatalogPrice = catalogPrice,
            ProductUrl = CombineUrl(siteBaseUrl, line.Product.Url),
            ImageUrl = CombineUrl(siteBaseUrl, imageUrl),
        };
    }

    private static MailchimpAddress ToAddress(Customer customer) => new()
    {
        Address1 = NullIfWhiteSpace(customer.Address),
        Address2 = NullIfWhiteSpace(customer.Apartment),
        City = NullIfWhiteSpace(customer.City),
        Province = NullIfWhiteSpace(customer.Region) ?? NullIfWhiteSpace(customer.State),
        PostalCode = NullIfWhiteSpace(customer.ZipCode),
        Country = NullIfWhiteSpace(customer.Country),
    };

    private static MailchimpAddress ToAddress(CustomerShippingInfo customer) => new()
    {
        Address1 = NullIfWhiteSpace(customer.Address),
        Address2 = NullIfWhiteSpace(customer.Apartment),
        City = NullIfWhiteSpace(customer.City),
        Province = NullIfWhiteSpace(customer.Region),
        PostalCode = NullIfWhiteSpace(customer.ZipCode),
        Country = NullIfWhiteSpace(customer.Country),
    };

    private static Uri? CombineUrl(Uri? baseUrl, string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return null;
        }

        if (Uri.TryCreate(path, UriKind.Absolute, out Uri? absolute))
        {
            return absolute;
        }

        if (baseUrl == null || !baseUrl.IsAbsoluteUri)
        {
            return null;
        }

        return new Uri(baseUrl, path.TrimStart('/'));
    }

    private static DateTimeOffset ToDateTimeOffset(DateTime value)
        => value.Kind == DateTimeKind.Unspecified
            ? new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Local))
            : new DateTimeOffset(value);

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static string? ResolveCampaignId(
        OrderTracking? tracking,
        Customer customer,
        bool hasMarketingConsent)
        => hasMarketingConsent
            ? NullIfWhiteSpace(ReadMailchimpTrackingValue(tracking, CampaignIdPropertyName))
                ?? NullIfWhiteSpace(customer.Value("mc_cid"))
            : null;

    internal static string? ResolveTrackingCode(
        OrderTracking? tracking,
        Customer customer,
        bool hasMarketingConsent)
        => hasMarketingConsent
            ? NormalizeTrackingCode(ReadMailchimpTrackingValue(tracking, TrackingCodePropertyName))
                ?? NormalizeTrackingCode(customer.Value("mc_tc"))
            : null;

    private static string? ReadMailchimpTrackingValue(OrderTracking? tracking, string propertyName)
    {
        // Reflection keeps the plugin compatible with Ekom versions from before Mailchimp tracking was added.
        object? mailchimpTracking = tracking?
            .GetType()
            .GetProperty(MailchimpTrackingPropertyName)?
            .GetValue(tracking);
        return mailchimpTracking?
            .GetType()
            .GetProperty(propertyName)?
            .GetValue(mailchimpTracking) as string;
    }

    private static string? NormalizeTrackingCode(string? value)
        => string.Equals(NullIfWhiteSpace(value), "prec", StringComparison.Ordinal)
            ? "prec"
            : null;
}
