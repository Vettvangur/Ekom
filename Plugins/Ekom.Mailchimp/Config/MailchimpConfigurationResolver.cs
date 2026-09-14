using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp;

internal interface IMailchimpConfigurationResolver
{
    MailchimpStoreConfiguration Resolve(string storeAlias, bool requireEcommerceStore = false);
}

internal sealed record MailchimpStoreConfiguration(
    string StoreAlias,
    string ApiKey,
    string ServerPrefix,
    string AudienceId,
    string EcommerceStoreId,
    Uri? SiteBaseUrl);

internal sealed class MailchimpConfigurationResolver : IMailchimpConfigurationResolver
{
    private readonly MailchimpOptions _options;

    public MailchimpConfigurationResolver(IOptions<MailchimpOptions> options)
    {
        _options = options.Value;
    }

    public MailchimpStoreConfiguration Resolve(string storeAlias, bool requireEcommerceStore = false)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            throw new ArgumentException("A store alias is required.", nameof(storeAlias));
        }

        MailchimpStoreOptions? store = _options.Stores.FirstOrDefault(x =>
            string.Equals(x.Alias, storeAlias, StringComparison.OrdinalIgnoreCase));

        string apiKey = store?.ApiKey ?? _options.ApiKey
            ?? throw new InvalidOperationException($"No Mailchimp API key is configured for store '{storeAlias}'.");
        string? serverPrefix = store?.ServerPrefix ?? _options.ServerPrefix;
        if (string.IsNullOrWhiteSpace(serverPrefix)
            && !MailchimpOptionsValidator.TryGetServerPrefix(apiKey, out serverPrefix))
        {
            throw new InvalidOperationException($"No Mailchimp server prefix is configured for store '{storeAlias}'.");
        }

        string? ecommerceStoreId = store?.EcommerceStoreId ?? _options.EcommerceStoreId;
        if (requireEcommerceStore && string.IsNullOrWhiteSpace(ecommerceStoreId))
        {
            throw new InvalidOperationException($"No Mailchimp e-commerce store ID is configured for store '{storeAlias}'.");
        }

        return new MailchimpStoreConfiguration(
            storeAlias,
            apiKey,
            serverPrefix,
            store?.AudienceId ?? _options.AudienceId
                ?? throw new InvalidOperationException($"No Mailchimp audience ID is configured for store '{storeAlias}'."),
            ecommerceStoreId ?? string.Empty,
            store?.SiteBaseUrl ?? _options.SiteBaseUrl);
    }
}
