using Ekom.Klaviyo.Clients;
using Ekom.Klaviyo.Dispatching.Subscriptions;
using Ekom.Klaviyo.Enrichers.SubscriptionsEnricher;
using Ekom.Klaviyo.Exceptions;
using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Profiles;
using Ekom.Klaviyo.Models.Subscriptions;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Umbraco.Extensions;

namespace Ekom.Klaviyo.Services;

public interface IKlaviyoProfilesService
{
    ValueTask UpsertProfileAsync(
        KlaviyoProfileUpdate payload, 
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

    ValueTask<IReadOnlyList<string>?> GetProfileListIdsAsync(
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
    private readonly IKlaviyoSubscriptionsDispatcher _dispatcher;
    private readonly IKlaviyoSubscriptionsEnricherRunner? _enrichers;

    public KlaviyoProfilesService(
        IKlaviyoProfilesClient client,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoProfilesService> logger,
        IKlaviyoSubscriptionsDispatcher dispatcher,
        IKlaviyoSubscriptionsEnricherRunner? enrichers)
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

        var work = new KlaviyoSubscriptionsWork(
            Type: KlaviyoSubscriptionsEventType.ProfileUpsert,
            Payload: payload.ToProfileImportRequest(),
            OccurredAt: DateTimeOffset.UtcNow,
            StoreAlias: payload.StoreAlias,
            CustomerIdentifier: payload.Profile.Customer.IdentifierForLogs());

        await _dispatcher.EnqueueAsync(work, ct);

        var listId = ResolveListId(payload.StoreAlias, payload.ListId);

        if (!string.IsNullOrWhiteSpace(listId))
        {
            var listWork = new KlaviyoSubscriptionsWork(
                Type: KlaviyoSubscriptionsEventType.AddToList,
                Payload: payload.Profile.ToAddToListRequest(),
                OccurredAt: DateTimeOffset.UtcNow,
                StoreAlias: payload.StoreAlias,
                CustomerIdentifier: payload.Profile.Customer.IdentifierForLogs(),
                ListId: listId);

            await _dispatcher.EnqueueAsync(listWork, ct);
        }
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

        return await ParseProfileResponseAsync(json, storeAlias, includeSubscriptions, ct).ConfigureAwait(false);
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

        return await ParseProfileResponseAsync(json, storeAlias, includeSubscriptions, ct).ConfigureAwait(false);
    }

    public async ValueTask<IReadOnlyList<string>?> GetProfileListIdsAsync(
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

        return await GetProfileListIdsAsync(profile.ProfileId, storeAlias, ct).ConfigureAwait(false);
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

    private async ValueTask<KlaviyoProfileLookupResult?> ParseProfileResponseAsync(
        string json,
        string? storeAlias,
        bool includeSubscriptions,
        CancellationToken ct)
    {
        using var doc = JsonDocument.Parse(json);

        if (!TryGetProfileData(doc.RootElement, out var profileData))
            return null;

        var attributes = GetAttributes(profileData);
        var channels = new HashSet<KlaviyoConsentChannel>();
        var hasConsentData = TryParseSubscriptions(attributes, channels);

        if (!hasConsentData && TryParseConsentProperties(attributes, channels))
            hasConsentData = true;

        var result = new KlaviyoProfileLookupResult
        {
            ProfileId = GetString(profileData, "id"),
            Email = GetString(attributes, "email"),
            PhoneNumber = GetString(attributes, "phone_number"),
            FirstName = GetString(attributes, "first_name"),
            LastName = GetString(attributes, "last_name"),
            ExternalId = GetString(attributes, "external_id"),
            SubscribedChannels = channels.ToArray()
        };

        if (!includeSubscriptions || hasConsentData || string.IsNullOrWhiteSpace(result.ProfileId))
            return result;

        var fallback = await TryGetSubscriptionsAsync(result.ProfileId, storeAlias, ct).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(fallback))
            return result;

        if (TryParseSubscriptionResponse(fallback, channels))
            result.SubscribedChannels = channels.ToArray();

        return result;
    }

    private async Task<string?> TryGetSubscriptionsAsync(
        string profileId,
        string? storeAlias,
        CancellationToken ct)
    {
        try
        {
            return await _client.GetSubscriptionsAsync(profileId, storeAlias, ct).ConfigureAwait(false);
        }
        catch (KlaviyoApiException ex)
        {
            _logger.LogDebug(
                "Klaviyo: subscriptions fallback failed. Store={StoreAlias} Status={Status}",
                storeAlias,
                ex.StatusCode);

            return null;
        }
    }

    private bool IsEnabled()
        => _opt.Enabled;

    private string? ResolveListId(string storeAlias, string? explicitListId)
    {
        if (!string.IsNullOrWhiteSpace(explicitListId))
            return explicitListId;

        var store = _opt.Stores.FirstOrDefault(s => s.Alias.InvariantEquals(storeAlias));
        if (!string.IsNullOrWhiteSpace(store?.ListId))
            return store.ListId;

        return string.IsNullOrWhiteSpace(_opt.Subscriptions.DefaultListId)
            ? null
            : _opt.Subscriptions.DefaultListId;
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

    private static bool TryParseSubscriptions(JsonElement attributes, ISet<KlaviyoConsentChannel> channels)
    {
        if (attributes.ValueKind != JsonValueKind.Object)
            return false;

        if (!attributes.TryGetProperty("subscriptions", out var subs))
            return false;

        return TryParseSubscriptionsObject(subs, channels);
    }

    private static bool TryParseConsentProperties(JsonElement attributes, ISet<KlaviyoConsentChannel> channels)
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

    private static bool TryAddConsentChannel(string? channel, ISet<KlaviyoConsentChannel> channels)
    {
        var parsed = ParseChannel(channel);
        if (parsed is null) return false;

        channels.Add(parsed.Value);
        return true;
    }

    private static bool TryParseSubscriptionsObject(JsonElement subs, ISet<KlaviyoConsentChannel> channels)
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

    private static bool TryParseSubscriptionResponse(string json, ISet<KlaviyoConsentChannel> channels)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        if (!root.TryGetProperty("data", out var data))
            return false;

        var hasAnyConsent = false;

        if (data.ValueKind == JsonValueKind.Object)
        {
            if (TryParseSubscriptionData(data, channels))
                hasAnyConsent = true;
        }
        else if (data.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in data.EnumerateArray())
            {
                if (TryParseSubscriptionData(item, channels))
                    hasAnyConsent = true;
            }
        }

        return hasAnyConsent;
    }

    private static bool TryParseSubscriptionData(JsonElement data, ISet<KlaviyoConsentChannel> channels)
    {
        if (data.ValueKind != JsonValueKind.Object)
            return false;

        var attributes = data.TryGetProperty("attributes", out var attrs)
            ? attrs
            : default;

        if (attributes.ValueKind == JsonValueKind.Object)
        {
            if (attributes.TryGetProperty("subscriptions", out var subs) &&
                TryParseSubscriptionsObject(subs, channels))
            {
                return true;
            }

            var channelValue = GetString(attributes, "channel");
            var channel = ParseChannel(channelValue);

            if (channel is not null && TryGetConsentState(attributes, out var isSubscribed))
            {
                if (isSubscribed)
                    channels.Add(channel.Value);

                return true;
            }
        }

        return false;
    }

    private static KlaviyoConsentChannel? ParseChannel(string? channel)
    {
        if (string.IsNullOrWhiteSpace(channel)) return null;

        return channel.Trim().ToLowerInvariant() switch
        {
            "email" => KlaviyoConsentChannel.Email,
            "sms" => KlaviyoConsentChannel.Sms,
            "push" => KlaviyoConsentChannel.Push,
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
