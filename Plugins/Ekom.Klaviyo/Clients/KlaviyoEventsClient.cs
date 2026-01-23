using Ekom.Klaviyo.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Clients;

internal interface IKlaviyoEventsClient
{
    /// <summary>
    /// Sends a batch of already-mapped Klaviyo event payloads.
    /// The caller controls schema; this method only transports.
    /// </summary>
    Task TrackEventAsync(object eventPayload, string storeAlias, CancellationToken ct = default);
}

internal sealed class KlaviyoEventsClient : IKlaviyoEventsClient
{
    private readonly KlaviyoHttpClient _http;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoEventsClient> _logger;

    public KlaviyoEventsClient(
        KlaviyoHttpClient http,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoEventsClient> logger)
    {
        _http = http;
        _opt = options.Value;
        _logger = logger;
    }

    public async Task TrackEventAsync(object eventPayload, string storeAlias, CancellationToken ct = default)
    {
        if (!_opt.Enabled || !_opt.Events.Enabled || eventPayload is null)
            return;

        var payload = new { data = eventPayload };

        _logger.LogDebug("Klaviyo: sending 1 event");

        await _http.PostAsync("/api/events", payload, storeAlias, ct);
    }
}
