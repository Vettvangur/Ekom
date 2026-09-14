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

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
