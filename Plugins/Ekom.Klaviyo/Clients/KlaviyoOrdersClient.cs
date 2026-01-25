using Ekom.Klaviyo.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Clients;

internal interface IKlaviyoOrdersClient
{
    Task TrackOrderEventAsync(object eventPayload, string storeAlias, CancellationToken ct = default);
}

internal sealed class KlaviyoOrdersClient : IKlaviyoOrdersClient
{
    private readonly KlaviyoHttpClient _http;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoOrdersClient> _logger;

    public KlaviyoOrdersClient(
        KlaviyoHttpClient http,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoOrdersClient> logger)
    {
        _http = http;
        _opt = options.Value;
        _logger = logger;
    }

    public async Task TrackOrderEventAsync(object eventPayload, string storeAlias, CancellationToken ct = default)
    {
        if (!_opt.Enabled || !_opt.Orders.Enabled || eventPayload is null)
            return;

        var payload = new { data = eventPayload };

        _logger.LogDebug("Klaviyo: sending 1 order event");

        await _http.PostAsync("/api/events", payload, storeAlias, ct);
    }
}
