using Ekom.Events;
using Ekom.Klaviyo.Helpers;
using Ekom.Klaviyo.Mappers;
using Ekom.Klaviyo.Models.Tracking;
using Ekom.Klaviyo.Services;
using Ekom.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Security.Cryptography;
using System.Text;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

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
        OrderEvents.UpdatedOrderlineAsync += OnUpdatedOrderlineAsync;
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

        var eventArgs = CreateStartedCheckoutEvent(orderInfo, useCartFingerprint: false);

        await trackingService.TrackStartedCheckoutAsync(eventArgs, ct);
    }

    private async Task OnUpdatedOrderlineAsync(object arg1, UpdatedOrderlineEventArgs args, CancellationToken ct)
    {
        if (!_opt.Enabled || !_opt.Tracking.StartedCheckout)
            return;

        var orderInfo = args.OrderInfo;
        if (orderInfo == null)
            return;

        using var scope = _scopeFactory.CreateScope();
        var trackingService = scope.ServiceProvider.GetRequiredService<IKlaviyoTrackingService>();

        if (orderInfo.OrderLines.Count == 0)
        {
            var emptiedEventArgs = CreateCartEmptiedEvent(orderInfo);
            if (!emptiedEventArgs.Customer.HasIdentifier)
                return;

            await trackingService.TrackCartEmptiedAsync(emptiedEventArgs, ct);
            return;
        }

        var eventArgs = CreateStartedCheckoutEvent(orderInfo, useCartFingerprint: true);
        if (!eventArgs.Customer.HasIdentifier)
            return;

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
        OrderEvents.UpdatedOrderlineAsync -= OnUpdatedOrderlineAsync;
        OrderEvents.CustomerEmailAddedAsync -= OnCustomerEmailAddedAsync;
    }

    private KlaviyoStartedCheckoutEvent CreateStartedCheckoutEvent(IOrderInfo orderInfo, bool useCartFingerprint)
    {
        var storeOptions = _opt.Stores.FirstOrDefault(x => string.Equals(x.Alias, orderInfo.StoreInfo.Alias, StringComparison.OrdinalIgnoreCase));

        return new KlaviyoStartedCheckoutEvent
        {
            OrderId = orderInfo.KlaviyoUniqueId(),
            OrderNumber = orderInfo.OrderNumber,
            Customer = orderInfo.ToKlaviyoProfile(_opt),
            Items = orderInfo.OrderLines.Select(ol => ol.ToKlaviyoOrderLine(_opt)).ToList(),
            CheckoutUrl = storeOptions?.CheckoutUrl,
            Value = orderInfo.ChargedAmount.Value,
            ValueFormatted = orderInfo.ChargedAmount.CurrencyString,
            Currency = orderInfo.StoreInfo.Currency.ISOCurrencySymbol,
            EventId = BuildStartedCheckoutEventId(orderInfo, useCartFingerprint),
            StoreAlias = orderInfo.StoreInfo.Alias,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private static string BuildStartedCheckoutEventId(IOrderInfo orderInfo, bool useCartFingerprint)
    {
        if (!useCartFingerprint)
            return orderInfo.KlaviyoUniqueId();

        return $"{orderInfo.KlaviyoUniqueId()}:{BuildCartFingerprint(orderInfo)}";
    }

    private KlaviyoCartEmptiedEvent CreateCartEmptiedEvent(IOrderInfo orderInfo)
    {
        return new KlaviyoCartEmptiedEvent
        {
            OrderId = orderInfo.KlaviyoUniqueId(),
            OrderNumber = orderInfo.OrderNumber,
            Customer = orderInfo.ToKlaviyoProfile(_opt),
            Currency = orderInfo.StoreInfo.Currency.ISOCurrencySymbol,
            EventId = $"{orderInfo.KlaviyoUniqueId()}:empty",
            StoreAlias = orderInfo.StoreInfo.Alias,
            OccurredAt = DateTimeOffset.UtcNow
        };
    }

    private static string BuildCartFingerprint(IOrderInfo orderInfo)
    {
        var builder = new StringBuilder();

        builder.Append(orderInfo.StoreInfo.Alias)
            .Append('|')
            .Append(orderInfo.KlaviyoUniqueId())
            .Append('|')
            .Append(orderInfo.ChargedAmount.Value)
            .Append('|')
            .Append(orderInfo.StoreInfo.Currency.ISOCurrencySymbol);

        foreach (var line in orderInfo.OrderLines.OrderBy(x => x.Key))
        {
            builder.Append('|')
                .Append(line.Key)
                .Append('|')
                .Append(line.ProductKey)
                .Append('|')
                .Append(line.Variant?.Key)
                .Append('|')
                .Append(line.Product?.SKU)
                .Append('|')
                .Append(line.Variant?.SKU)
                .Append('|')
                .Append(line.Quantity)
                .Append('|')
                .Append(line.Amount.WithVat.Value);
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString()));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

internal sealed class KlaviyoEkomEventsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Components().Append<KlaviyoEkomEvents>();
    }
}
