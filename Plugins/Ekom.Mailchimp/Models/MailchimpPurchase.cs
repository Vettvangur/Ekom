namespace Ekom.Mailchimp.Models;

public sealed record MailchimpPurchase
{
    public required string StoreAlias { get; init; }
    public required string StoreName { get; init; }
    public required string OrderId { get; init; }
    public string? OrderNumber { get; init; }
    public required string CurrencyCode { get; init; }
    public required decimal OrderTotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal ShippingTotal { get; init; }
    public decimal DiscountTotal { get; init; }
    public required DateTimeOffset ProcessedAt { get; init; }
    public string FinancialStatus { get; init; } = "paid";
    public string? FulfillmentStatus { get; init; }
    public Uri? OrderUrl { get; init; }
    public string? CampaignId { get; init; }
    public string? TrackingCode { get; init; }
    public required MailchimpPurchaseCustomer Customer { get; init; }
    public MailchimpAddress? BillingAddress { get; init; }
    public MailchimpAddress? ShippingAddress { get; init; }
    public IReadOnlyList<MailchimpPurchaseLine> Lines { get; init; } = [];
}

public sealed record MailchimpPurchaseCustomer
{
    public required string Id { get; init; }
    public required string Email { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public bool MarketingOptIn { get; init; }
    public MailchimpAddress? Address { get; init; }
}

public sealed record MailchimpPurchaseLine
{
    public required string Id { get; init; }
    public required string ProductId { get; init; }
    public required string ProductVariantId { get; init; }
    public required string ProductTitle { get; init; }
    public required string VariantTitle { get; init; }
    public string? Sku { get; init; }
    public int Quantity { get; init; }
    public decimal Price { get; init; }
    public decimal? CatalogPrice { get; init; }
    public Uri? ProductUrl { get; init; }
    public Uri? ImageUrl { get; init; }
}

public sealed record MailchimpAddress
{
    public string? Address1 { get; init; }
    public string? Address2 { get; init; }
    public string? City { get; init; }
    public string? Province { get; init; }
    public string? PostalCode { get; init; }
    public string? Country { get; init; }
}
