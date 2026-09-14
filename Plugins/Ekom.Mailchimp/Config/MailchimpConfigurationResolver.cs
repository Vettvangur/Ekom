using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Ekom.Mailchimp;

internal interface IMailchimpConfigurationResolver
{
    MailchimpStoreConfiguration Resolve(string storeAlias, bool requireEcommerceStore = false);
    bool TryResolve(
        string storeAlias,
        bool requireEcommerceStore,
        [NotNullWhen(true)] out MailchimpStoreConfiguration? configuration);
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
    private readonly ConcurrentDictionary<string, byte> _loggedFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<MailchimpConfigurationResolver> _logger;
    private readonly MailchimpOptions _options;

    public MailchimpConfigurationResolver(
        IOptions<MailchimpOptions> options,
        ILogger<MailchimpConfigurationResolver> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public MailchimpStoreConfiguration Resolve(string storeAlias, bool requireEcommerceStore = false)
    {
        if (TryResolve(storeAlias, requireEcommerceStore, out MailchimpStoreConfiguration? configuration))
        {
            return configuration;
        }

        throw new InvalidOperationException($"Mailchimp configuration is incomplete for store '{storeAlias}'.");
    }

    public bool TryResolve(
        string storeAlias,
        bool requireEcommerceStore,
        [NotNullWhen(true)] out MailchimpStoreConfiguration? configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeAlias);

        MailchimpStoreOptions? store = _options.Stores.FirstOrDefault(x =>
            string.Equals(x.Alias, storeAlias, StringComparison.OrdinalIgnoreCase));

        bool isValid = true;
        string apiKey = store?.ApiKey ?? _options.ApiKey ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LogMissingSetting(storeAlias, nameof(MailchimpOptions.ApiKey));
            isValid = false;
        }

        string serverPrefix = store?.ServerPrefix ?? _options.ServerPrefix ?? string.Empty;
        if (string.IsNullOrWhiteSpace(serverPrefix)
            && !TryGetServerPrefix(apiKey, out serverPrefix))
        {
            LogMissingSetting(storeAlias, nameof(MailchimpOptions.ServerPrefix));
            isValid = false;
        }

        string audienceId = store?.AudienceId ?? _options.AudienceId ?? string.Empty;
        if (string.IsNullOrWhiteSpace(audienceId))
        {
            LogMissingSetting(storeAlias, nameof(MailchimpOptions.AudienceId));
            isValid = false;
        }

        string ecommerceStoreId = store?.EcommerceStoreId ?? _options.EcommerceStoreId ?? string.Empty;
        if (requireEcommerceStore && string.IsNullOrWhiteSpace(ecommerceStoreId))
        {
            LogMissingSetting(storeAlias, nameof(MailchimpOptions.EcommerceStoreId));
            isValid = false;
        }

        if (!isValid)
        {
            configuration = null;
            return false;
        }

        configuration = new MailchimpStoreConfiguration(
            storeAlias,
            apiKey,
            serverPrefix,
            audienceId,
            ecommerceStoreId,
            store?.SiteBaseUrl ?? _options.SiteBaseUrl);
        return true;
    }

    private static bool TryGetServerPrefix(string? apiKey, out string serverPrefix)
    {
        serverPrefix = string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        int separator = apiKey.LastIndexOf('-');
        if (separator < 0 || separator == apiKey.Length - 1)
        {
            return false;
        }

        serverPrefix = apiKey[(separator + 1)..].Trim();
        return !string.IsNullOrWhiteSpace(serverPrefix);
    }

    private void LogMissingSetting(string storeAlias, string setting)
    {
        if (!_loggedFailures.TryAdd($"{storeAlias}\0{setting}", 0))
        {
            return;
        }

        _logger.LogError(
            "Mailchimp configuration for store {StoreAlias} is missing {Setting}; affected work will be ignored",
            storeAlias,
            setting);
    }
}
