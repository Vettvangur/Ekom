using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp;

internal sealed class MailchimpOptionsValidator : IValidateOptions<MailchimpOptions>
{
    public ValidateOptionsResult Validate(string? name, MailchimpOptions options)
    {
        var failures = new List<string>();
        bool hasEnabledFeature = options.Subscriptions.Enabled || options.Purchases.Enabled;
        if (options.Enabled && hasEnabledFeature)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            bool validateGlobal = options.Stores.Count == 0
                || !string.IsNullOrWhiteSpace(options.ApiKey)
                || !string.IsNullOrWhiteSpace(options.AudienceId)
                || !string.IsNullOrWhiteSpace(options.EcommerceStoreId);
            if (validateGlobal)
            {
                ValidateConfiguration(
                    options.ApiKey,
                    options.ServerPrefix,
                    options.AudienceId,
                    options.EcommerceStoreId,
                    options.Purchases.Enabled,
                    "Ekom:Mailchimp",
                    failures);
            }

            foreach (MailchimpStoreOptions store in options.Stores)
            {
                if (string.IsNullOrWhiteSpace(store.Alias))
                {
                    failures.Add("Every Ekom:Mailchimp:Stores entry requires an Alias.");
                    continue;
                }

                if (!aliases.Add(store.Alias))
                {
                    failures.Add($"Mailchimp store alias '{store.Alias}' is configured more than once.");
                    continue;
                }

                ValidateConfiguration(
                    store.ApiKey ?? options.ApiKey,
                    store.ServerPrefix ?? options.ServerPrefix,
                    store.AudienceId ?? options.AudienceId,
                    store.EcommerceStoreId ?? options.EcommerceStoreId,
                    options.Purchases.Enabled,
                    $"Ekom:Mailchimp:Stores:{store.Alias}",
                    failures);
            }
        }

        if (options.Dispatching.MaxQueueSize <= 0)
        {
            failures.Add("Ekom:Mailchimp:Dispatching:MaxQueueSize must be greater than zero.");
        }

        if (options.Dispatching.MaxConcurrency <= 0)
        {
            failures.Add("Ekom:Mailchimp:Dispatching:MaxConcurrency must be greater than zero.");
        }

        if (options.Dispatching.MaxAttempts <= 0)
        {
            failures.Add("Ekom:Mailchimp:Dispatching:MaxAttempts must be greater than zero.");
        }

        if (options.Dispatching.InitialRetryDelaySeconds < 0)
        {
            failures.Add("Ekom:Mailchimp:Dispatching:InitialRetryDelaySeconds cannot be negative.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateConfiguration(
        string? apiKey,
        string? serverPrefix,
        string? audienceId,
        string? ecommerceStoreId,
        bool requireEcommerceStore,
        string path,
        ICollection<string> failures)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            failures.Add($"{path}:ApiKey is required.");
        }

        if (string.IsNullOrWhiteSpace(serverPrefix) && !TryGetServerPrefix(apiKey, out _))
        {
            failures.Add($"{path}:ServerPrefix is required when it cannot be derived from ApiKey.");
        }

        if (string.IsNullOrWhiteSpace(audienceId))
        {
            failures.Add($"{path}:AudienceId is required.");
        }

        if (requireEcommerceStore && string.IsNullOrWhiteSpace(ecommerceStoreId))
        {
            failures.Add($"{path}:EcommerceStoreId is required.");
        }
    }

    internal static bool TryGetServerPrefix(string? apiKey, out string serverPrefix)
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
}
