using Ekom.Klaviyo.Exceptions;
using Ekom.Klaviyo.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Clients;

internal interface IKlaviyoProfilesClient
{
    Task<string?> GetByIdAsync(
        string profileId,
        string? storeAlias,
        bool includeSubscriptions,
        CancellationToken ct = default);

    Task<string?> GetByEmailAsync(
        string email,
        string? storeAlias,
        bool includeSubscriptions,
        CancellationToken ct = default);

    Task<string?> GetSubscriptionsAsync(
        string profileId,
        string? storeAlias,
        CancellationToken ct = default);

    Task<string?> GetListIdsAsync(
        string profileId,
        string? storeAlias,
        CancellationToken ct = default);

    Task UpsertProfileAsync(object profileImportRequest, string storeAlias, CancellationToken ct = default);
    Task BulkSubscribeAsync(object bulkSubscribeJobRequest, string storeAlias, CancellationToken ct = default);
    Task BulkUnsubscribeAsync(object bulkUnsubscribeJobRequest, string storeAlias, CancellationToken ct = default);
}

internal sealed class KlaviyoProfilesClient : IKlaviyoProfilesClient
{
    private readonly KlaviyoHttpClient _http;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoProfilesClient> _logger;

    public KlaviyoProfilesClient(
        KlaviyoHttpClient http,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoProfilesClient> logger)
    {
        _http = http;
        _opt = options.Value;
        _logger = logger;
    }

    public async Task<string?> GetByIdAsync(
        string profileId,
        string? storeAlias,
        bool includeSubscriptions,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return null;
        if (!_opt.Enabled) return null;

        var path = $"/api/profiles/{Uri.EscapeDataString(profileId)}";

        _logger.LogDebug("Klaviyo: profiles GET by id for store {StoreAlias}", storeAlias);

        try
        {
            return await _http.GetAsync(path, storeAlias, ct).ConfigureAwait(false);
        }
        catch (KlaviyoApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task<string?> GetByEmailAsync(
        string email,
        string? storeAlias,
        bool includeSubscriptions,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return null;
        if (!_opt.Enabled) return null;

        var sanitizedEmail = email.Replace("\"", "\\\"");
        var filter = $"equals(email,\"{sanitizedEmail}\")";
        var query = $"?filter={Uri.EscapeDataString(filter)}";

        var path = $"/api/profiles{query}";

        _logger.LogDebug("Klaviyo: profiles GET by email for store {StoreAlias}", storeAlias);

        return await _http.GetAsync(path, storeAlias, ct).ConfigureAwait(false);
    }

    public async Task<string?> GetSubscriptionsAsync(
        string profileId,
        string? storeAlias,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return null;
        if (!_opt.Enabled) return null;

        var filter = $"equals(profile_id,\"{profileId.Replace("\"", "\\\"")}\")";
        var path = $"/api/profile-subscriptions?filter={Uri.EscapeDataString(filter)}";

        _logger.LogDebug("Klaviyo: profile-subscriptions GET for store {StoreAlias}", storeAlias);

        try
        {
            return await _http.GetAsync(path, storeAlias, ct).ConfigureAwait(false);
        }
        catch (KlaviyoApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task<string?> GetListIdsAsync(
        string profileId,
        string? storeAlias,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(profileId)) return null;
        if (!_opt.Enabled) return null;

        var path = $"/api/profiles/{Uri.EscapeDataString(profileId)}/relationships/lists";

        _logger.LogDebug("Klaviyo: profile lists GET for store {StoreAlias}", storeAlias);

        try
        {
            return await _http.GetAsync(path, storeAlias, ct).ConfigureAwait(false);
        }
        catch (KlaviyoApiException ex) when (ex.StatusCode == 404)
        {
            return null;
        }
    }

    public async Task UpsertProfileAsync(object profileImportRequest, string storeAlias, CancellationToken ct = default)
    {
        if (!IsSubscriptionsEnabled(profileImportRequest)) return;

        _logger.LogDebug("Klaviyo: profile-import (upsert) for store {StoreAlias}", storeAlias);

        // POST https://a.klaviyo.com/api/profile-import
        await _http.PostAsync("/api/profile-import", profileImportRequest, storeAlias, ct).ConfigureAwait(false);
    }

    public async Task BulkSubscribeAsync(object bulkSubscribeJobRequest, string storeAlias, CancellationToken ct = default)
    {
        if (!IsSubscriptionsEnabled(bulkSubscribeJobRequest)) return;

        _logger.LogDebug("Klaviyo: profile-subscription-bulk-create-jobs for store {StoreAlias}", storeAlias);

        // POST https://a.klaviyo.com/api/profile-subscription-bulk-create-jobs
        await _http.PostAsync("/api/profile-subscription-bulk-create-jobs", bulkSubscribeJobRequest, storeAlias, ct)
            .ConfigureAwait(false);
    }

    public async Task BulkUnsubscribeAsync(object bulkUnsubscribeJobRequest, string storeAlias, CancellationToken ct = default)
    {
        if (!IsSubscriptionsEnabled(bulkUnsubscribeJobRequest)) return;

        _logger.LogDebug("Klaviyo: profile-subscription-bulk-delete-jobs for store {StoreAlias}", storeAlias);

        // POST https://a.klaviyo.com/api/profile-subscription-bulk-delete-jobs
        await _http.PostAsync("/api/profile-subscription-bulk-delete-jobs", bulkUnsubscribeJobRequest, storeAlias, ct)
            .ConfigureAwait(false);
    }

    private bool IsSubscriptionsEnabled(object payload)
        => _opt.Enabled && _opt.Subscriptions.Enabled && payload is not null;
}
