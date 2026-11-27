using Microsoft.Extensions.Options;
using System.Net.Mail;

namespace Ekom.Brevo;

internal sealed class BrevoConfiguration(IOptions<BrevoOptions> options)
{
    public BrevoOptions Options => options.Value;

    public BrevoStoreOptions? Store(string alias)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(alias);
        return Options.Stores.FirstOrDefault(x => string.Equals(x.Alias, alias, StringComparison.OrdinalIgnoreCase));
    }

    public string? ApiKey(string alias, bool transactional = false)
    {
        var store = Store(alias);
        string? key = transactional
            ? store?.Transactional.ApiKey ?? store?.ApiKey ?? Options.Transactional.ApiKey ?? Options.ApiKey
            : store?.ApiKey ?? Options.ApiKey;
        if (key?.Any(char.IsControl) == true)
            throw new InvalidOperationException("Brevo API keys cannot contain control characters.");
        return key;
    }

    public bool Enabled(string alias, bool transactional = false)
        => Options.Enabled && (!transactional || (Store(alias)?.Transactional.Enabled ?? Options.Transactional.Enabled));

    public static void ValidateEmail(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || !MailAddress.TryCreate(value, out var parsed)
            || !string.Equals(parsed.Address, value, StringComparison.OrdinalIgnoreCase)
            || value.Contains('\r', StringComparison.Ordinal) || value.Contains('\n', StringComparison.Ordinal))
            throw new ArgumentException("A plain valid email address is required.", nameof(value));
    }
}

internal sealed class BrevoOptionsValidator : IValidateOptions<BrevoOptions>
{
    private static bool ValidToken(string? token) => token is { Length: >= 32 and <= 256 } && !token.Any(char.IsWhiteSpace) && !token.Any(char.IsControl);
    public ValidateOptionsResult Validate(string? name, BrevoOptions options)
    {
        if (!options.ApiBaseUrl.IsAbsoluteUri || options.ApiBaseUrl.Scheme != Uri.UriSchemeHttps
            || !options.ApiBaseUrl.AbsolutePath.EndsWith("/", StringComparison.Ordinal) || !string.IsNullOrEmpty(options.ApiBaseUrl.Query)
            || !string.IsNullOrEmpty(options.ApiBaseUrl.UserInfo))
            return ValidateOptionsResult.Fail("Brevo ApiBaseUrl must be an absolute HTTPS URL ending in / without a query or credentials.");
        if (options.Stores.Any(x => string.IsNullOrWhiteSpace(x.Alias))
            || options.Stores.Select(x => x.Alias).Distinct(StringComparer.OrdinalIgnoreCase).Count() != options.Stores.Count)
            return ValidateOptionsResult.Fail("Brevo store aliases must be nonempty and unique.");
        foreach (var dispatcher in new[] { options.Dispatching, options.Transactional.Dispatching })
        {
            if (dispatcher.MaxQueueSize <= 0 || dispatcher.MaxAttempts is < 1 or > 10
                || dispatcher.InitialRetryDelaySeconds <= 0 || dispatcher.MaxRetryDelaySeconds < dispatcher.InitialRetryDelaySeconds
                || dispatcher.MaxMessageBytes <= 0 || dispatcher.MaxQueueBytes < dispatcher.MaxMessageBytes)
                return ValidateOptionsResult.Fail("Brevo queue limits and retry settings are invalid.");
        }
        var persistence = options.Transactional.Persistence;
        if (persistence.PollIntervalSeconds is < 1 or > 60 || persistence.LeaseSeconds is < 60 or > 3600
            || persistence.RetentionDays is < 1 or > 365)
            return ValidateOptionsResult.Fail("Brevo persistence settings are invalid.");
        if (options.Transactional.Webhooks.Enabled
            && !ValidToken(options.Transactional.Webhooks.Token))
            return ValidateOptionsResult.Fail("Enabled Brevo webhooks require a global bearer token of at least 32 characters.");
        if (options.Stores.Any(x => x.Transactional.WebhookToken != null && !ValidToken(x.Transactional.WebhookToken)))
            return ValidateOptionsResult.Fail("Brevo store webhook tokens must have at least 32 characters and no whitespace.");
        return ValidateOptionsResult.Success;
    }
}
