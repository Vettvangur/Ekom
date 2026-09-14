using Ekom.Mailchimp.Helpers;
using Ekom.Mailchimp.Http;
using Ekom.Mailchimp.Models;
using System.Net;

namespace Ekom.Mailchimp.Clients;

internal interface IMailchimpAudienceClient
{
    Task SubscribeAsync(MailchimpSubscribeRequest request, CancellationToken ct);
    Task UnsubscribeAsync(MailchimpUnsubscribeRequest request, CancellationToken ct);
}

internal sealed class MailchimpAudienceClient : IMailchimpAudienceClient
{
    private readonly IMailchimpConfigurationResolver _configurationResolver;
    private readonly MailchimpHttpClient _httpClient;

    public MailchimpAudienceClient(
        IMailchimpConfigurationResolver configurationResolver,
        MailchimpHttpClient httpClient)
    {
        _configurationResolver = configurationResolver;
        _httpClient = httpClient;
    }

    public async Task SubscribeAsync(MailchimpSubscribeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);

        MailchimpStoreConfiguration configuration = _configurationResolver.Resolve(request.StoreAlias);
        string subscriberHash = MailchimpSubscriberHash.Create(request.Email);
        var mergeFields = new Dictionary<string, object?>(request.MergeFields, StringComparer.OrdinalIgnoreCase);
        AddIfNotEmpty(mergeFields, "FNAME", request.FirstName);
        AddIfNotEmpty(mergeFields, "LNAME", request.LastName);

        await _httpClient.SendAsync(
            configuration,
            HttpMethod.Put,
            $"lists/{Uri.EscapeDataString(configuration.AudienceId)}/members/{subscriberHash}",
            new
            {
                EmailAddress = request.Email.Trim(),
                StatusIfNew = ToApiStatus(request.Status),
                Status = ToApiStatus(request.Status),
                MergeFields = mergeFields,
                Language = NullIfWhiteSpace(request.Language),
            },
            ct).ConfigureAwait(false);

        string[] tags = request.Tags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (tags.Length == 0)
        {
            return;
        }

        await _httpClient.SendAsync(
            configuration,
            HttpMethod.Post,
            $"lists/{Uri.EscapeDataString(configuration.AudienceId)}/members/{subscriberHash}/tags",
            new
            {
                Tags = tags.Select(x => new { Name = x, Status = "active" }).ToArray(),
            },
            ct).ConfigureAwait(false);
    }

    public async Task UnsubscribeAsync(MailchimpUnsubscribeRequest request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Email);

        MailchimpStoreConfiguration configuration = _configurationResolver.Resolve(request.StoreAlias);
        string subscriberHash = MailchimpSubscriberHash.Create(request.Email);
        await _httpClient.SendAsync(
            configuration,
            HttpMethod.Patch,
            $"lists/{Uri.EscapeDataString(configuration.AudienceId)}/members/{subscriberHash}",
            new { Status = "unsubscribed" },
            ct).ConfigureAwait(false);
    }

    private static string ToApiStatus(MailchimpSubscriptionStatus status) => status switch
    {
        MailchimpSubscriptionStatus.Pending => "pending",
        MailchimpSubscriptionStatus.Subscribed => "subscribed",
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unsupported subscription status."),
    };

    private static void AddIfNotEmpty(IDictionary<string, object?> values, string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            values[key] = value.Trim();
        }
    }

    private static string? NullIfWhiteSpace(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
