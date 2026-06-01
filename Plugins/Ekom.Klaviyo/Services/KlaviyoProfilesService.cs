using Ekom.Klaviyo.Clients;
using Ekom.Klaviyo.Dispatching.Profiles;
using Ekom.Klaviyo.Enrichers.ProfilesEnricher;
using Ekom.Klaviyo.Exceptions;
using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Profiles;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Ekom.Klaviyo.Services;

public interface IKlaviyoProfilesService
{
    ValueTask UpsertProfileAsync(
        KlaviyoProfileUpdate payload, 
        CancellationToken ct = default);

    ValueTask SubscribeAsync(
        KlaviyoProfileSubscribeRequest payload, 
        CancellationToken ct = default);

    ValueTask UnsubscribeAsync(
        KlaviyoProfileUnsubscribeRequest payload, 
        CancellationToken ct = default);

    ValueTask<KlaviyoProfileLookupResult?> GetProfileByIdAsync(
        string profileId,
        string? storeAlias,
        bool includeSubscriptions = false,
        CancellationToken ct = default);

    ValueTask<KlaviyoProfileLookupResult?> GetProfileByEmailAsync(
        string email,
        string? storeAlias,
        bool includeSubscriptions = false,
        CancellationToken ct = default);

    ValueTask<IReadOnlyList<string>?> GetProfileListIdsByProfileIdAsync(
        string profileId,
        string? storeAlias,
        CancellationToken ct = default);

    ValueTask<IReadOnlyList<string>?> GetProfileListIdsByEmailAsync(
        string email,
        string? storeAlias,
        CancellationToken ct = default);
}

internal sealed class KlaviyoProfilesService : IKlaviyoProfilesService
{
    private readonly IKlaviyoProfilesClient _client;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoProfilesService> _logger;
    private readonly IKlaviyoProfilesDispatcher _dispatcher;
    private readonly IKlaviyoProfilesEnricherRunner? _enrichers;

    public KlaviyoProfilesService(
        IKlaviyoProfilesClient client,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoProfilesService> logger,
        IKlaviyoProfilesDispatcher dispatcher,
        IKlaviyoProfilesEnricherRunner? enrichers)
    {
        _client = client;
        _opt = options.Value;
        _logger = logger;
        _dispatcher = dispatcher;
        _enrichers = enrichers;
    }

    public async ValueTask UpsertProfileAsync(KlaviyoProfileUpdate payload, CancellationToken ct = default)
    {
        if (!IsEnabled()) return;

        if (!payload.Profile.Customer.HasIdentifier)
        {
            _logger.LogWarning(
                "Klaviyo: skipping Profile Upsert because no customer identifier was provided. Store={StoreAlias}",
                payload.StoreAlias);
            return;
        }

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(payload, ct);

        var work = new KlaviyoProfilesWork(
            Type: KlaviyoProfilesEventType.ProfileUpsert,
            Payload: payload.ToProfileImportRequest(),
            OccurredAt: DateTimeOffset.UtcNow,
            StoreAlias: payload.StoreAlias,
            CustomerIdentifier: payload.Profile.Customer.IdentifierForLogs());

        await _dispatcher.EnqueueAsync(work, ct);

        if (!string.IsNullOrWhiteSpace(payload.ListId))
        {
            string? profileId = payload.Profile.Customer.KlaviyoProfileId;

            if (string.IsNullOrWhiteSpace(profileId) &&
                !string.IsNullOrWhiteSpace(payload.Profile.Customer.Email))
            {
                var lookup = await GetProfileByEmailAsync(
                    payload.Profile.Customer.Email,
                    payload.StoreAlias,
                    includeSubscriptions: false,
                    ct).ConfigureAwait(false);

                profileId = lookup?.ProfileId;
            }

            if (string.IsNullOrWhiteSpace(profileId))
            {
                _logger.LogDebug(
                    "Klaviyo: skipping list add because no profile id was resolved. Store={StoreAlias}",
                    payload.StoreAlias);
                return;
            }

            var addToList = new KlaviyoProfilesWork(
                Type: KlaviyoProfilesEventType.AddToList,
                Payload: ProfileMapper.ToAddToListRequest(profileId),
                OccurredAt: DateTimeOffset.UtcNow,
                StoreAlias: payload.StoreAlias,
                CustomerIdentifier: payload.Profile.Customer.IdentifierForLogs(),
                ListId: payload.ListId);

            await _dispatcher.EnqueueAsync(addToList, ct);
        }

    }

