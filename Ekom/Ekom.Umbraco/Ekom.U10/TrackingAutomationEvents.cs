using Ekom.Events;
using Ekom.Models;
using Ekom.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Ekom.Umb;

internal sealed class TrackingAutomationEvents : IComponent
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TrackingOptions _options;

    public TrackingAutomationEvents(IServiceScopeFactory scopeFactory, IOptions<TrackingOptions> options)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    public void Initialize()
    {
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
        OrderEvents.AddedOrderlineAsync += OnAddedOrderlineAsync;
        OrderEvents.AddedOrderlineAsync += OnAddedOrderlineMetaAsync;
        OrderEvents.RemovedOrderlineAsync += OnRemovedOrderlineAsync;
        OrderEvents.RemovedOrderlineAsync += OnRemovedOrderlineMetaAsync;
        OrderEvents.CustomerEmailAddedAsync += OnCustomerEmailAddedAsync;
        OrderEvents.CustomerEmailAddedAsync += OnCustomerEmailAddedMetaAsync;
        OrderEvents.ShippingProviderAddedAsync += OnShippingProviderAddedAsync;
        OrderEvents.ShippingProviderAddedAsync += OnShippingProviderAddedMetaAsync;
        OrderEvents.PaymentProviderAddedAsync += OnPaymentProviderAddedAsync;
        OrderEvents.PaymentProviderAddedAsync += OnPaymentProviderAddedMetaAsync;
    }

    public void Terminate()
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
        OrderEvents.AddedOrderlineAsync -= OnAddedOrderlineAsync;
        OrderEvents.AddedOrderlineAsync -= OnAddedOrderlineMetaAsync;
        OrderEvents.RemovedOrderlineAsync -= OnRemovedOrderlineAsync;
        OrderEvents.RemovedOrderlineAsync -= OnRemovedOrderlineMetaAsync;
        OrderEvents.CustomerEmailAddedAsync -= OnCustomerEmailAddedAsync;
        OrderEvents.CustomerEmailAddedAsync -= OnCustomerEmailAddedMetaAsync;
        OrderEvents.ShippingProviderAddedAsync -= OnShippingProviderAddedAsync;
        OrderEvents.ShippingProviderAddedAsync -= OnShippingProviderAddedMetaAsync;
        OrderEvents.PaymentProviderAddedAsync -= OnPaymentProviderAddedAsync;
        OrderEvents.PaymentProviderAddedAsync -= OnPaymentProviderAddedMetaAsync;
    }

    private async Task OnAddedOrderlineAsync(object sender, AddedOrderlineEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Ga4.Enabled || !_options.Ga4.Events.AddedToCart)
            return;

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var ga4Service = scope.ServiceProvider.GetRequiredService<IGa4TrackingService>();
        var request = ga4Service.CreateAddedToCartRequest(args.OrderInfo, args.OrderLine);
        request.HasAnalyticsConsent = consentService.CanCaptureAnalytics(args.OrderInfo.Consent);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IGa4TrackingDispatcher>();
        await dispatcher.EnqueueAsync(request, ct).ConfigureAwait(false);
    }

    private async Task OnAddedOrderlineMetaAsync(object sender, AddedOrderlineEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Meta.Enabled || !_options.Meta.Events.AddedToCart)
            return;

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var metaService = scope.ServiceProvider.GetRequiredService<IMetaTrackingService>();
        var request = metaService.CreateAddedToCartRequest(args.OrderInfo, args.OrderLine);
        request.HasMarketingConsent = consentService.CanCaptureMarketing(args.OrderInfo.Consent);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IMetaTrackingDispatcher>();
        await dispatcher.EnqueueAsync(request, ct).ConfigureAwait(false);
    }

    private async Task OnRemovedOrderlineAsync(object sender, RemovedOrderlineEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Ga4.Enabled || !_options.Ga4.Events.RemovedFromCart)
            return;

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var ga4Service = scope.ServiceProvider.GetRequiredService<IGa4TrackingService>();
        var request = ga4Service.CreateRemovedFromCartRequest(args.OrderInfo, args.OrderLine);
        request.HasAnalyticsConsent = consentService.CanCaptureAnalytics(args.OrderInfo.Consent);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IGa4TrackingDispatcher>();
        await dispatcher.EnqueueAsync(request, ct).ConfigureAwait(false);
    }

    private async Task OnRemovedOrderlineMetaAsync(object sender, RemovedOrderlineEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Meta.Enabled || !_options.Meta.Events.RemovedFromCart)
            return;

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var metaService = scope.ServiceProvider.GetRequiredService<IMetaTrackingService>();
        var request = metaService.CreateRemovedFromCartRequest(args.OrderInfo, args.OrderLine);
        request.HasMarketingConsent = consentService.CanCaptureMarketing(args.OrderInfo.Consent);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IMetaTrackingDispatcher>();
        await dispatcher.EnqueueAsync(request, ct).ConfigureAwait(false);
    }

    private async Task OnCustomerEmailAddedAsync(object sender, CustomerEmailAddedEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Ga4.Enabled || !_options.Ga4.Events.StartedCheckout)
            return;

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var ga4Service = scope.ServiceProvider.GetRequiredService<IGa4TrackingService>();
        var request = ga4Service.CreateStartedCheckoutRequest(args.OrderInfo);
        request.HasAnalyticsConsent = consentService.CanCaptureAnalytics(args.OrderInfo.Consent);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IGa4TrackingDispatcher>();
        await dispatcher.EnqueueAsync(request, ct).ConfigureAwait(false);
    }

    private async Task OnCustomerEmailAddedMetaAsync(object sender, CustomerEmailAddedEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Meta.Enabled || !_options.Meta.Events.StartedCheckout)
            return;

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var metaService = scope.ServiceProvider.GetRequiredService<IMetaTrackingService>();
        var request = metaService.CreateStartedCheckoutRequest(args.OrderInfo);
        request.HasMarketingConsent = consentService.CanCaptureMarketing(args.OrderInfo.Consent);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IMetaTrackingDispatcher>();
        await dispatcher.EnqueueAsync(request, ct).ConfigureAwait(false);
    }

    private async Task OnShippingProviderAddedAsync(object sender, ShippingProviderAddedEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Ga4.Enabled || !_options.Ga4.Events.AddedShippingInfo)
            return;

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var ga4Service = scope.ServiceProvider.GetRequiredService<IGa4TrackingService>();
        var request = ga4Service.CreateAddedShippingInfoRequest(args.OrderInfo);
        request.HasAnalyticsConsent = consentService.CanCaptureAnalytics(args.OrderInfo.Consent);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IGa4TrackingDispatcher>();
        await dispatcher.EnqueueAsync(request, ct).ConfigureAwait(false);
    }

    private async Task OnShippingProviderAddedMetaAsync(object sender, ShippingProviderAddedEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Meta.Enabled || !_options.Meta.Events.AddedShippingInfo)
            return;

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var metaService = scope.ServiceProvider.GetRequiredService<IMetaTrackingService>();
        var request = metaService.CreateAddedShippingInfoRequest(args.OrderInfo);
        request.HasMarketingConsent = consentService.CanCaptureMarketing(args.OrderInfo.Consent);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IMetaTrackingDispatcher>();
        await dispatcher.EnqueueAsync(request, ct).ConfigureAwait(false);
    }

    private async Task OnPaymentProviderAddedAsync(object sender, PaymentProviderAddedEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Ga4.Enabled || !_options.Ga4.Events.AddedPaymentInfo)
            return;

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var ga4Service = scope.ServiceProvider.GetRequiredService<IGa4TrackingService>();
        var request = ga4Service.CreateAddedPaymentInfoRequest(args.OrderInfo);
        request.HasAnalyticsConsent = consentService.CanCaptureAnalytics(args.OrderInfo.Consent);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IGa4TrackingDispatcher>();
        await dispatcher.EnqueueAsync(request, ct).ConfigureAwait(false);
    }

    private async Task OnPaymentProviderAddedMetaAsync(object sender, PaymentProviderAddedEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Meta.Enabled || !_options.Meta.Events.AddedPaymentInfo)
            return;

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var metaService = scope.ServiceProvider.GetRequiredService<IMetaTrackingService>();
        var request = metaService.CreateAddedPaymentInfoRequest(args.OrderInfo);
        request.HasMarketingConsent = consentService.CanCaptureMarketing(args.OrderInfo.Consent);

        var dispatcher = scope.ServiceProvider.GetRequiredService<IMetaTrackingDispatcher>();
        await dispatcher.EnqueueAsync(request, ct).ConfigureAwait(false);
    }

    private async Task OnCompleteCheckoutAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || args.OrderInfo == null)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var consent = args.OrderInfo.Consent;

        if (_options.Ga4.Enabled)
        {
            var ga4Service = scope.ServiceProvider.GetRequiredService<IGa4TrackingService>();
            var request = ga4Service.CreatePurchaseRequest(args.OrderInfo);
            request.HasAnalyticsConsent = consentService.CanCaptureAnalytics(consent);
            var eventArgs = new Ga4PurchasePreparingEventArgs
            {
                OrderInfo = args.OrderInfo,
                Request = request
            };

            await TrackingEvents.OnGa4PurchasePreparingAsync(this, eventArgs, ct).ConfigureAwait(false);

            if (!eventArgs.Cancel)
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IGa4TrackingDispatcher>();
                await dispatcher.EnqueueAsync(eventArgs.Request, ct).ConfigureAwait(false);
            }
        }

        if (_options.Meta.Enabled)
        {
            var metaService = scope.ServiceProvider.GetRequiredService<IMetaTrackingService>();
            var request = metaService.CreatePurchaseRequest(args.OrderInfo);
            request.HasMarketingConsent = consentService.CanCaptureMarketing(consent);
            var eventArgs = new MetaPurchasePreparingEventArgs
            {
                OrderInfo = args.OrderInfo,
                Request = request
            };

            await TrackingEvents.OnMetaPurchasePreparingAsync(this, eventArgs, ct).ConfigureAwait(false);

            if (!eventArgs.Cancel)
            {
                var dispatcher = scope.ServiceProvider.GetRequiredService<IMetaTrackingDispatcher>();
                await dispatcher.EnqueueAsync(eventArgs.Request, ct).ConfigureAwait(false);
            }
        }
    }
}

internal sealed class TrackingAutomationComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Components().Append<TrackingAutomationEvents>();
    }
}
