using Ekom.Events;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Services;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Ekom.Klaviyo.Events;
internal class KlaviyoEkomEvents : IComponent
{
    private readonly IKlaviyoOrderService _orderService;
    private readonly KlaviyoOptions _opt;

    public KlaviyoEkomEvents(IKlaviyoOrderService orderService, IOptions<KlaviyoOptions> opt)
    {
        _orderService = orderService;
        _opt = opt.Value;
    }

    public void Initialize()
    {
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
    }

    private async Task OnCompleteCheckoutAsync(object e, CompleteCheckoutEventArgs args)
    {

        if (!_opt.Events.TrackingPlacedOrders) { return; }

        var orderInfo = args.OrderInfo;

        if (orderInfo == null) { return; }

        var klaviyoOrder = orderInfo.ToKlaviyoPlacedOrder(_opt.SiteBaseUrl);

        await _orderService.TrackPlacedOrderAsync(
            klaviyoOrder,
            CancellationToken.None);
    }

    public void Terminate()
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
    }
}

internal class KlaviyoEkomEventsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Components().Append<KlaviyoEkomEvents>();
    }
}