    public async ValueTask SubscribeAsync(KlaviyoProfileSubscribeRequest payload, CancellationToken ct = default)
    {
        await SendSubscribeJobAsync(payload, ct);
    }

    public async ValueTask UnsubscribeAsync(KlaviyoProfileUnsubscribeRequest payload, CancellationToken ct = default)
    {
        await SendUnsubscribeJobAsync(payload, ct);
    }

    public async ValueTask<KlaviyoProfileLookupResult?> GetProfileByIdAsync(
        string profileId,
        string? storeAlias,
        bool includeSubscriptions = false,
        CancellationToken ct = default)
    {
        if (!IsEnabled()) return null;
        if (string.IsNullOrWhiteSpace(profileId)) return null;

        var json = await TryGetByIdAsync(profileId, storeAlias, includeSubscriptions, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return null;

        return ParseProfileResponse(json);
    }

    public async ValueTask<KlaviyoProfileLookupResult?> GetProfileByEmailAsync(
        string email,
        string? storeAlias,
        bool includeSubscriptions = false,
        CancellationToken ct = default)
    {
        if (!IsEnabled()) return null;
        if (string.IsNullOrWhiteSpace(email)) return null;

        var json = await TryGetByEmailAsync(email, storeAlias, includeSubscriptions, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return null;

        return ParseProfileResponse(json);
    }

    public async ValueTask<IReadOnlyList<string>?> GetProfileListIdsByProfileIdAsync(
        string profileId,
        string? storeAlias,
        CancellationToken ct = default)
    {
        if (!IsEnabled()) return null;
        if (string.IsNullOrWhiteSpace(profileId)) return null;

        var json = await _client.GetListIdsAsync(profileId, storeAlias, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(json)) return null;

        return ParseListIds(json);
    }

    public async ValueTask<IReadOnlyList<string>?> GetProfileListIdsByEmailAsync(
        string email,
        string? storeAlias,
        CancellationToken ct = default)
    {
        if (!IsEnabled()) return null;
        if (string.IsNullOrWhiteSpace(email)) return null;

        var profile = await GetProfileByEmailAsync(email, storeAlias, includeSubscriptions: false, ct).ConfigureAwait(false);
        if (profile?.ProfileId is null) return null;

        return await GetProfileListIdsByProfileIdAsync(profile.ProfileId, storeAlias, ct).ConfigureAwait(false);
    }

    private async Task<string?> TryGetByIdAsync(
        string profileId,
        string? storeAlias,
        bool includeSubscriptions,
        CancellationToken ct)
    {
        if (!includeSubscriptions)
            return await _client.GetByIdAsync(profileId, storeAlias, includeSubscriptions: false, ct).ConfigureAwait(false);

        try
        {
            return await _client.GetByIdAsync(
                profileId,
                storeAlias,
                includeSubscriptions: true,
                ct).ConfigureAwait(false);
        }
        catch (KlaviyoApiException ex) when (ex.StatusCode == 400)
        {
            _logger.LogDebug(
                "Klaviyo: retrying profile fetch without subscriptions fields. Store={StoreAlias}",
                storeAlias);

            return await _client.GetByIdAsync(
                profileId,
                storeAlias,
                includeSubscriptions: false,
                ct).ConfigureAwait(false);
        }
    }

    private async Task<string?> TryGetByEmailAsync(
        string email,
        string? storeAlias,
        bool includeSubscriptions,
        CancellationToken ct)
    {
        if (!includeSubscriptions)
            return await _client.GetByEmailAsync(email, storeAlias, includeSubscriptions: false, ct).ConfigureAwait(false);

        try
        {
            return await _client.GetByEmailAsync(
                email,
                storeAlias,
                includeSubscriptions: true,
                ct).ConfigureAwait(false);
        }
        catch (KlaviyoApiException ex) when (ex.StatusCode == 400)
        {
            _logger.LogDebug(
                "Klaviyo: retrying profile fetch without subscriptions fields. Store={StoreAlias}",
                storeAlias);

            return await _client.GetByEmailAsync(
                email,
                storeAlias,
                includeSubscriptions: false,
                ct).ConfigureAwait(false);
        }
    }

    private static KlaviyoProfileLookupResult? ParseProfileResponse(string json)
    {
        using var doc = JsonDocument.Parse(json);

        if (!TryGetProfileData(doc.RootElement, out var profileData))
            return null;

        var attributes = GetAttributes(profileData);
        var channels = new HashSet<KlaviyoProfileConsentChannel>();
        if (!TryParseSubscriptions(attributes, channels))
            TryParseConsentProperties(attributes, channels);

        return new KlaviyoProfileLookupResult
        {
            ProfileId = GetString(profileData, "id"),
            Email = GetString(attributes, "email"),
            PhoneNumber = GetString(attributes, "phone_number"),
            FirstName = GetString(attributes, "first_name"),
            LastName = GetString(attributes, "last_name"),
            ExternalId = GetString(attributes, "external_id"),
            SubscribedChannels = channels.ToArray()
        };
    }

    private bool IsEnabled()
        => _opt.Enabled;

    private async ValueTask SendSubscribeJobAsync(KlaviyoProfileSubscribeRequest payload, CancellationToken ct)
    {
        if (!IsEnabled() || !_opt.Subscriptions.Enabled) return;

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            _logger.LogWarning(
                "Klaviyo: skipping {Type} because no email was provided. Store={StoreAlias}",
                KlaviyoProfilesEventType.Subscribe, payload.StoreAlias);
            return;
        }

        if (payload.Consents is null || payload.Consents.Count == 0)
        {
            _logger.LogDebug(
                "Klaviyo: skipping {Type} because no consent changes were provided. Store={StoreAlias}",
                KlaviyoProfilesEventType.Subscribe, payload.StoreAlias);
            return;
        }

        await TryUpsertProfileForSubscribeAsync(payload, ct);

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(payload, ct);

        var request = (payload with
        {
            ListId = _opt.ResolveSubscriptionListId(payload.StoreAlias, payload.ListId)
        }).ToBulkSubscribeJobRequest();

        var work = new KlaviyoProfilesWork(
            Type: KlaviyoProfilesEventType.Subscribe,
            Payload: request,
            OccurredAt: DateTimeOffset.UtcNow,
            StoreAlias: payload.StoreAlias,
            CustomerIdentifier: KlaviyoCustomerLoggingExtensions.MaskEmailForLogs(payload.Email));

        await _dispatcher.EnqueueAsync(work, ct);
    }

    private async ValueTask TryUpsertProfileForSubscribeAsync(KlaviyoProfileSubscribeRequest payload, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(payload.FirstName) &&
            string.IsNullOrWhiteSpace(payload.LastName) &&
            string.IsNullOrWhiteSpace(payload.FullName) &&
            (payload.CustomProperties is null || payload.CustomProperties.Count == 0))
        {
            return;
        }

        var update = new KlaviyoProfileUpdate(
            StoreAlias: payload.StoreAlias,
            Profile: new KlaviyoProfile
            {
                Customer = new KlaviyoCustomer
                {
                    Email = payload.Email,
                    PhoneNumber = payload.PhoneNumber
                },
                Attributes = new KlaviyoProfileAttributes
                {
                    FullName = payload.FullName,
                    FirstName = payload.FirstName,
                    LastName = payload.LastName,
                    CustomProperties = payload.CustomProperties
                }
            });

        await UpsertProfileAsync(update, ct);
    }

