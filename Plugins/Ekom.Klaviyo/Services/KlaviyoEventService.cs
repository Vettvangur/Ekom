using Ekom.Klaviyo.Clients;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Events;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Services;

public interface IKlaviyoEventService
{
    Task SendEventAsync(KlaviyoCustomEvent payload, CancellationToken ct = default);

    Task SendEventAsync(
        string storeAlias,
        string eventName,
        object? properties,
        KlaviyoEventProfile profile,
        DateTimeOffset? occurredAt = null,
        string? uniqueId = null,
        CancellationToken ct = default);

    Task SendRawEventAsync(string storeAlias, object payload, CancellationToken ct = default);
}

internal sealed class KlaviyoEventService : IKlaviyoEventService
{
    private readonly KlaviyoOptions _opt;
    private readonly IKlaviyoEventsClient _client;
    private readonly ILogger<KlaviyoEventService> _logger;

    public KlaviyoEventService(
        IOptions<KlaviyoOptions> opt,
        IKlaviyoEventsClient client,
        ILogger<KlaviyoEventService> logger)
    {
        _opt = opt.Value;
        _client = client;
        _logger = logger;
    }

    public async Task SendEventAsync(KlaviyoCustomEvent payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!_opt.Enabled)
            return;

        if (!ValidatePayload(payload.StoreAlias, payload.EventName, payload.Profile))
            return;

        await _client.SendEventAsync(payload.ToCustomEventRequest(_opt), payload.StoreAlias, ct).ConfigureAwait(false);
    }

    public Task SendEventAsync(
        string storeAlias,
        string eventName,
        object? properties,
        KlaviyoEventProfile profile,
        DateTimeOffset? occurredAt = null,
        string? uniqueId = null,
        CancellationToken ct = default)
    {
        var payload = new KlaviyoCustomEvent
        {
            StoreAlias = storeAlias,
            EventName = eventName,
            Properties = properties,
            Profile = profile,
            OccurredAt = occurredAt ?? DateTimeOffset.UtcNow,
            UniqueId = uniqueId
        };

        return SendEventAsync(payload, ct);
    }

    public async Task SendRawEventAsync(string storeAlias, object payload, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(payload);

        if (!_opt.Enabled)
            return;

        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            _logger.LogWarning(
                "Klaviyo: skipping raw event because no store alias was provided.");
            return;
        }

        await _client.SendEventAsync(payload, storeAlias, ct).ConfigureAwait(false);
    }

    private bool ValidatePayload(string storeAlias, string eventName, KlaviyoEventProfile profile)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            _logger.LogWarning(
                "Klaviyo: skipping {EventName} because no store alias was provided.",
                eventName);
            return false;
        }

        if (string.IsNullOrWhiteSpace(eventName))
        {
            _logger.LogWarning(
                "Klaviyo: skipping event because no event name was provided. Store={StoreAlias}",
                storeAlias);
            return false;
        }

        if (profile is null || !profile.HasIdentifier)
        {
            _logger.LogWarning(
                "Klaviyo: skipping {EventName} because no customer identifier was provided. Store={StoreAlias}",
                eventName,
                storeAlias);
            return false;
        }

        return true;
    }
}
