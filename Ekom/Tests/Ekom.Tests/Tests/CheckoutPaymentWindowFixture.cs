using Ekom.API;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Tracking;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using ReservationDatabase = Ekom.Tests.Tests.StockReservationTests.ReservationDatabase;

namespace Ekom.Tests.Tests;

// A saved basket with one line (quantity 1, 10 in stock), a store checkout that sends the
// customer to a payment provider, and payment attempts registered as in production.
// Shared by CheckoutRetryAfterTimeoutTests and CheckoutAbuseTests.
internal sealed class CheckoutPaymentWindowFixture : IDisposable
{
    private readonly ConfigurationScope _scope;
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly bool _legacyLineOverride;

    public ReservationDatabase Database { get; } = new();
    public Guid ProductKey { get; } = Guid.NewGuid();
    public Guid OrderId { get; } = Guid.NewGuid();
    public Guid LineKey { get; } = Guid.NewGuid();

    public CheckoutPaymentWindowFixture(bool reservations, bool legacyLineOverride = false)
    {
        _legacyLineOverride = legacyLineOverride;
        var store = new Mock<IStore>();
        store.SetupGet(x => x.Alias).Returns("main");
        store.SetupGet(x => x.Culture).Returns(new CultureInfoDto { Name = "en-US" });
        var stores = new Mock<IStoreService>();
        stores.Setup(x => x.GetStoreByAlias("main")).Returns(store.Object);
        stores.Setup(x => x.GetStoreFromCache()).Returns(store.Object);
        var storeInfo = new StoreInfo(Guid.NewGuid(), new CurrencyModel { CurrencyValue = "en-US" }, [], "en-US", "main", false, 0, false);
        store.SetupGet(x => x.Currency).Returns(storeInfo.Currency);
        var product = new Mock<IProduct>();
        product.SetupGet(x => x.Key).Returns(ProductKey);
        product.SetupGet(x => x.Stock).Returns(10);
        product.SetupGet(x => x.Url).Returns("");
        product.SetupGet(x => x.AllVariants).Returns(Array.Empty<IVariant>());
        product.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>
        {
            ["__Key"] = ProductKey.ToString(), ["title"] = "Product", ["sku"] = "product",
        });
        product.SetupGet(x => x.Prices).Returns(new List<IPrice> { new Price(10, storeInfo.Currency, 0, false) });
        var products = new Mock<IPerStoreIndexedCache<IProduct>>();
        var cached = product.Object;
        products.Setup(x => x.TryGetByKey("main", ProductKey, out cached)).Returns(true);
        var discounts = new Mock<IPerStoreCache<IDiscount>>();
        discounts.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IDiscount>> { ["main"] = new() });
        // Basket edits validate quantities against the stock cache.
        Database.StockCache[ProductKey] = new StockData { UniqueId = ProductKey.ToString(), Stock = 10, CreateDate = DateTime.UtcNow, UpdateDate = DateTime.UtcNow };
        _scope = new ConfigurationScope(overrides: new Dictionary<string, string?>
        {
            ["Ekom:Reservations:Enabled"] = reservations.ToString(),
        }, addServices: services =>
        {
            services.AddSingleton(sp => Database.NewCheckout(sp.GetRequiredService<Configuration>()));
            services.AddSingleton(Database.NewStockApi());
            services.AddSingleton(new API.Store(stores.Object, Mock.Of<ICacheRefreshService>()));
            services.AddSingleton(sp => new Discounts(sp.GetRequiredService<Configuration>(), NullLogger<Discounts>.Instance, discounts.Object, stores.Object));
            services.AddSingleton(sp => new Catalog(NullLogger<Catalog>.Instance, sp.GetRequiredService<Configuration>(),
                sp.GetRequiredService<IServiceScopeFactory>(), products.Object, Mock.Of<IPerStoreIndexedCache<ICategory>>(),
                Mock.Of<IPerStoreCache<IProductDiscount>>(), Mock.Of<IPerStoreIndexedCache<IVariant>>(), Mock.Of<IPerStoreIndexedCache<IVariantGroup>>(),
                stores.Object, new HttpContextAccessor(), Mock.Of<IProductFilterService>()));
            // Registered the same way as in production.
            services.AddSingleton(Database.Factory);
            services.AddSingleton<IStockReservationService>(Database.Service);
            services.AddSingleton(new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, Database.Factory));
            services.AddSingleton<IMemoryCache>(_cache);
            services.AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));
            services.AddSingleton<CheckoutPaymentAttemptService>();
            services.AddSingleton(sp => new OrderRepository(NullLogger<OrderRepository>.Instance, sp.GetRequiredService<Configuration>(),
                Database.Factory, _cache, sp.GetRequiredService<CheckoutPaymentAttemptService>()));
            services.AddSingleton(sp => new OrderService(sp.GetRequiredService<Configuration>(), sp.GetRequiredService<OrderRepository>(),
                null!, Mock.Of<IOrderActivityLogService>(), NullLogger<OrderService>.Instance, stores.Object, _cache,
                Mock.Of<IMemberService>(), null!, Mock.Of<IOrderTrackingService>(), sp.GetRequiredService<CheckoutPaymentAttemptService>()));
            services.AddSingleton(sp => new Order(sp.GetRequiredService<Configuration>(), NullLogger<Order>.Instance, null!, null!,
                sp.GetRequiredService<OrderService>(), null!, stores.Object, sp.GetRequiredService<OrderRepository>(), null!,
                Mock.Of<IOrderActivityLogService>(), new DiscountEvents()));
        });
        using var db = Database.Factory.GetDatabase();
        db.CreateTable<OrderData>();
        db.CreateTable<OrderActivityLog>(tableOptions: TableOptions.CreateIfNotExists);
        db.CreateTable<CheckoutPaymentOperationData>(tableOptions: TableOptions.CreateIfNotExists);
        db.CreateTable<CheckoutPaymentAttemptData>(tableOptions: TableOptions.CreateIfNotExists);
        db.Insert(new StockData { UniqueId = ProductKey.ToString(), Stock = 10, CreateDate = DateTime.UtcNow, UpdateDate = DateTime.UtcNow });
        var basket = new OrderInfo(new OrderData
        {
            UniqueId = OrderId, OrderStatusCol = "Incomplete",
            OrderInfo = JsonConvert.SerializeObject(new { StoreInfo = storeInfo, OrderLines = Array.Empty<object>(), CustomerInformation = new CustomerInfo() }),
        });
        basket.orderLines.Add(new OrderLine(product.Object, 1, LineKey, basket, []));
        db.Insert(new OrderData
        {
            UniqueId = OrderId, OrderStatusCol = "Incomplete", StoreAlias = "main", Currency = "USD",
            OrderInfo = JsonConvert.SerializeObject(basket, EkomJsonDotNet.Settings),
        });
    }

    private static OrderRepository Repository => Configuration.Resolver.GetRequiredService<OrderRepository>();
    private static CheckoutPaymentAttemptService Attempts => Configuration.Resolver.GetRequiredService<CheckoutPaymentAttemptService>();

    // Presses pay on the saved basket. Returns the status code, plus the conflict code for a 409.
    public async Task<string> PayAsync()
    {
        var basket = new OrderInfo((await Repository.GetOrderAsync(OrderId))!);
        var response = await new StoreCheckout(_scope.Instance, Database.Factory, Configuration.Resolver, _legacyLineOverride)
            .PayAsync(new PaymentRequest(), "en-US", basket);
        return $"{response.HttpStatusCode} {(response.ResponseBody as CheckoutStateError)?.Code}".Trim();
    }

    public async Task EditQuantityAsync(decimal quantity)
    {
        var basket = new OrderInfo((await Repository.GetOrderAsync(OrderId))!);
        await Configuration.Resolver.GetRequiredService<OrderService>().UpdateOrderLineQuantityAsync(LineKey, quantity, "main",
            new OrderSettings { OrderInfo = basket, FireEvents = false, FireOnOrderUpdatedEvent = false });
    }

    // The hold runs out and the expiry worker sweeps it; the attempt's own deadline passes with it.
    public async Task PaymentWindowPassesAsync()
    {
        var window = _scope.Instance.ReservationTimeout + TimeSpan.FromMinutes(1);
        var attemptId = await CurrentAttemptIdAsync();
        using var db = Database.Factory.GetDatabase();
        foreach (var id in await db.StockReservations.Where(x => x.OrderId == OrderId.ToString() && x.State == StockReservationState.Active)
            .Select(x => x.Id).ToListAsync())
            await Database.MakeDueAsync(id);
        await Database.Service.ExpireDueAsync(100);
        var attempt = await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == attemptId);
        await db.GetTable<CheckoutPaymentAttemptData>().Where(x => x.AttemptId == attemptId)
            .Set(x => x.CreatedUtc, attempt.CreatedUtc - window)
            .Set(x => x.SubmittedUtc, attempt.SubmittedUtc - window)
            .Set(x => x.ExpiresUtc, attempt.ExpiresUtc - window)
            .UpdateAsync();
    }

    // What /ekom/checkout/payment-return does for outcome=cancel.
    public async Task CancelReturnAsync()
        => await Attempts.ReleaseAsync(OrderId, await CurrentAttemptIdAsync(), "payment-cancel");

    // What the Ekom Payments success handler does for a verified payment. Returns whether the order
    // was completed; false means the payment was sent to reconciliation.
    public async Task<bool> ProviderConfirmsPaymentAsync(Guid? attemptId = null)
    {
        var attempt = attemptId ?? await CurrentAttemptIdAsync();
        var checkout = new CheckoutService(NullLogger<CheckoutService>.Instance, _scope.Instance, Repository, null!,
            Configuration.Resolver.GetRequiredService<OrderService>(), null!, Mock.Of<IOrderActivityLogService>(),
            Mock.Of<IMailService>(), Database.Service, Configuration.Resolver.GetRequiredService<CheckoutReservationService>());
        static void KeepStatus(object? sender, CompleteCheckoutEventArgs args) => args.UpdateOrderStatus = false;
        CheckoutEvents.CompleteCheckout += KeepStatus;
        try { return await Attempts.CompleteAsync(OrderId, attempt, () => checkout.CompleteAsync(OrderId)); }
        finally { CheckoutEvents.CompleteCheckout -= KeepStatus; }
    }

    public async Task<Guid> CurrentAttemptIdAsync()
    {
        using var db = Database.Factory.GetDatabase();
        return (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync(x => x.OrderId == OrderId)).ActiveAttemptId!.Value;
    }

    public async Task<CheckoutPaymentAttemptState> AttemptStateAsync(Guid attemptId)
    {
        using var db = Database.Factory.GetDatabase();
        return (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == attemptId)).State;
    }

    public Task<bool> StockCompletedAsync()
        => Configuration.Resolver.GetRequiredService<CheckoutReservationService>().IsCompletedAsync(OrderId, default);

    public Task<decimal> StockAsync() => Database.StockAsync(ProductKey);

    public void Dispose() { _scope.Dispose(); _cache.Dispose(); Database.Dispose(); }

    // The store's checkout. By default Ekom reserves stock itself; the legacy line override
    // reserves each line with ReserveStockAsync and AddHangfireJobsToOrderAsync and never calls base.
    private sealed class StoreCheckout : CheckoutControllerService
    {
        private readonly bool _legacyLineOverride;

        public StoreCheckout(Configuration config, DatabaseFactory database, IServiceProvider services, bool legacyLineOverride)
            : base(NullLogger.Instance, config, database, Mock.Of<IMemberService>(), new HttpContextAccessor(), null!,
                services.GetRequiredService<IServiceScopeFactory>(), services)
        {
            _legacyLineOverride = legacyLineOverride;
        }

        protected override Task<IOrderInfo> UpdateOrderDateAsync(Dictionary<string, string> collection, IOrderInfo order,
            Guid? paymentProviderKey = null, Guid? shippingProviderKey = null, CancellationToken ct = default) => Task.FromResult(order);
        protected override Task<CheckoutResponse?> ValidationAndOrderUpdatesAsync(PaymentRequest request, IOrderInfo order, CancellationToken ct)
            => Task.FromResult<CheckoutResponse?>(null);
        protected override async Task<CheckoutResponse?> ProcessOrderLinesAsync(PaymentRequest request, IOrderInfo order, ICollection<string> hangfireJobs, CancellationToken ct = default)
        {
            if (!_legacyLineOverride) return await base.ProcessOrderLinesAsync(request, order, hangfireJobs, ct);
            foreach (var line in order.OrderLines)
            {
                var id = await Stock.Instance.ReserveStockAsync(line.ProductKey, -line.Quantity, ct: ct);
                await Order.Instance.AddHangfireJobsToOrderAsync([id], order, ct: ct);
            }
            return null;
        }
        protected override Task<string> CreateOrderTitleAsync(PaymentRequest request, IOrderInfo order, IStore store, CancellationToken ct)
            => Task.FromResult("order");
        protected override Task<CheckoutResponse> ProcessPaymentAsync(PaymentRequest request, IOrderInfo order, string title, CancellationToken ct)
            => Task.FromResult(new CheckoutResponse { HttpStatusCode = 230 });
    }
}
