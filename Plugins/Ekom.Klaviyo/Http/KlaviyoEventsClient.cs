using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Http;

internal interface IKlaviyoEventsClient
{
    /// <summary>
    /// Sends a batch of already-mapped Klaviyo event payloads.
    /// The caller controls schema; this method only transports.
    /// </summary>
    Task TrackEventsAsync(IReadOnlyList<object> eventsPayload, CancellationToken ct = default);
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

    public async Task TrackEventsAsync(IReadOnlyList<object> eventsPayload, CancellationToken ct = default)
    {
        if (!_opt.Enabled || !_opt.Events.Enabled || eventsPayload.Count == 0)
            return;

        // NOTE: You must confirm the exact endpoint + schema you standardize on.
        // This client intentionally mirrors your catalog client pattern and delegates schema control to caller.
        //
        // If you already decided on a specific Klaviyo events endpoint (e.g. /api/events),
        // keep it here and centralize the mapping in your Event dispatcher/service.
        var payload = new
        {
            data = eventsPayload
        };

        _logger.LogDebug("Klaviyo: sending {Count} events", eventsPayload.Count);

        await _http.PostAsync("/api/events", payload, ct);
    }
}
