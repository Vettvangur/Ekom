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
using Xunit;
using ReservationDatabase = Ekom.Tests.Tests.StockReservationTests.ReservationDatabase;

namespace Ekom.Tests.Tests;

// A customer leaves the payment page and comes back after the payment window has passed.
// Each test runs the real flow with payment attempts registered, as in production:
// pay, the window passes, pay again, then the provider's verified success.
[Collection("Reservations")]
public sealed class CheckoutRetryAfterTimeoutTests
{
    [Fact]
    public async Task PayAgainAfterHoldExpiredCompletesTheNewPayment()
    {
        using var f = new Fixture(reservations: true);
        Assert.Equal("230", Outcome(await f.PayAsync()));
        await f.PaymentWindowPassesAsync();
        Assert.Equal(10, await f.StockAsync());

        Assert.Equal("230", Outcome(await f.PayAsync()));
        Assert.Equal(9, await f.StockAsync());
        Assert.True(await f.PaymentSucceedsAsync());
        Assert.True(await f.StockCompletedAsync());
        Assert.Equal(9, await f.StockAsync());
    }

    [Fact]
    public async Task CancelAfterHoldExpiredThenPayAgainCompletesTheNewPayment()
    {
        using var f = new Fixture(reservations: true);
        Assert.Equal("230", Outcome(await f.PayAsync()));
        await f.PaymentWindowPassesAsync();
        await f.CancelReturnAsync();

        Assert.Equal("230", Outcome(await f.PayAsync()));
        Assert.True(await f.PaymentSucceedsAsync());
        Assert.True(await f.StockCompletedAsync());
        Assert.Equal(9, await f.StockAsync());
    }

    [Fact]
    public async Task LegacyLineOverridePayAgainAfterHoldExpiredCompletesTheNewPayment()
    {
        using var f = new Fixture(reservations: false, legacyLineOverride: true);
        Assert.Equal("230", Outcome(await f.PayAsync()));
        await f.PaymentWindowPassesAsync();

        Assert.Equal("230", Outcome(await f.PayAsync()));
        Assert.Equal(9, await f.StockAsync());
        Assert.True(await f.PaymentSucceedsAsync());
        Assert.True(await f.StockCompletedAsync());
        Assert.Equal(9, await f.StockAsync());
    }

    [Fact]
    public async Task PayAgainAfterAbandonedPaymentWithoutHoldsWorksOnceTheWindowHasPassed()
    {
        // Default settings: reservations are disabled, so the attempt has no holds.
        using var f = new Fixture(reservations: false);
        Assert.Equal("230", Outcome(await f.PayAsync()));
        await f.PaymentWindowPassesAsync();

        Assert.Equal("230", Outcome(await f.PayAsync()));
        Assert.True(await f.PaymentSucceedsAsync());
        Assert.True(await f.StockCompletedAsync());
        Assert.Equal(9, await f.StockAsync());
    }

    private static string Outcome(CheckoutResponse response)
        => $"{response.HttpStatusCode} {(response.ResponseBody as CheckoutStateError)?.Code}".Trim();

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _scope;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly bool _legacyLineOverride;

        public ReservationDatabase Database { get; } = new();
        public Guid ProductKey { get; } = Guid.NewGuid();
        public Guid OrderId { get; } = Guid.NewGuid();

