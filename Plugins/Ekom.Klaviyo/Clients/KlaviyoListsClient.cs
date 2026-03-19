using Ekom.Klaviyo.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Clients;

internal interface IKlaviyoListsClient
{
    Task AddProfilesToListAsync(string listId, object addToListRequest, string storeAlias, CancellationToken ct = default);
}

internal sealed class KlaviyoListsClient : IKlaviyoListsClient
{
    private readonly KlaviyoHttpClient _http;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoListsClient> _logger;

    public KlaviyoListsClient(
        KlaviyoHttpClient http,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoListsClient> logger)
    {
        _http = http;
        _opt = options.Value;
        _logger = logger;
    }

    public async Task AddProfilesToListAsync(string listId, object addToListRequest, string storeAlias, CancellationToken ct = default)
    {
        if (!IsEnabled(addToListRequest) || string.IsNullOrWhiteSpace(listId)) return;

        _logger.LogDebug("Klaviyo: list add profiles {ListId} for store {StoreAlias}", listId, storeAlias);

        // POST https://a.klaviyo.com/api/lists/{id}/relationships/profiles
        await _http.PostAsync($"/api/lists/{listId}/relationships/profiles", addToListRequest, storeAlias, ct)
            .ConfigureAwait(false);
    }

    private bool IsEnabled(object payload)
        => _opt.Enabled && payload is not null;
}
