using Ekom.Events;
using Ekom.Algolia.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;

namespace Ekom.Algolia.Events;

internal sealed class AlgoliaEkomEvents : IComponent
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AlgoliaOptions _options;

    public AlgoliaEkomEvents(IServiceScopeFactory scopeFactory, IOptions<AlgoliaOptions> options)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
    }

    public void Initialize()
    {
        //CatalogEvents.BeforeReturnProductAsync += OnBeforeReturnProductAsync;
        OrderEvents.AddedOrderlineAsync += OnAddedOrderlineAsync;
        OrderEvents.RemovedOrderlineAsync += OnRemovedOrderlineAsync;
        OrderEvents.CustomerEmailAddedAsync += OnCustomerEmailAddedAsync;
        CheckoutEvents.CompleteCheckoutAsync += OnCompleteCheckoutAsync;
    }

    public void Terminate()
    {
        //CatalogEvents.BeforeReturnProductAsync -= OnBeforeReturnProductAsync;
        OrderEvents.AddedOrderlineAsync -= OnAddedOrderlineAsync;
        OrderEvents.RemovedOrderlineAsync -= OnRemovedOrderlineAsync;
        OrderEvents.CustomerEmailAddedAsync -= OnCustomerEmailAddedAsync;
        CheckoutEvents.CompleteCheckoutAsync -= OnCompleteCheckoutAsync;
    }

    //private async ValueTask OnBeforeReturnProductAsync(ProductEventArgs args, CancellationToken ct)
    //{
    //    if (!_options.Enabled || !_options.Events.Enabled || !_options.Events.ViewedProduct)
    //        return;

    //    if (args.Product == null)
    //        return;

    //    using var scope = _scopeFactory.CreateScope();
    //    var service = scope.ServiceProvider.GetRequiredService<IAlgoliaEventService>();

    //    await service.TrackViewedProductAsync(args.Product, ct).ConfigureAwait(false);
    //}

    private async Task OnAddedOrderlineAsync(object sender, AddedOrderlineEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled)
            return;

        using var scope = _scopeFactory.CreateScope();
        var userTokenProvider = scope.ServiceProvider.GetRequiredService<IAlgoliaUserTokenProvider>();
        var tracking = args.OrderInfo.Tracking ??= new();
        var algoliaTracking = tracking.Algolia ??= new();
        algoliaTracking.UserToken ??= userTokenProvider.GetOrCreateUserToken();
        algoliaTracking.AddLine(args.OrderLine.Key, args.Settings.AlgoliaQueryId);

        if (!_options.Events.Enabled || !_options.Events.AddedToCart)
            return;

        var service = scope.ServiceProvider.GetRequiredService<IAlgoliaEventService>();

        await service.TrackAddedToCartAsync(args.OrderInfo, args.OrderLine, ct).ConfigureAwait(false);
    }

    private Task OnRemovedOrderlineAsync(object sender, RemovedOrderlineEventArgs args, CancellationToken ct)
    {
        if (_options.Enabled)
            args.OrderInfo.Tracking?.Algolia?.RemoveLine(args.OrderLine.Key);

        return Task.CompletedTask;
    }

    private async Task OnCustomerEmailAddedAsync(object sender, CustomerEmailAddedEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Events.Enabled || !_options.Events.StartedCheckout)
            return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAlgoliaEventService>();

        await service.TrackStartedCheckoutAsync(args.OrderInfo, ct).ConfigureAwait(false);
    }

    private async Task OnCompleteCheckoutAsync(object sender, CompleteCheckoutEventArgs args, CancellationToken ct)
    {
        if (!_options.Enabled || !_options.Events.Enabled || !_options.Events.Purchase)
            return;

        using var scope = _scopeFactory.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IAlgoliaEventService>();

        await service.TrackPurchaseAsync(args.OrderInfo, ct).ConfigureAwait(false);
    }
}

internal sealed class AlgoliaEkomEventsComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.Components().Append<AlgoliaEkomEvents>();
    }
}
