using Ekom.Klaviyo.Dispatching.Tracking;
using Ekom.Klaviyo.Enrichers.TrackingEnricher;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Orders;
using Ekom.Klaviyo.Models.Tracking;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo.Services;

public interface IKlaviyoTrackingService
{
    ValueTask TrackSearchAsync(KlaviyoSearchEvent payload, CancellationToken ct = default);
    ValueTask TrackAddedToCartAsync(KlaviyoAddedToCartEvent payload, CancellationToken ct = default);
    ValueTask TrackViewedCategoryAsync(KlaviyoViewedCategoryEvent payload, CancellationToken ct = default);
    ValueTask TrackViewedProductAsync(KlaviyoViewedProductEvent payload, CancellationToken ct = default);
    ValueTask TrackActiveOnSiteAsync(KlaviyoActiveOnSiteEvent payload, CancellationToken ct = default);
    ValueTask TrackCheckoutStartedAsync(KlaviyoCheckoutStartedEvent payload, CancellationToken ct = default);
}

public sealed class KlaviyoTrackingService : IKlaviyoTrackingService
{
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoTrackingService> _logger;
    private readonly IKlaviyoTrackingDispatcher _dispatcher;
    private readonly IKlaviyoTrackingEnricherRunner? _enrichers;

    public KlaviyoTrackingService(
        IOptions<KlaviyoOptions> opt,
        ILogger<KlaviyoTrackingService> logger,
        IKlaviyoTrackingDispatcher dispatcher,
        IKlaviyoTrackingEnricherRunner? enrichers = null)
    {
        _opt = opt.Value;
        _logger = logger;
        _dispatcher = dispatcher;
        _enrichers = enrichers;
    }

    public async ValueTask TrackSearchAsync(KlaviyoSearchEvent payload, CancellationToken ct = default)
    {
        if (!IsEnabled(_opt.Tracking.Search)) return;

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(KlaviyoTrackingEventType.Search, payload, payload.StoreAlias, ct);

        if (!ValidatePayload(payload, "Search")) return;

        var work = new KlaviyoTrackingWork(
            Type: KlaviyoTrackingEventType.Search,
            EventPayload: payload.ToTrackingEvent(_opt),
            OccurredAt: payload.OccurredAt,
            StoreAlias: payload.StoreAlias,
            EventId: payload.EventId ?? string.Empty);

        await _dispatcher.EnqueueAsync(work, ct);
    }

    public async ValueTask TrackAddedToCartAsync(KlaviyoAddedToCartEvent payload, CancellationToken ct = default)
    {
        if (!IsEnabled(_opt.Tracking.AddedToCart)) return;

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(KlaviyoTrackingEventType.AddedToCart, payload, payload.StoreAlias, ct);

        if (!ValidatePayload(payload, "Added to Cart")) return;

        var work = new KlaviyoTrackingWork(
            Type: KlaviyoTrackingEventType.AddedToCart,
            EventPayload: payload.ToTrackingEvent(_opt),
            OccurredAt: payload.OccurredAt,
            StoreAlias: payload.StoreAlias,
            EventId: payload.EventId ?? string.Empty);

        await _dispatcher.EnqueueAsync(work, ct);
    }

    public async ValueTask TrackViewedCategoryAsync(KlaviyoViewedCategoryEvent payload, CancellationToken ct = default)
    {
        if (!IsEnabled(_opt.Tracking.ViewedCategory)) return;

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(KlaviyoTrackingEventType.ViewedCategory, payload, payload.StoreAlias, ct); 
        
        if (!ValidatePayload(payload, "Viewed Category")) return;

        var work = new KlaviyoTrackingWork(
            Type: KlaviyoTrackingEventType.ViewedCategory,
            EventPayload: payload.ToTrackingEvent(_opt),
            OccurredAt: payload.OccurredAt,
            StoreAlias: payload.StoreAlias,
            EventId: payload.EventId ?? string.Empty);

        await _dispatcher.EnqueueAsync(work, ct);
    }

