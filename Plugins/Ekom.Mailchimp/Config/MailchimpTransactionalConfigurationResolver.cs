using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;

namespace Ekom.Mailchimp;

internal interface IMailchimpTransactionalConfigurationResolver
{
    bool IsEnabled(string storeAlias);
    bool TryResolve(
        string storeAlias,
        [NotNullWhen(true)] out MailchimpTransactionalStoreConfiguration? configuration);
}

internal sealed record MailchimpTransactionalStoreConfiguration(
    string StoreAlias,
    string ApiKey,
    string? DefaultFromEmail,
    string? DefaultFromName,
    string? DefaultReplyTo);

internal sealed class MailchimpTransactionalConfigurationResolver : IMailchimpTransactionalConfigurationResolver
{
    private readonly ConcurrentDictionary<string, byte> _loggedFailures = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<MailchimpTransactionalConfigurationResolver> _logger;
    private readonly MailchimpOptions _options;

    public MailchimpTransactionalConfigurationResolver(
        IOptions<MailchimpOptions> options,
        ILogger<MailchimpTransactionalConfigurationResolver> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled(string storeAlias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeAlias);
        MailchimpStoreOptions? store = FindStore(storeAlias);
        return _options.Enabled && (store?.Transactional.Enabled ?? _options.Transactional.Enabled);
    }

    public bool TryResolve(
        string storeAlias,
        [NotNullWhen(true)] out MailchimpTransactionalStoreConfiguration? configuration)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(storeAlias);
        MailchimpStoreOptions? store = FindStore(storeAlias);
        if (!_options.Enabled || !(store?.Transactional.Enabled ?? _options.Transactional.Enabled))
        {
            configuration = null;
            return false;
        }

        string apiKey = GetValue(store?.Transactional.ApiKey, _options.Transactional.ApiKey) ?? string.Empty;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            LogMissingApiKey(storeAlias);
            configuration = null;
            return false;
        }

        configuration = new MailchimpTransactionalStoreConfiguration(
            storeAlias,
            apiKey,
            GetValue(store?.Transactional.DefaultFromEmail, _options.Transactional.DefaultFromEmail),
            GetValue(store?.Transactional.DefaultFromName, _options.Transactional.DefaultFromName),
            GetValue(store?.Transactional.DefaultReplyTo, _options.Transactional.DefaultReplyTo));
        return true;
    }

    private MailchimpStoreOptions? FindStore(string storeAlias)
        => _options.Stores.FirstOrDefault(x =>
            string.Equals(x.Alias, storeAlias, StringComparison.OrdinalIgnoreCase));

    private static string? GetValue(string? storeValue, string? globalValue)
        => string.IsNullOrWhiteSpace(storeValue) ? globalValue : storeValue;

    private void LogMissingApiKey(string storeAlias)
    {
        if (!_loggedFailures.TryAdd(storeAlias, 0))
        {
            return;
        }

        _logger.LogError(
            "Mailchimp Transactional configuration for store {StoreAlias} is missing ApiKey; affected messages will be ignored",
            storeAlias);
    }
}