    private async ValueTask SendUnsubscribeJobAsync(KlaviyoProfileUnsubscribeRequest payload, CancellationToken ct)
    {
        if (!IsEnabled() || !_opt.Subscriptions.Enabled) return;

        if (string.IsNullOrWhiteSpace(payload.Email))
        {
            _logger.LogWarning(
                "Klaviyo: skipping {Type} because no email was provided. Store={StoreAlias}",
                KlaviyoProfilesEventType.Unsubscribe, payload.StoreAlias);
            return;
        }

        var request = payload.ToBulkUnsubscribeJobRequest();

        var work = new KlaviyoProfilesWork(
            Type: KlaviyoProfilesEventType.Unsubscribe,
            Payload: request,
            OccurredAt: DateTimeOffset.UtcNow,
            StoreAlias: payload.StoreAlias,
            CustomerIdentifier: KlaviyoCustomerLoggingExtensions.MaskEmailForLogs(payload.Email));

        await _dispatcher.EnqueueAsync(work, ct);
    }

    private static bool TryGetProfileData(JsonElement root, out JsonElement profileData)
    {
        profileData = default;

        if (!root.TryGetProperty("data", out var data))
            return false;

        if (data.ValueKind == JsonValueKind.Object)
        {
            profileData = data;
            return true;
        }

        if (data.ValueKind == JsonValueKind.Array)
        {
            var enumerator = data.EnumerateArray();
            if (!enumerator.MoveNext())
                return false;

            profileData = enumerator.Current;
            return true;
        }

        return false;
    }

