using Ekom.Mailchimp.Helpers;
using Ekom.Mailchimp.Http;
using Ekom.Mailchimp.Models;
using Microsoft.Extensions.Caching.Memory;
using System.Net;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Ekom.Mailchimp.Clients;

internal interface IMailchimpAudienceClient
{
    Task<IReadOnlyList<MailchimpTag>> GetTagsAsync(CancellationToken ct);
    Task SubscribeAsync(MailchimpSubscribeRequest request, CancellationToken ct);
    Task UnsubscribeAsync(MailchimpUnsubscribeRequest request, CancellationToken ct);
}

internal sealed class MailchimpAudienceClient : IMailchimpAudienceClient
{
    private static readonly TimeSpan TagsCacheDuration = TimeSpan.FromMinutes(5);

    private readonly IMailchimpConfigurationResolver _configurationResolver;
    private readonly MailchimpHttpClient _httpClient;
    private readonly IMemoryCache _memoryCache;
    private readonly SemaphoreSlim _tagsLock = new(1, 1);

    public MailchimpAudienceClient(
        IMailchimpConfigurationResolver configurationResolver,
        MailchimpHttpClient httpClient,
        IMemoryCache memoryCache)
    {
        _configurationResolver = configurationResolver;
        _httpClient = httpClient;
        _memoryCache = memoryCache;
    }

    public async Task<IReadOnlyList<MailchimpTag>> GetTagsAsync(CancellationToken ct)
    {
        MailchimpStoreConfiguration configuration = _configurationResolver.ResolveGlobal();
        string cacheKey = $"Ekom.Mailchimp.Tags:{configuration.ServerPrefix}:{configuration.AudienceId}";
        if (_memoryCache.TryGetValue(cacheKey, out IReadOnlyList<MailchimpTag>? cachedTags)
            && cachedTags != null)
        {
            return cachedTags;
        }

        await _tagsLock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            if (_memoryCache.TryGetValue(cacheKey, out cachedTags) && cachedTags != null)
            {
                return cachedTags;
            }

            JsonNode? response = await _httpClient.SendAsync(
                configuration,
                HttpMethod.Get,
                $"lists/{Uri.EscapeDataString(configuration.AudienceId)}/tag-search",
                null,
                ct).ConfigureAwait(false);
            IReadOnlyList<MailchimpTag> tags = ParseTags(response);
            _memoryCache.Set(cacheKey, tags, TagsCacheDuration);
            return tags;
        }
        finally
        {
            _tagsLock.Release();
        }
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

    private static IReadOnlyList<MailchimpTag> ParseTags(JsonNode? response)
    {
        if (response?["tags"] is not JsonArray tagNodes)
        {
            throw new JsonException("Mailchimp tag response does not contain a tags array.");
        }

        var tags = new List<MailchimpTag>(tagNodes.Count);
        foreach (JsonNode? tagNode in tagNodes)
        {
            if (tagNode?["id"] is not JsonNode idNode
                || tagNode["name"] is not JsonNode nameNode)
            {
                throw new JsonException("Mailchimp tag response contains an invalid tag.");
            }

            string name = nameNode.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new JsonException("Mailchimp tag response contains a tag without a name.");
            }

            tags.Add(new MailchimpTag
            {
                Id = idNode.GetValue<long>(),
                Name = name,
            });
        }

        return tags.AsReadOnly();
    }
}
