using Ekom.Klaviyo.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Clients;

internal interface IKlaviyoTrackingClient
{
    Task TrackEventAsync(object eventPayload, string storeAlias, CancellationToken ct = default);
}

internal sealed class KlaviyoTrackingClient : IKlaviyoTrackingClient
{
    private readonly KlaviyoHttpClient _http;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoTrackingClient> _logger;

    public KlaviyoTrackingClient(
        KlaviyoHttpClient http,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoTrackingClient> logger)
    {
        _http = http;
        _opt = options.Value;
        _logger = logger;
    }

    public async Task TrackEventAsync(
        object eventPayload,
        string storeAlias,
        CancellationToken ct = default)
    {
        if (!_opt.Enabled || !_opt.Tracking.Enabled || eventPayload is null)
            return;

        var payload = new { data = eventPayload };

        try
        {
            _logger.LogDebug(
                "Klaviyo: sending tracking event for store {StoreAlias}",
                storeAlias);

            await _http.PostAsync("/api/events", payload, storeAlias, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Klaviyo: tracking event cancelled for store {StoreAlias}",
                storeAlias);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Klaviyo: failed to send tracking event for store {StoreAlias}. PayloadType={PayloadType}",
                storeAlias,
                eventPayload.GetType().Name);
        }
    }
}
