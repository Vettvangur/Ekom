using Microsoft.Extensions.Options;

namespace Ekom.Mailchimp;

internal sealed class MailchimpOptionsValidator : IValidateOptions<MailchimpOptions>
{
    public ValidateOptionsResult Validate(string? name, MailchimpOptions options)
    {
        var failures = new List<string>();
        bool hasEnabledFeature = options.Subscriptions.Enabled
            || options.Purchases.Enabled
            || options.Transactional.Enabled
            || options.Stores.Any(x => x.Transactional.Enabled == true);
        if (options.Enabled && hasEnabledFeature)
        {
            var aliases = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
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
                }
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

        if (options.Transactional.Dispatching.MaxQueueSize <= 0)
        {
            failures.Add("Ekom:Mailchimp:Transactional:Dispatching:MaxQueueSize must be greater than zero.");
        }

        if (options.Transactional.Dispatching.MaxConcurrency <= 0)
        {
            failures.Add("Ekom:Mailchimp:Transactional:Dispatching:MaxConcurrency must be greater than zero.");
        }

        if (options.Transactional.Dispatching.MaxAttempts <= 0)
        {
            failures.Add("Ekom:Mailchimp:Transactional:Dispatching:MaxAttempts must be greater than zero.");
        }

        if (options.Transactional.Dispatching.InitialRetryDelaySeconds < 0)
        {
            failures.Add("Ekom:Mailchimp:Transactional:Dispatching:InitialRetryDelaySeconds cannot be negative.");
        }

        if (options.Transactional.Dispatching.MaxMessageBytes <= 0
            || options.Transactional.Dispatching.MaxMessageBytes >= 10_000_000)
        {
            failures.Add(
                "Ekom:Mailchimp:Transactional:Dispatching:MaxMessageBytes must be greater than zero and less than Mandrill's 10000000-byte API limit.");
        }

        if (options.Transactional.Dispatching.MaxQueueBytes <= 0)
        {
            failures.Add("Ekom:Mailchimp:Transactional:Dispatching:MaxQueueBytes must be greater than zero.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
