using Ekom.Mailchimp.Exceptions;
using Ekom.Mailchimp.Http;
using Ekom.Mailchimp.Models;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace Ekom.Mailchimp.Clients;

internal interface IMailchimpEcommerceClient
{
    Task TrackPurchaseAsync(MailchimpPurchase purchase, CancellationToken ct);
}

internal sealed class MailchimpEcommerceClient : IMailchimpEcommerceClient
{
    private readonly ConcurrentDictionary<string, byte> _initializedStores = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _storeLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly IMailchimpConfigurationResolver _configurationResolver;
    private readonly MailchimpHttpClient _httpClient;

    public MailchimpEcommerceClient(
        IMailchimpConfigurationResolver configurationResolver,
        MailchimpHttpClient httpClient)
    {
        _configurationResolver = configurationResolver;
        _httpClient = httpClient;
    }

    public async Task TrackPurchaseAsync(MailchimpPurchase purchase, CancellationToken ct)
    {
        MailchimpPurchaseValidator.Validate(purchase);
        MailchimpStoreConfiguration configuration = _configurationResolver.Resolve(purchase.StoreAlias, requireEcommerceStore: true);

        await EnsureStoreAsync(configuration, purchase, ct).ConfigureAwait(false);
        await UpsertCustomerAsync(configuration, purchase, ct).ConfigureAwait(false);
        await UpsertProductsAsync(configuration, purchase, ct).ConfigureAwait(false);
        await UpsertOrderAsync(configuration, purchase, ct).ConfigureAwait(false);
    }

    private async Task EnsureStoreAsync(
        MailchimpStoreConfiguration configuration,
        MailchimpPurchase purchase,
        CancellationToken ct)
    {
        string accountHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(configuration.ApiKey)));
        string cacheKey = $"{configuration.ServerPrefix}:{accountHash}:{configuration.EcommerceStoreId}";
        if (_initializedStores.ContainsKey(cacheKey))
        {
            return;
        }

        SemaphoreSlim storeLock = _storeLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));
        await storeLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_initializedStores.ContainsKey(cacheKey))
            {
                return;
            }

            string storePath = $"ecommerce/stores/{Uri.EscapeDataString(configuration.EcommerceStoreId)}";
            try
            {
                await _httpClient.SendAsync(configuration, HttpMethod.Get, storePath, null, ct).ConfigureAwait(false);
            }
            catch (MailchimpApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
            {
                try
                {
                    await _httpClient.SendAsync(
                        configuration,
                        HttpMethod.Post,
                        "ecommerce/stores",
                        new
                        {
                            Id = configuration.EcommerceStoreId,
                            ListId = configuration.AudienceId,
                            Name = purchase.StoreName,
                            CurrencyCode = purchase.CurrencyCode,
                        },
                        ct).ConfigureAwait(false);
                }
                catch (MailchimpApiException createException) when (
                    createException.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.Conflict)
                {
                    await _httpClient.SendAsync(configuration, HttpMethod.Get, storePath, null, ct).ConfigureAwait(false);
                }
            }

            _initializedStores.TryAdd(cacheKey, 0);
        }
        finally
        {
            storeLock.Release();
        }
    }

    private Task UpsertCustomerAsync(
        MailchimpStoreConfiguration configuration,
        MailchimpPurchase purchase,
        CancellationToken ct)
    {
        MailchimpPurchaseCustomer customer = purchase.Customer;
        return SendPutAsync(
            configuration,
            $"ecommerce/stores/{Escape(configuration.EcommerceStoreId)}/customers/{Escape(customer.Id)}",
            new
            {
                Id = customer.Id,
                EmailAddress = customer.Email,
                OptInStatus = customer.MarketingOptIn,
                FirstName = customer.FirstName,
                LastName = customer.LastName,
                Address = ToAddressPayload(customer.Address),
            },
            ct);
    }

    private async Task UpsertProductsAsync(
        MailchimpStoreConfiguration configuration,
        MailchimpPurchase purchase,
        CancellationToken ct)
    {
        foreach (IGrouping<string, MailchimpPurchaseLine> productLines in purchase.Lines.GroupBy(x => x.ProductId))
        {
            MailchimpPurchaseLine first = productLines.First();
            object[] variants = productLines
                .GroupBy(x => x.ProductVariantId)
                .Select(x => x.First())
                .Select(x => (object)new
                {
                    Id = x.ProductVariantId,
                    Title = x.VariantTitle,
                    Sku = x.Sku,
                    Price = x.CatalogPrice ?? x.Price,
                    Url = x.ProductUrl,
                    ImageUrl = x.ImageUrl,
                })
                .ToArray();

            await SendPutAsync(
                configuration,
                $"ecommerce/stores/{Escape(configuration.EcommerceStoreId)}/products/{Escape(first.ProductId)}",
                new
                {
                    Id = first.ProductId,
                    Title = first.ProductTitle,
                    Url = first.ProductUrl,
                    ImageUrl = first.ImageUrl,
                    Variants = variants,
                },
                ct).ConfigureAwait(false);
        }
    }

    private Task UpsertOrderAsync(
        MailchimpStoreConfiguration configuration,
        MailchimpPurchase purchase,
        CancellationToken ct)
    {
        return SendPutAsync(
            configuration,
            $"ecommerce/stores/{Escape(configuration.EcommerceStoreId)}/orders/{Escape(purchase.OrderId)}",
            new
            {
                Id = purchase.OrderId,
                Customer = new { Id = purchase.Customer.Id },
                CurrencyCode = purchase.CurrencyCode,
                OrderTotal = purchase.OrderTotal,
                TaxTotal = purchase.TaxTotal,
                ShippingTotal = purchase.ShippingTotal,
                DiscountTotal = purchase.DiscountTotal,
                ProcessedAtForeign = purchase.ProcessedAt.UtcDateTime,
                FinancialStatus = purchase.FinancialStatus,
                FulfillmentStatus = purchase.FulfillmentStatus,
                OrderUrl = purchase.OrderUrl,
                CampaignId = purchase.CampaignId,
                TrackingCode = purchase.TrackingCode,
                BillingAddress = ToAddressPayload(purchase.BillingAddress),
                ShippingAddress = ToAddressPayload(purchase.ShippingAddress),
                Lines = purchase.Lines.Select(x => new
                {
                    x.Id,
                    x.ProductId,
                    x.ProductVariantId,
                    x.Quantity,
                    x.Price,
                }).ToArray(),
            },
            ct);
    }

    private async Task SendPutAsync(
        MailchimpStoreConfiguration configuration,
        string path,
        object payload,
        CancellationToken ct)
    {
        await _httpClient.SendAsync(configuration, HttpMethod.Put, path, payload, ct).ConfigureAwait(false);
    }

    private static object? ToAddressPayload(MailchimpAddress? address) => address == null
        ? null
        : new
        {
            address.Address1,
            address.Address2,
            address.City,
            Province = address.Province,
            PostalCode = address.PostalCode,
            Country = address.Country,
        };

    private static string Escape(string value) => Uri.EscapeDataString(value);

}
