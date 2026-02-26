using Ekom.Klaviyo.Dispatching.Tracking;
using Ekom.Klaviyo.Enrichers.TrackingEnricher;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Orders;
using Ekom.Klaviyo.Models.Profiles;
using Ekom.Klaviyo.Models.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Umbraco.Extensions;

namespace Ekom.Klaviyo.Services;

public interface IKlaviyoTrackingService
{
    ValueTask TrackSearchAsync(KlaviyoSearchEvent payload, CancellationToken ct = default);
    ValueTask TrackAddedToCartAsync(KlaviyoAddedToCartEvent payload, CancellationToken ct = default);
    ValueTask TrackViewedCategoryAsync(KlaviyoViewedCategoryEvent payload, CancellationToken ct = default);
    ValueTask TrackViewedProductAsync(KlaviyoViewedProductEvent payload, CancellationToken ct = default);
    ValueTask TrackActiveOnSiteAsync(KlaviyoActiveOnSiteEvent payload, CancellationToken ct = default);
    ValueTask TrackStartedCheckoutAsync(KlaviyoStartedCheckoutEvent payload, CancellationToken ct = default);
}

public sealed class KlaviyoTrackingService : IKlaviyoTrackingService
{
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoTrackingService> _logger;
    private readonly IKlaviyoTrackingDispatcher _dispatcher;
    private readonly IKlaviyoTrackingEnricherRunner? _enrichers;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHostApplicationLifetime _appLifetime;

    public KlaviyoTrackingService(
        IOptions<KlaviyoOptions> opt,
        ILogger<KlaviyoTrackingService> logger,
        IKlaviyoTrackingDispatcher dispatcher,
        IKlaviyoTrackingEnricherRunner? enrichers,
        IServiceScopeFactory scopeFactory,
        IHostApplicationLifetime appLifetime)
    {
        _opt = opt.Value;
        _logger = logger;
        _dispatcher = dispatcher;
        _enrichers = enrichers;
        _scopeFactory = scopeFactory;
        _appLifetime = appLifetime;
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

    private async ValueTask TryProfileUpdateStartedCheckoutAsync(
        KlaviyoStartedCheckoutEvent payload,
        IKlaviyoProfilesService profiles,
        CancellationToken ct)
    {
        if (payload is null || payload.Customer is null)
            return;

        if (string.IsNullOrWhiteSpace(payload.ListId) &&
            string.IsNullOrWhiteSpace(payload.Customer.FirstName))
        {
            return;
        }

        await Task.Delay(TimeSpan.FromSeconds(10), ct).ConfigureAwait(false);

        var listId = ResolveListId(payload.StoreAlias, payload.ListId);

        var email = payload?.Customer?.Email;
        if (string.IsNullOrWhiteSpace(email))
        {
            _logger.LogDebug(
                "Klaviyo: skipping started checkout list subscribe because no email was provided. Store={StoreAlias}",
                payload.StoreAlias);
            return;
        }

        var profilePayload = new KlaviyoProfileUpdate(
            StoreAlias: payload.StoreAlias,
            Profile: new KlaviyoProfile
            {
                Customer = new KlaviyoCustomer
                {
                    Email = payload?.Customer?.Email,
                    PhoneNumber = payload?.Customer?.PhoneNumber
                },
                Attributes = new KlaviyoProfileAttributes
                {
                    FirstName = payload?.Customer?.FirstName,
                    LastName = payload?.Customer?.LastName
                }
            },
            ListId: listId);

        await profiles.UpsertProfileAsync(profilePayload, ct).ConfigureAwait(false);
    }

    private void QueueStartedCheckoutProfileUpdate(KlaviyoStartedCheckoutEvent payload)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var profiles = scope.ServiceProvider.GetRequiredService<IKlaviyoProfilesService>();

                await TryProfileUpdateStartedCheckoutAsync(payload, profiles, _appLifetime.ApplicationStopping)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_appLifetime.ApplicationStopping.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Klaviyo: failed background profile update for started checkout.");
            }
        }, _appLifetime.ApplicationStopping);
    }

    private string? ResolveListId(string storeAlias, string? listId)
    {
        if (!string.IsNullOrWhiteSpace(listId))
            return listId;

        var storeListId = _opt.Stores
            .FirstOrDefault(x => x.Alias.InvariantEquals(storeAlias))
            ?.ListId;

        if (!string.IsNullOrWhiteSpace(storeListId))
            return storeListId;

        return _opt.Subscriptions.DefaultListId;
    }

    public async ValueTask TrackAddedToCartAsync(KlaviyoAddedToCartEvent payload, CancellationToken ct = default)
    {
        if (!IsEnabled(_opt.Tracking.AddedToCart)) return;

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(KlaviyoTrackingEventType.AddedToCart, payload, payload.StoreAlias, ct);

        if (payload.Customer is null || !payload.Customer.HasIdentifier)
        {
            return;
        }

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

        if (payload.Customer is null || !payload.Customer.HasIdentifier)
        {
            return;
        }

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

    public async ValueTask TrackStartedCheckoutAsync(KlaviyoStartedCheckoutEvent payload, CancellationToken ct = default)
    {
        if (!IsEnabled(_opt.Tracking.StartedCheckout)) return;

        if (_enrichers is not null)
            await _enrichers.ApplyAsync(KlaviyoTrackingEventType.StartedCheckout, payload, payload.StoreAlias, ct);

        if (!ValidatePayload(payload, "Started Checkout")) return;

        var work = new KlaviyoTrackingWork(
            Type: KlaviyoTrackingEventType.StartedCheckout,
            EventPayload: payload.ToTrackingEvent(_opt),
            OccurredAt: payload.OccurredAt,
            StoreAlias: payload.StoreAlias,
            EventId: payload.EventId ?? string.Empty);

        await _dispatcher.EnqueueAsync(work, ct);

        QueueStartedCheckoutProfileUpdate(payload);
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

    private bool ValidatePayload(KlaviyoStartedCheckoutEvent payload, string eventName)
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
