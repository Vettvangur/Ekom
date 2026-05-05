using Ekom.Klaviyo.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Clients;

internal interface IKlaviyoEventsClient
{
    Task SendEventAsync(object requestPayload, string storeAlias, CancellationToken ct = default);
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

    public async Task SendEventAsync(
        object requestPayload,
        string storeAlias,
        CancellationToken ct = default)
    {
        if (!_opt.Enabled || requestPayload is null)
            return;

        try
        {
            _logger.LogDebug(
                "Klaviyo: sending event for store {StoreAlias}",
                storeAlias);

            await _http.PostAsync("/api/events", requestPayload, storeAlias, ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            _logger.LogDebug(
                "Klaviyo: event send cancelled for store {StoreAlias}",
                storeAlias);

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Klaviyo: failed to send event for store {StoreAlias}. PayloadType={PayloadType}",
                storeAlias,
                requestPayload.GetType().Name);
        }
    }
}
