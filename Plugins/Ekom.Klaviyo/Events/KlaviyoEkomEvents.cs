using Ekom.Events;
using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Tracking;
using Ekom.Klaviyo.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;

namespace Ekom.Klaviyo.Events;

internal sealed class KlaviyoEkomEvents : IComponent
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly KlaviyoOptions _opt;

    public KlaviyoEkomEvents(IServiceScopeFactory scopeFactory, IOptions<KlaviyoOptions> opt, IHttpContextAccessor httpContextAccessor)
    {
        _scopeFactory = scopeFactory;
        _opt = opt.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public void Initialize()
    {
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
        OrderEvents.AddedOrderlineAsync += OnAddedOrderlineAsync;
        OrderEvents.CustomerEmailAddedAsync += OnCustomerEmailAddedAsync;
    }

    private async Task OnAddedOrderlineAsync(object arg1, AddedOrderlineEventArgs args, CancellationToken ct)
    {
        if (!_opt.Enabled || !_opt.Tracking.AddedToCart)
            return;

        var orderInfo = args.OrderInfo;
        if (orderInfo == null)
            return;

        var orderline = args.OrderLine;
        if (orderline == null)
            return;

        using var scope = _scopeFactory.CreateScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<IKlaviyoTrackingService>();

        var eventsArgs = new KlaviyoAddedToCartEvent
        {
            Currency = orderInfo.StoreInfo.Currency.ISOCurrencySymbol,
            Customer = orderInfo.ToKlaviyoProfile(_opt), 
            EventId = $"{orderInfo.KlaviyoUniqueId()}-{orderline.Key}",
            StoreAlias = orderInfo.StoreInfo.Alias,
            Sku = orderline.Variant?.SKU ?? orderline.Product.SKU,
            Quantity = orderline.Quantity,
            Price = orderline.Amount.WithVat.Value,
            PriceFormatted = orderline.Amount.WithVat.CurrencyString,
            ProductId= orderline.Product.Key.ToString(), 
            ProductName = orderline.Product.Title,
            ProductUrl = string.IsNullOrWhiteSpace(orderline.Product?.Url) ? null : UrlBuilder.Combine(_opt.SiteBaseUrl, orderline.Product.Url),
            OccurredAt = DateTimeOffset.UtcNow
        };

        if (_httpContextAccessor.HttpContext != null)
        {
            var userName = _httpContextAccessor.HttpContext.User.Identity?.Name;

            if (!string.IsNullOrEmpty(userName))
            {
                if (string.IsNullOrEmpty(eventsArgs.Customer.ExternalId))
                {
                    eventsArgs.Customer.ExternalId = userName;
                }

                if (string.IsNullOrEmpty(eventsArgs.Customer.Email) && userName.Contains("@", StringComparison.OrdinalIgnoreCase))
                {
                    eventsArgs.Customer.Email = userName;
                }

            }
        }

        await trackingService.TrackAddedToCartAsync(eventsArgs, ct);
    }

    private async Task OnCustomerEmailAddedAsync(object arg1, CustomerEmailAddedEventArgs args, CancellationToken ct)
    {

        if (!_opt.Enabled || !_opt.Tracking.StartedCheckout)
            return;

        var orderInfo = args.OrderInfo;
        if (orderInfo == null)
            return;

        using var scope = _scopeFactory.CreateScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<IKlaviyoTrackingService>();

        var storeOptions = _opt.Stores.FirstOrDefault(x => x.Alias.InvariantEquals(orderInfo.StoreInfo.Alias));

        var eventArgs = new KlaviyoStartedCheckoutEvent
        {
            CartId = orderInfo.KlaviyoUniqueId(),
            Customer = orderInfo.ToKlaviyoProfile(_opt), 
            Items = orderInfo.OrderLines.Select(ol => ol.ToKlaviyoOrderLine(_opt)).ToList(),
            CheckoutUrl = storeOptions?.CheckoutUrl,
            Value = orderInfo.ChargedAmount.Value,
            ValueFormatted = orderInfo.ChargedAmount.CurrencyString,
            Currency = orderInfo.StoreInfo.Currency.ISOCurrencySymbol,
            EventId= orderInfo.KlaviyoUniqueId(), // Using the same unique ID for the event, as Klaviyo can deduplicate events with the same ID, preventing duplicates if the email is changed multiple times during checkout
            StoreAlias = orderInfo.StoreInfo.Alias, 
            OccurredAt = DateTimeOffset.UtcNow
        };

        await trackingService.TrackStartedCheckoutAsync(eventArgs, ct);
    }

    private async Task OnCompleteCheckoutAsync(object e, CompleteCheckoutEventArgs args, CancellationToken ct)
    {
        if (!_opt.Enabled || !_opt.Orders.TrackingPlacedOrders)
            return;

        var orderInfo = args.OrderInfo;
        if (orderInfo == null)
            return;

        var klaviyoOrder = orderInfo.ToKlaviyoPlacedOrder(_opt, DateTimeOffset.UtcNow);

        using var scope = _scopeFactory.CreateScope();
        var orderService = scope.ServiceProvider.GetRequiredService<IKlaviyoOrderService>();

        await orderService.TrackPlacedOrderAsync(klaviyoOrder, ct);
    }

    public void Terminate()
    {
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
        OrderEvents.AddedOrderlineAsync -= OnAddedOrderlineAsync; 
        OrderEvents.CustomerEmailAddedAsync -= OnCustomerEmailAddedAsync;
    }
}

internal sealed class KlaviyoEkomEventsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Components().Append<KlaviyoEkomEvents>();
    }
}