        public Fixture(bool reservations, bool legacyLineOverride = false)
        {
            _legacyLineOverride = legacyLineOverride;
            var store = new Mock<IStore>();
            store.SetupGet(x => x.Alias).Returns("main");
            store.SetupGet(x => x.Culture).Returns(new CultureInfoDto { Name = "en-US" });
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("main")).Returns(store.Object);
            stores.Setup(x => x.GetStoreFromCache()).Returns(store.Object);
            var storeInfo = new StoreInfo(Guid.NewGuid(), new CurrencyModel { CurrencyValue = "en-US" }, [], "en-US", "main", false, 0, false);
            var product = new Mock<IProduct>();
            product.SetupGet(x => x.Key).Returns(ProductKey);
            product.SetupGet(x => x.Stock).Returns(10);
            product.SetupGet(x => x.Properties).Returns(new Dictionary<string, string> { ["__Key"] = ProductKey.ToString() });
            product.SetupGet(x => x.Prices).Returns(new List<IPrice> { new Price(10, storeInfo.Currency, 0, false) });
            var products = new Mock<IPerStoreIndexedCache<IProduct>>();
            var cached = product.Object;
            products.Setup(x => x.TryGetByKey("main", ProductKey, out cached)).Returns(true);
            var discounts = new Mock<IPerStoreCache<IDiscount>>();
            discounts.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IDiscount>> { ["main"] = new() });
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
            var cart = new OrderInfo(new OrderData
            {
                UniqueId = OrderId, OrderStatusCol = "Incomplete",
                OrderInfo = JsonConvert.SerializeObject(new { StoreInfo = storeInfo, OrderLines = Array.Empty<object>(), CustomerInformation = new CustomerInfo() }),
            });
            cart.orderLines.Add(new OrderLine(product.Object, 1, Guid.NewGuid(), cart, []));
            db.Insert(new OrderData
            {
                UniqueId = OrderId, OrderStatusCol = "Incomplete",
                OrderInfo = JsonConvert.SerializeObject(cart, EkomJsonDotNet.Settings),
            });
        }

        public async Task<CheckoutResponse> PayAsync()
        {
            var cart = new OrderInfo((await Repository.GetOrderAsync(OrderId))!);
            return await new StoreCheckout(_scope.Instance, Database.Factory, Configuration.Resolver, _legacyLineOverride)
                .PayAsync(new PaymentRequest(), "en-US", cart);
        }

        // The hold runs out and the expiry worker sweeps it; the attempt's own deadline passes with it.
        public async Task PaymentWindowPassesAsync()
        {
            var window = _scope.Instance.ReservationTimeout + TimeSpan.FromMinutes(1);
            using var db = Database.Factory.GetDatabase();
            foreach (var id in await db.StockReservations.Where(x => x.OrderId == OrderId.ToString() && x.State == StockReservationState.Active)
                .Select(x => x.Id).ToListAsync())
                await Database.MakeDueAsync(id);
            await Database.Service.ExpireDueAsync(100);
            var attempt = await CurrentAttemptAsync(db);
            await db.GetTable<CheckoutPaymentAttemptData>().Where(x => x.AttemptId == attempt.AttemptId)
                .Set(x => x.CreatedUtc, attempt.CreatedUtc - window)
                .Set(x => x.SubmittedUtc, attempt.SubmittedUtc - window)
                .Set(x => x.ExpiresUtc, attempt.ExpiresUtc - window)
                .UpdateAsync();
        }

        // What /ekom/checkout/payment-return does for outcome=cancel.
        public async Task CancelReturnAsync()
        {
            using var db = Database.Factory.GetDatabase();
            var attempt = await CurrentAttemptAsync(db);
            await Attempts.ReleaseAsync(OrderId, attempt.AttemptId, "payment-cancel");
        }

        // What the Ekom Payments success handler does for a verified payment of the current attempt.
        public async Task<bool> PaymentSucceedsAsync()
        {
            Guid attemptId;
            using (var db = Database.Factory.GetDatabase())
                attemptId = (await CurrentAttemptAsync(db)).AttemptId;
            var checkout = new CheckoutService(NullLogger<CheckoutService>.Instance, _scope.Instance, Repository, null!,
                Configuration.Resolver.GetRequiredService<OrderService>(), null!, Mock.Of<IOrderActivityLogService>(),
                Mock.Of<IMailService>(), Database.Service, Configuration.Resolver.GetRequiredService<CheckoutReservationService>());
            static void KeepStatus(object? sender, CompleteCheckoutEventArgs args) => args.UpdateOrderStatus = false;
            CheckoutEvents.CompleteCheckout += KeepStatus;
            try { return await Attempts.CompleteAsync(OrderId, attemptId, () => checkout.CompleteAsync(OrderId)); }
            finally { CheckoutEvents.CompleteCheckout -= KeepStatus; }
        }

        public Task<bool> StockCompletedAsync()
            => Configuration.Resolver.GetRequiredService<CheckoutReservationService>().IsCompletedAsync(OrderId, default);

        public Task<decimal> StockAsync() => Database.StockAsync(ProductKey);

        private static OrderRepository Repository => Configuration.Resolver.GetRequiredService<OrderRepository>();
        private static CheckoutPaymentAttemptService Attempts => Configuration.Resolver.GetRequiredService<CheckoutPaymentAttemptService>();

        private async Task<CheckoutPaymentAttemptData> CurrentAttemptAsync(DbContext db)
        {
            var operation = await db.GetTable<CheckoutPaymentOperationData>().SingleAsync(x => x.OrderId == OrderId);
            return await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == operation.ActiveAttemptId);
        }

        public void Dispose() { _scope.Dispose(); _cache.Dispose(); Database.Dispose(); }
    }

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