    private static JsonElement GetAttributes(JsonElement profileData)
    {
        return profileData.TryGetProperty("attributes", out var attributes)
            ? attributes
            : default;
    }

    private static string? GetString(JsonElement element, string propertyName)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;
        if (!element.TryGetProperty(propertyName, out var value)) return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : null;
    }

    private static bool TryParseSubscriptions(JsonElement attributes, ISet<KlaviyoProfileConsentChannel> channels)
    {
        if (attributes.ValueKind != JsonValueKind.Object)
            return false;

        if (!attributes.TryGetProperty("subscriptions", out var subs))
            return false;

        return TryParseSubscriptionsObject(subs, channels);
    }

    private static bool TryParseConsentProperties(JsonElement attributes, ISet<KlaviyoProfileConsentChannel> channels)
    {
        if (attributes.ValueKind != JsonValueKind.Object)
            return false;

        if (!attributes.TryGetProperty("properties", out var props) || props.ValueKind != JsonValueKind.Object)
            return false;

        if (!props.TryGetProperty("$consent", out var consent))
            return false;

        if (consent.ValueKind == JsonValueKind.String)
            return TryAddConsentChannel(consent.GetString(), channels);

        if (consent.ValueKind != JsonValueKind.Array)
            return false;

        var added = false;
        foreach (var item in consent.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String) continue;
            if (TryAddConsentChannel(item.GetString(), channels))
                added = true;
        }

        return added;
    }

    private static bool TryAddConsentChannel(string? channel, ISet<KlaviyoProfileConsentChannel> channels)
    {
        var parsed = ParseChannel(channel);
        if (parsed is null) return false;

        channels.Add(parsed.Value);
        return true;
    }

    private static bool TryParseSubscriptionsObject(JsonElement subs, ISet<KlaviyoProfileConsentChannel> channels)
    {
        if (subs.ValueKind != JsonValueKind.Object)
            return false;

        var hasAnyConsent = false;

        foreach (var channelProp in subs.EnumerateObject())
        {
            var channel = ParseChannel(channelProp.Name);
            if (channel is null) continue;

            if (!TryGetConsentState(channelProp.Value, out var isSubscribed))
                continue;

            hasAnyConsent = true;
            if (isSubscribed)
                channels.Add(channel.Value);
        }

        return hasAnyConsent;
    }

    private static KlaviyoProfileConsentChannel? ParseChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return null;

        return channel.Trim().ToLowerInvariant() switch
        {
            "email" => KlaviyoProfileConsentChannel.Email,
            "sms" => KlaviyoProfileConsentChannel.Sms,
            "push" => KlaviyoProfileConsentChannel.Push,
            _ => null
        };
    }

    private static bool TryGetConsentState(JsonElement element, out bool isSubscribed)
    {
        isSubscribed = false;

        if (TryGetConsentValue(element, out var consent))
        {
            isSubscribed = consent.Equals("SUBSCRIBED", StringComparison.OrdinalIgnoreCase) ||
                           consent.Equals("subscribed", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        return false;
    }

    private static bool TryGetConsentValue(JsonElement element, out string consent)
    {
        consent = string.Empty;

        if (element.ValueKind != JsonValueKind.Object)
            return false;

        if (element.TryGetProperty("marketing", out var marketing) &&
            marketing.ValueKind == JsonValueKind.Object &&
            marketing.TryGetProperty("consent", out var marketingConsent) &&
            marketingConsent.ValueKind == JsonValueKind.String)
        {
            consent = marketingConsent.GetString() ?? string.Empty;
            return true;
        }

        if (element.TryGetProperty("consent", out var consentEl) &&
            consentEl.ValueKind == JsonValueKind.String)
        {
            consent = consentEl.GetString() ?? string.Empty;
            return true;
        }

        if (element.TryGetProperty("state", out var stateEl) &&
            stateEl.ValueKind == JsonValueKind.String)
        {
            consent = stateEl.GetString() ?? string.Empty;
            return true;
        }

        return false;
    }

    private static IReadOnlyList<string> ParseListIds(string json)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data))
            return Array.Empty<string>();

        var listIds = new List<string>();

        if (data.ValueKind == JsonValueKind.Object)
        {
            var id = GetString(data, "id");
            if (!string.IsNullOrWhiteSpace(id))
                listIds.Add(id);
        }
        else if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                var id = GetString(item, "id");
                if (!string.IsNullOrWhiteSpace(id))
                    listIds.Add(id);
            }
        }

        return listIds;
    }
}
