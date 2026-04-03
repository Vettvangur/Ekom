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
    }

    public void Terminate()
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
    }

    private async Task OnCompleteCheckoutAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || args.OrderInfo?.Tracking?.HasData() != true)
        {
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var consentService = scope.ServiceProvider.GetRequiredService<ITrackingConsentService>();
        var consent = args.OrderInfo.Consent;

        if (_options.Ga4.Enabled && consentService.CanCaptureAnalytics(consent))
        {
            var ga4Service = scope.ServiceProvider.GetRequiredService<IGa4TrackingService>();
            var request = ga4Service.CreatePurchaseRequest(args.OrderInfo);
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

        if (_options.Meta.Enabled && consentService.CanCaptureMarketing(consent))
        {
            var metaService = scope.ServiceProvider.GetRequiredService<IMetaTrackingService>();
            var request = metaService.CreatePurchaseRequest(args.OrderInfo);
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
