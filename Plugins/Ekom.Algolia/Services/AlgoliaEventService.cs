using Ekom.Algolia.Models.Events;
using Ekom.Models;
using Microsoft.Extensions.Options;

namespace Ekom.Algolia.Services;

public interface IAlgoliaEventService
{
    Task TrackViewedProductAsync(IProduct product, CancellationToken ct = default);
    Task TrackAddedToCartAsync(IOrderInfo orderInfo, IOrderLine orderLine, CancellationToken ct = default);
    Task TrackStartedCheckoutAsync(IOrderInfo orderInfo, CancellationToken ct = default);
    Task TrackPurchaseAsync(IOrderInfo orderInfo, CancellationToken ct = default);
}

internal sealed class AlgoliaEventService : IAlgoliaEventService
{
    private const string ProductsEntity = "products";

    private readonly AlgoliaOptions _options;
    private readonly AlgoliaStoreResolver _storeResolver;
    private readonly IndexNameBuilder _indexNameBuilder;
    private readonly IAlgoliaUserTokenProvider _userTokenProvider;
    private readonly IAlgoliaInsightsClient _insightsClient;

    public AlgoliaEventService(
        IOptions<AlgoliaOptions> options,
        AlgoliaStoreResolver storeResolver,
        IndexNameBuilder indexNameBuilder,
        IAlgoliaUserTokenProvider userTokenProvider,
        IAlgoliaInsightsClient insightsClient)
    {
        _options = options.Value;
        _storeResolver = storeResolver;
        _indexNameBuilder = indexNameBuilder;
        _userTokenProvider = userTokenProvider;
        _insightsClient = insightsClient;
    }

    public Task TrackViewedProductAsync(IProduct product, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.Events.Enabled || !_options.Events.ViewedProduct)
            return Task.CompletedTask;

        var userToken = _userTokenProvider.GetUserToken();
        if (string.IsNullOrWhiteSpace(userToken))
            return Task.CompletedTask;

        var store = _storeResolver.Resolve(product.Store.Alias);
        var indexName = _indexNameBuilder.BuildPrimary(ProductsEntity, store);

        var evt = new AlgoliaInsightsEvent
        {
            EventType = "view",
            EventName = "Viewed Product",
            Index = indexName,
            UserToken = userToken,
            ObjectIds = new[] { product.Key.ToString() }
        };

        return _insightsClient.SendEventsAsync(new[] { evt }, ct);
    }

    public Task TrackAddedToCartAsync(IOrderInfo orderInfo, IOrderLine orderLine, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.Events.Enabled || !_options.Events.AddedToCart)
            return Task.CompletedTask;

        if (orderLine.Quantity <= 0)
            return Task.CompletedTask;

        var userToken = GetUserToken(orderInfo);
        if (string.IsNullOrWhiteSpace(userToken))
            return Task.CompletedTask;

        var store = _storeResolver.Resolve(orderInfo.StoreInfo.Alias);
        var indexName = _indexNameBuilder.Build(
            AlgoliaIndexKind.Primary,
            ProductsEntity,
            store,
            replica: null,
            localeOverride: orderInfo.StoreInfo.Culture,
            currencyOverride: orderInfo.StoreInfo.Currency.CurrencyValue);

        var evt = new AlgoliaInsightsEvent
        {
            EventType = "conversion",
            EventName = "Added To Cart",
            Index = indexName,
            UserToken = userToken,
            ObjectIds = new[] { orderLine.ProductKey.ToString() },
            Currency = orderInfo.StoreInfo.Currency.CurrencyValue,
            ObjectData = [CreateObjectData(orderInfo, orderLine)]
        };

        return _insightsClient.SendEventsAsync(new[] { evt }, ct);
    }

    public Task TrackStartedCheckoutAsync(IOrderInfo orderInfo, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.Events.Enabled || !_options.Events.StartedCheckout)
            return Task.CompletedTask;

        var userToken = GetUserToken(orderInfo);
        if (string.IsNullOrWhiteSpace(userToken))
            return Task.CompletedTask;

        var store = _storeResolver.Resolve(orderInfo.StoreInfo.Alias);
        var indexName = _indexNameBuilder.Build(
            AlgoliaIndexKind.Primary,
            ProductsEntity,
            store,
            replica: null,
            localeOverride: orderInfo.StoreInfo.Culture,
            currencyOverride: orderInfo.StoreInfo.Currency.CurrencyValue);

        var events = CreateOrderConversionEvents(orderInfo, userToken, indexName, "Started Checkout");
        if (events.Count == 0)
            return Task.CompletedTask;

        return _insightsClient.SendEventsAsync(events, ct);
    }

    public Task TrackPurchaseAsync(IOrderInfo orderInfo, CancellationToken ct = default)
    {
        if (!_options.Enabled || !_options.Events.Enabled || !_options.Events.Purchase)
            return Task.CompletedTask;

        var userToken = GetUserToken(orderInfo);
        if (string.IsNullOrWhiteSpace(userToken))
            return Task.CompletedTask;

        var store = _storeResolver.Resolve(orderInfo.StoreInfo.Alias);
        var indexName = _indexNameBuilder.Build(
            AlgoliaIndexKind.Primary,
            ProductsEntity,
            store,
            replica: null,
            localeOverride: orderInfo.StoreInfo.Culture,
            currencyOverride: orderInfo.StoreInfo.Currency.CurrencyValue);

        var events = CreateOrderConversionEvents(orderInfo, userToken, indexName, "Purchased");
        if (events.Count == 0)
            return Task.CompletedTask;

        return _insightsClient.SendEventsAsync(events, ct);
    }

    private string? GetUserToken(IOrderInfo orderInfo)
        => orderInfo.Tracking?.Algolia?.UserToken ?? _userTokenProvider.GetOrCreateUserToken();

    private static IReadOnlyList<AlgoliaInsightsEvent> CreateOrderConversionEvents(
        IOrderInfo orderInfo,
        string userToken,
        string indexName,
        string eventName)
        => orderInfo.OrderLines
            .Where(orderLine => orderLine.Quantity > 0)
            .Chunk(20)
            .Select(orderLines => new AlgoliaInsightsEvent
            {
                EventType = "conversion",
                EventName = eventName,
                Index = indexName,
                UserToken = userToken,
                ObjectIds = orderLines.Select(orderLine => orderLine.ProductKey.ToString()).ToList(),
                Currency = orderInfo.StoreInfo.Currency.CurrencyValue,
                ObjectData = orderLines.Select(orderLine => CreateObjectData(orderInfo, orderLine)).ToList()
            })
            .ToList();

    internal static IReadOnlyDictionary<string, object?> CreateObjectData(IOrderInfo orderInfo, IOrderLine orderLine)
    {
        var quantity = orderLine.Quantity;
        var amount = orderLine.Amount;
        var objectData = new Dictionary<string, object?>();
        var queryId = orderInfo.Tracking?.Algolia?.GetQueryId(orderLine.Key);
        if (!string.IsNullOrWhiteSpace(queryId))
            objectData["queryID"] = queryId;

        objectData["price"] = amount.Value / quantity;

        var discount = amount.DiscountAmount.Value / quantity;
        if (discount > 0)
            objectData["discount"] = discount;

        objectData["quantity"] = quantity;
        return objectData;
    }

}