    public async ValueTask TrackViewedProductAsync(KlaviyoViewedProductEvent payload, CancellationToken ct = default)
    {
        if (!IsEnabled(_opt.Tracking.ViewedProduct)) return;

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(KlaviyoTrackingEventType.ViewedProduct, payload, payload.StoreAlias, ct);

        if (!ValidatePayload(payload, "Viewed Product")) return;

        var work = new KlaviyoTrackingWork(
            Type: KlaviyoTrackingEventType.ViewedProduct,
            EventPayload: payload.ToTrackingEvent(_opt),
            OccurredAt: payload.OccurredAt,
            StoreAlias: payload.StoreAlias,
            EventId: payload.EventId ?? string.Empty);

        await _dispatcher.EnqueueAsync(work, ct);
    }

    public async ValueTask TrackActiveOnSiteAsync(KlaviyoActiveOnSiteEvent payload, CancellationToken ct = default)
    {
        if (!IsEnabled(_opt.Tracking.ActiveOnSite)) return;
        
        if (_enrichers is not null)
            await _enrichers.ApplyAsync(KlaviyoTrackingEventType.ActiveOnSite, payload, payload.StoreAlias, ct);

        if (!ValidatePayload(payload, "Active on Site")) return;

        var work = new KlaviyoTrackingWork(
            Type: KlaviyoTrackingEventType.ActiveOnSite,
            EventPayload: payload.ToTrackingEvent(_opt),
            OccurredAt: payload.OccurredAt,
            StoreAlias: payload.StoreAlias,
            EventId: payload.EventId ?? string.Empty);

        await _dispatcher.EnqueueAsync(work, ct);
    }

    public async ValueTask TrackCheckoutStartedAsync(KlaviyoCheckoutStartedEvent payload, CancellationToken ct = default)
    {
        if (!IsEnabled(_opt.Tracking.CheckoutStarted)) return;

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(KlaviyoTrackingEventType.CheckoutStarted, payload, payload.StoreAlias, ct);

        if (!ValidatePayload(payload, "Checkout Started")) return;

        var work = new KlaviyoTrackingWork(
            Type: KlaviyoTrackingEventType.CheckoutStarted,
            EventPayload: payload.ToTrackingEvent(_opt),
            OccurredAt: payload.OccurredAt,
            StoreAlias: payload.StoreAlias,
            EventId: payload.EventId ?? string.Empty);

        await _dispatcher.EnqueueAsync(work, ct);
    }

    private bool IsEnabled(bool eventEnabled)
        => _opt.Enabled && _opt.Tracking.Enabled && eventEnabled;

    private bool ValidatePayload(KlaviyoSearchEvent payload, string eventName)
        => ValidatePayload(payload.StoreAlias, payload.Customer, eventName);

    private bool ValidatePayload(KlaviyoAddedToCartEvent payload, string eventName)
        => ValidatePayload(payload.StoreAlias, payload.Customer, eventName);

    private bool ValidatePayload(KlaviyoViewedCategoryEvent payload, string eventName)
        => ValidatePayload(payload.StoreAlias, payload.Customer, eventName);

    private bool ValidatePayload(KlaviyoViewedProductEvent payload, string eventName)
        => ValidatePayload(payload.StoreAlias, payload.Customer, eventName);

    private bool ValidatePayload(KlaviyoActiveOnSiteEvent payload, string eventName)
        => ValidatePayload(payload.StoreAlias, payload.Customer, eventName);

    private bool ValidatePayload(KlaviyoCheckoutStartedEvent payload, string eventName)
        => ValidatePayload(payload.StoreAlias, payload.Customer, eventName);

    private bool ValidatePayload(string storeAlias, KlaviyoOrderProfile customer, string eventName)
    {
        if (string.IsNullOrWhiteSpace(storeAlias))
        {
            _logger.LogWarning(
                "Klaviyo: skipping {EventName} because no store alias was provided.",
                eventName);
            return false;
        }

        if (customer is null || !customer.HasIdentifier)
        {
            _logger.LogWarning(
                "Klaviyo: skipping {EventName} because no customer identifier was provided. Store={StoreAlias}",
                eventName, storeAlias);
            return false;
        }

        return true;
    }
}
