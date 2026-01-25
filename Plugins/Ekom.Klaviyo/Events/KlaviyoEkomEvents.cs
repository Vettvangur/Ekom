using Ekom.Events;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Ekom.Klaviyo.Events;

internal sealed class KlaviyoEkomEvents : IComponent
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly KlaviyoOptions _opt;

    public KlaviyoEkomEvents(IServiceScopeFactory scopeFactory, IOptions<KlaviyoOptions> opt)
    {
        _scopeFactory = scopeFactory;
        _opt = opt.Value;
    }

    public void Initialize()
    {
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
    }

    private async Task OnCompleteCheckoutAsync(object e, CompleteCheckoutEventArgs args, CancellationToken ct)
    {
        if (!_opt.Enabled || !_opt.Orders.TrackingPlacedOrders)
            return;

        var orderInfo = args.OrderInfo;
        if (orderInfo == null)
            return;

        var klaviyoOrder = orderInfo.ToKlaviyoPlacedOrder(_opt.SiteBaseUrl);

        using var scope = _scopeFactory.CreateScope();
        var orderService = scope.ServiceProvider.GetRequiredService<IKlaviyoOrderService>();

        await orderService.TrackPlacedOrderAsync(klaviyoOrder, ct);
    }

    public void Terminate()
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
    }
}

internal sealed class KlaviyoEkomEventsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Components().Append<KlaviyoEkomEvents>();
    }
}
