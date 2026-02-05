using Ekom.Klaviyo.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Clients;

internal interface IKlaviyoSubscriptionsClient
{
    Task UpsertProfileAsync(object profileImportRequest, string storeAlias, CancellationToken ct = default);
    Task BulkSubscribeAsync(object bulkSubscribeJobRequest, string storeAlias, CancellationToken ct = default);
    Task BulkUnsubscribeAsync(object bulkUnsubscribeJobRequest, string storeAlias, CancellationToken ct = default);
}

internal sealed class KlaviyoSubscriptionsClient : IKlaviyoSubscriptionsClient
{
    private readonly KlaviyoHttpClient _http;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoSubscriptionsClient> _logger;

    public KlaviyoSubscriptionsClient(
        KlaviyoHttpClient http,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoSubscriptionsClient> logger)
    {
        _http = http;
        _opt = options.Value;
        _logger = logger;
    }

    public async Task UpsertProfileAsync(object profileImportRequest, string storeAlias, CancellationToken ct = default)
    {
        if (!IsEnabled(profileImportRequest)) return;

        _logger.LogDebug("Klaviyo: profile-import (upsert) for store {StoreAlias}", storeAlias);

        // POST https://a.klaviyo.com/api/profile-import
        await _http.PostAsync("/api/profile-import", profileImportRequest, storeAlias, ct).ConfigureAwait(false);
    }

    public async Task BulkSubscribeAsync(object bulkSubscribeJobRequest, string storeAlias, CancellationToken ct = default)
    {
        if (!IsEnabled(bulkSubscribeJobRequest)) return;

        _logger.LogDebug("Klaviyo: profile-subscription-bulk-create-jobs for store {StoreAlias}", storeAlias);

        // POST https://a.klaviyo.com/api/profile-subscription-bulk-create-jobs
        await _http.PostAsync("/api/profile-subscription-bulk-create-jobs", bulkSubscribeJobRequest, storeAlias, ct)
            .ConfigureAwait(false);
    }

    public async Task BulkUnsubscribeAsync(object bulkUnsubscribeJobRequest, string storeAlias, CancellationToken ct = default)
    {
        if (!IsEnabled(bulkUnsubscribeJobRequest)) return;

        _logger.LogDebug("Klaviyo: profile-subscription-bulk-delete-jobs for store {StoreAlias}", storeAlias);

        // POST https://a.klaviyo.com/api/profile-subscription-bulk-delete-jobs
        await _http.PostAsync("/api/profile-subscription-bulk-delete-jobs", bulkUnsubscribeJobRequest, storeAlias, ct)
            .ConfigureAwait(false);
    }

    private bool IsEnabled(object payload)
        => _opt.Enabled && _opt.Subscriptions.Enabled && payload is not null;
}
