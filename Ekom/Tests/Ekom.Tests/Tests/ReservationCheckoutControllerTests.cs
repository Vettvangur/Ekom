using Ekom.API;
using Ekom.Cache;
using Ekom.Events;
using Ekom.Exceptions;
using Ekom.Interfaces;
using Ekom.Repositories;
using Ekom.Models;
using Ekom.Services;
using Ekom.Tests.Objects;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using System.Text.Json;
using Xunit;
using ReservationDatabase = Ekom.Tests.Tests.StockReservationTests.ReservationDatabase;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class ReservationCheckoutControllerTests
{
    [Fact]
    public async Task AnotherNodeCannotAdoptHoldsUntilFailingOwnerFinishesCompensation()
    {
        using var f = new Fixture(true);
        await f.SeedAsync();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var first = f.Controller("coupon");
        first.BeforeCoupons = async () => { entered.SetResult(); await resume.Task; };
        var firstPay = first.PayAsync(new PaymentRequest(), "en-US", f.Order.Object);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.Equal(7, await f.Database.StockAsync(f.Product.Key));
            var second = f.Controller();
            await Assert.ThrowsAsync<StockException>(() => second.PayAsync(new PaymentRequest(), "en-US", f.Order.Object));
            Assert.False(second.PaymentCalled);
            Assert.False(second.Saved);
        }
        finally { resume.SetResult(); }
        Assert.Equal(400, (await firstPay).HttpStatusCode);
        Assert.Equal(10, await f.Database.StockAsync(f.Product.Key));
        await f.Controller().PayAsync(new PaymentRequest(), "en-US", f.Order.Object);
        Assert.Equal(7, await f.Database.StockAsync(f.Product.Key));
    }

    [Fact]
    public async Task StaleRetryFailureCannotReleasePreviouslySubmittedRecoveredHolds()
    {
        using var f = new Fixture(true);
        await f.SeedAsync();
        await f.Controller().PayAsync(new PaymentRequest(), "en-US", f.Order.Object);
        var submitted = f.Ids.Single();
        f.Ids.Clear(); // Another node fetched this order before the first node saved it.
        Assert.Equal(400, (await f.Controller("coupon").PayAsync(new PaymentRequest(), "en-US", f.Order.Object)).HttpStatusCode);
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync(x => x.Id == submitted)).State);
        Assert.Equal(7, await f.Database.StockAsync(f.Product.Key));
        await f.Controller().PayAsync(new PaymentRequest(), "en-US", f.Order.Object);
        Assert.Equal(submitted, f.Ids.Single());
    }

    [Fact]
    public async Task CommittedSaveWithCacheAndSubscriberFailuresKeepsHoldsAndAllowsRetry()
    {
        using var f = new Fixture(true);
        await f.SeedAsync();
        using var db = f.Database.Factory.GetDatabase();
        db.CreateTable<OrderData>();
        var data = new OrderData { UniqueId = f.Order.Object.UniqueId, OrderStatusCol = "Incomplete", OrderInfo = "{}" };
        var cache = new Mock<IMemoryCache>();
        cache.Setup(x => x.Remove(It.IsAny<object>())).Throws(new InvalidOperationException("cache failure after commit"));
        var repository = new OrderRepository(NullLogger<OrderRepository>.Instance, Configuration.Instance, f.Database.Factory, cache.Object);
        await repository.InsertOrderAsync(data);
        var notified = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var resume = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var laterNotificationRan = false;
        var first = f.Controller();
        first.PersistAction = async ids =>
        {
            data.OrderInfo = JsonSerializer.Serialize(ids);
            await repository.UpdateOrderAsync(data, reservationPersistence: true, default);
            await OrderPersistenceNotifications.RunAsync(data.UniqueId, NullLogger.Instance,
                async () =>
                {
                    notified.SetResult();
                    await resume.Task;
                    throw new InvalidOperationException("post-save subscriber failed");
                },
                () => { laterNotificationRan = true; return Task.CompletedTask; });
        };
        var payment = first.PayAsync(new PaymentRequest(), "en-US", f.Order.Object);
        await notified.Task.WaitAsync(TimeSpan.FromSeconds(5));
        try
        {
            Assert.NotEqual("{}", (await repository.GetOrderAsync(data.UniqueId))!.OrderInfo);
            await Assert.ThrowsAsync<StockException>(() => f.Controller().PayAsync(new PaymentRequest(), "en-US", f.Order.Object));
        }
        finally { resume.SetResult(); }
        Assert.Equal(230, (await payment).HttpStatusCode);
        Assert.True(laterNotificationRan);
        var id = f.Ids.Single();
        Assert.Equal(new[] { id }, JsonSerializer.Deserialize<string[]>((await repository.GetOrderAsync(data.UniqueId))!.OrderInfo));
        f.Ids.Clear();
        await f.Controller().PayAsync(new PaymentRequest(), "en-US", f.Order.Object);
        Assert.Equal(id, f.Ids.Single());
        Assert.Equal(7, await f.Database.StockAsync(f.Product.Key));
        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync()).State);
    }

    [Fact]
    public async Task InterruptedOwnerHasNoUnsafeTimedTakeover()
    {
        using var f = new Fixture(true);
        var owner = await f.Database.NewCheckout().AcquirePreparationAsync(f.Order.Object.UniqueId, default);
        await Assert.ThrowsAsync<StockException>(() => f.Database.NewCheckout().AcquirePreparationAsync(f.Order.Object.UniqueId, default));
        await Assert.ThrowsAsync<System.Data.DBConcurrencyException>(() => f.Database.NewCheckout().ReleasePreparationAsync(
            new CheckoutPreparationData { OrderId = owner.OrderId, Owner = Guid.NewGuid().ToString() }, default));
        await f.Database.NewCheckout().ReleasePreparationAsync(owner, default);
        var next = await f.Database.NewCheckout().AcquirePreparationAsync(owner.OrderId, default);
        Assert.NotEqual(owner.Owner, next.Owner);
        await f.Database.NewCheckout().ReleasePreparationAsync(next, default);
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PayPreparesAndPersistsBeforePaymentInBothModes(bool enabled)
    {
        using var f = new Fixture(enabled);
        await f.SeedAsync();
        var controller = f.Controller();
        var response = await controller.PayAsync(new PaymentRequest(), "en-US", f.Order.Object);
        Assert.Equal(230, response.HttpStatusCode);
        Assert.True(controller.Saved);
        Assert.Equal(enabled ? 1 : 0, controller.IdsAtPayment.Count);
        Assert.Equal(enabled ? 7 : 10, await f.Database.StockAsync(f.Product.Key));
        if (enabled)
        {
            var original = controller.IdsAtPayment.Single();
            await controller.PayAsync(new PaymentRequest(), "en-US", f.Order.Object);
            Assert.Equal(original, controller.IdsAtPayment.Single());
            Assert.Equal(7, await f.Database.StockAsync(f.Product.Key));
        }
    }

    [Theory]
    [InlineData("prepare")]
    [InlineData("validation")]
    [InlineData("coupon")]
    [InlineData("coupon-throw")]
    [InlineData("title")]
    [InlineData("cancel")]
    public async Task PreparationErrorsStopPaymentAndCompensate(string failure)
    {
        using var f = new Fixture(true);
        await f.SeedAsync();
        var controller = f.Controller(failure);
        if (failure == "cancel")
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => controller.PayAsync(new PaymentRequest(), "en-US", f.Order.Object));
        else if (failure is "coupon-throw" or "title")
            await Assert.ThrowsAsync<InvalidOperationException>(() => controller.PayAsync(new PaymentRequest(), "en-US", f.Order.Object));
        else
            Assert.Equal(400, (await controller.PayAsync(new PaymentRequest(), "en-US", f.Order.Object)).HttpStatusCode);
        Assert.False(controller.PaymentCalled);
        Assert.Equal(10, await f.Database.StockAsync(f.Product.Key));
        using var db = f.Database.Factory.GetDatabase();
        Assert.DoesNotContain(await db.StockReservations.ToListAsync(), x => x.State == StockReservationState.Active);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UncertainPersistenceFailureRetainsOwnershipAndHolds(bool sqlCommitted)
    {
        using var f = new Fixture(true);
        await f.SeedAsync();
        var first = f.Controller(sqlCommitted ? null : "save");
        using var db = f.Database.Factory.GetDatabase();
        db.CreateTable<OrderData>();
        if (sqlCommitted)
            first.PersistAction = async ids =>
            {
                await db.InsertAsync(new OrderData
                {
                    UniqueId = f.Order.Object.UniqueId, OrderStatusCol = "Incomplete", OrderInfo = JsonSerializer.Serialize(ids),
                });
                throw new InvalidOperationException("commit acknowledgement lost");
            };
        await Assert.ThrowsAsync<InvalidOperationException>(() => first.PayAsync(new PaymentRequest(), "en-US", f.Order.Object));
        Assert.False(first.PaymentCalled);
        Assert.Equal(7, await f.Database.StockAsync(f.Product.Key));
        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync()).State);
        Assert.Equal(sqlCommitted ? 1 : 0, await db.OrderData.CountAsync());
        Assert.NotNull((await db.GetTable<CheckoutPreparationData>().SingleAsync()).Owner);
        await Assert.ThrowsAsync<StockException>(() => f.Controller().PayAsync(new PaymentRequest(), "en-US", f.Order.Object));
    }

    [Fact]
    public async Task ProductBufferRejectsStaleCatalogAvailabilityBeforePayment()
    {
        using var f = new Fixture(true);
        // Catalog reports 10, but SQL has only four units, two of which are buffered.
        await f.Database.SeedAsync(4, key: f.Product.Key);
        var controller = f.Controller();
        var response = await controller.PayAsync(new PaymentRequest(), "en-US", f.Order.Object);
        Assert.Equal(530, response.HttpStatusCode);
        Assert.False(controller.PaymentCalled);
        Assert.Empty(f.Ids);
        Assert.Equal(4, await f.Database.StockAsync(f.Product.Key));
    }

    [Fact]
    public async Task ProviderExceptionRetainsPersistedHoldsForUncertainPaymentOutcome()
    {
        using var f = new Fixture(true);
        await f.SeedAsync();
        var controller = f.Controller("payment");
        await Assert.ThrowsAsync<InvalidOperationException>(() => controller.PayAsync(new PaymentRequest(), "en-US", f.Order.Object));
        Assert.Single(f.Ids);
        Assert.Equal(7, await f.Database.StockAsync(f.Product.Key));
        using var db = f.Database.Factory.GetDatabase();
        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync()).State);
    }

    [Fact]
    public async Task ProcessingEventCanDisableInventoryReservation()
    {
        using var f = new Fixture(true);
        await f.SeedAsync();
        void Disable(object? sender, ProcessingEventArgs args) => args.StockValidation = false;
        CheckoutEvents.Processing += Disable;
        try
        {
            await f.Controller().PayAsync(new PaymentRequest(), "en-US", f.Order.Object);
            Assert.Empty(f.Ids);
            Assert.Equal(10, await f.Database.StockAsync(f.Product.Key));
        }
        finally { CheckoutEvents.Processing -= Disable; }
    }

    [Fact]
    public async Task CatalogPolicyUsesVariantBufferAndSkipsBackordersButKeepsLineDiscounts()
    {
        using var f = new Fixture(true);
        var variant = new Mock<IVariant>();
        var variantKey = Guid.NewGuid();
        variant.SetupGet(x => x.Key).Returns(variantKey);
        variant.SetupGet(x => x.StockBuffer).Returns(4);
        variant.SetupGet(x => x.Prices).Returns(new List<IPrice>());
        variant.SetupGet(x => x.Properties).Returns(new Dictionary<string, string> { ["__Key"] = variantKey.ToString() });
        var group = new Mock<IVariantGroup>();
        group.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>());
        variant.SetupGet(x => x.VariantGroup).Returns(group.Object);
        f.Variants[variantKey] = variant.Object;
        var ordered = new OrderedProduct(f.Product, variant.Object, f.StoreInfo);
        var variantLine = Line(f.Product.Key, ordered, 3);
        var backorder = new OrderedProduct(f.Product, null, f.StoreInfo)
        {
            Properties = new Dictionary<string, string> { ["enableBackorder"] = "true" },
        };
        var backorderLine = Line(f.Product.Key, backorder, 50);
        var discount = new OrderedDiscount(Guid.NewGuid(), "discount", false, 1, default, [], [], new Constraints(), true, false);
        backorderLine.SetupGet(x => x.Discount).Returns(discount);
        backorderLine.SetupGet(x => x.Coupon).Returns("ExactCoupon");
        f.Order.SetupGet(x => x.OrderLines).Returns(new[] { variantLine.Object, backorderLine.Object });
        var requests = await f.Checkout.GetRequirementsAsync(f.Order.Object, default);
        var inventory = Assert.Single(requests, x => !x.IsDiscount);
        Assert.Equal(variantKey, inventory.Key);
        Assert.Equal(4, inventory.MinimumRemainingStock);
        Assert.Equal(3, inventory.Quantity);
        Assert.Equal(2, requests.Count(x => x.IsDiscount));
        Assert.All(requests.Where(x => x.IsDiscount), x => Assert.Equal(1, x.Quantity));
        await f.Database.SeedAsync(7, key: variantKey);
        await f.Database.SeedDiscountAsync(discount.Key, null, 1);
        await f.Database.SeedDiscountAsync(discount.Key, "ExactCoupon", 1);
        var ids = new List<string>();
        await f.Checkout.PrepareAsync(f.Order.Object.UniqueId, requests, ids, default);
        await f.Checkout.CompleteStockAsync(f.Order.Object.UniqueId, requests, ids, true, default);
        Assert.Equal(4, await f.Database.StockAsync(variantKey));
    }

    private static Mock<IOrderLine> Line(Guid key, OrderedProduct product, decimal quantity)
    {
        var line = new Mock<IOrderLine>();
        line.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        line.SetupGet(x => x.ProductKey).Returns(key);
        line.SetupGet(x => x.Product).Returns(product);
        line.SetupGet(x => x.Quantity).Returns(quantity);
        return line;
    }

    private sealed class Fixture : IDisposable
    {
        public ReservationDatabase Database { get; } = new();
        public Mock<IOrderInfo> Order { get; } = new();
        public IProduct Product { get; }
        public StoreInfo StoreInfo { get; } = new(Guid.NewGuid(), new CurrencyModel { CurrencyValue = "en-US" }, [], "en-US", "main", false, 0, false);
        public ConcurrentDictionary<Guid, IVariant> Variants { get; } = new();
        public List<string> Ids { get; } = new();
        public CheckoutReservationService Checkout { get; private set; } = null!;
        private readonly ConfigurationScope _scope;

        public Fixture(bool enabled)
        {
            var product = new Mock<IProduct>();
            product.SetupGet(x => x.Key).Returns(Guid.NewGuid());
            product.SetupGet(x => x.Stock).Returns(10);
            product.SetupGet(x => x.StockBuffer).Returns(2);
            product.SetupGet(x => x.Prices).Returns(new List<IPrice>());
            product.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>());
            Product = product.Object;
            var products = new Mock<IPerStoreIndexedCache<IProduct>>();
            var cached = Product;
            products.Setup(x => x.TryGetByKey("main", Product.Key, out cached)).Returns(true);
            var variants = new Mock<IPerStoreIndexedCache<IVariant>>();
            variants.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IVariant>>
            {
                ["main"] = Variants,
            });
            var store = new Mock<IStore>();
            store.SetupGet(x => x.Alias).Returns("main");
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("main")).Returns(store.Object);
            _scope = new ConfigurationScope(overrides: new Dictionary<string, string?> { ["Ekom:Reservations:Enabled"] = enabled.ToString() },
                addServices: services =>
                {
                    services.AddSingleton(sp => Database.NewCheckout(sp.GetRequiredService<Configuration>()));
                    services.AddSingleton(Database.NewStockApi());
                    services.AddSingleton(new API.Store(stores.Object, Mock.Of<ICacheRefreshService>()));
                    services.AddSingleton(sp => new Catalog(NullLogger<Catalog>.Instance, sp.GetRequiredService<Configuration>(),
                        sp.GetRequiredService<IServiceScopeFactory>(), products.Object, Mock.Of<IPerStoreIndexedCache<ICategory>>(),
                        Mock.Of<IPerStoreCache<IProductDiscount>>(), variants.Object, Mock.Of<IPerStoreIndexedCache<IVariantGroup>>(),
                        stores.Object, new HttpContextAccessor(), Mock.Of<IProductFilterService>()));
                });
            Checkout = Configuration.Resolver.GetRequiredService<CheckoutReservationService>();
            Order.SetupGet(x => x.UniqueId).Returns(Guid.NewGuid());
            Order.SetupGet(x => x.StoreInfo).Returns(StoreInfo);
            Order.SetupGet(x => x.ReservationIds).Returns(() => Ids);
            Order.SetupGet(x => x.CustomerInformation).Returns(new CustomerInfo());
            Order.SetupGet(x => x.OrderLines).Returns(new[] { Line(Product.Key, new OrderedProduct(Product, null, StoreInfo), 3).Object });
        }

        public Task<Guid> SeedAsync() => Database.SeedAsync(10, key: Product.Key);
        public TestController Controller(string? failure = null) => new(_scope.Instance, Database.Factory, Configuration.Resolver, Ids, failure);
        public void Dispose() { _scope.Dispose(); Database.Dispose(); }
    }

    private sealed class TestController : CheckoutControllerService
    {
        private readonly List<string> _ids;
        private readonly string? _failure;
        public bool Saved { get; private set; }
        public bool PaymentCalled { get; private set; }
        public List<string> IdsAtPayment { get; private set; } = new();
        public Func<Task>? BeforeCoupons { get; set; }
        public Func<IEnumerable<string>, Task>? PersistAction { get; set; }

        public TestController(Configuration config, DatabaseFactory database, IServiceProvider services, List<string> ids, string? failure)
            : base(NullLogger.Instance, config, database, Mock.Of<IMemberService>(), new HttpContextAccessor(), null!,
                services.GetRequiredService<IServiceScopeFactory>(), services)
        { _ids = ids; _failure = failure; }

        protected override Task<IOrderInfo> UpdateOrderDateAsync(Dictionary<string, string> collection, IOrderInfo order,
            Guid? paymentProviderKey = null, Guid? shippingProviderKey = null, CancellationToken ct = default) => Task.FromResult(order);
        protected override Task<CheckoutResponse?> PrepareCheckoutAsync(PaymentRequest request, IOrderInfo? order, CancellationToken ct)
            => Task.FromResult(_failure == "prepare" ? new CheckoutResponse { HttpStatusCode = 400 } : null);
        protected override Task<CheckoutResponse?> ValidationAndOrderUpdatesAsync(PaymentRequest request, IOrderInfo order, CancellationToken ct)
            => Task.FromResult(_failure == "validation" ? new CheckoutResponse { HttpStatusCode = 400 } : null);
        protected override async Task<CheckoutResponse> ProcessCouponsAsync(PaymentRequest request, IOrderInfo order, ICollection<string> ids, CancellationToken ct)
        {
            if (BeforeCoupons != null) await BeforeCoupons();
            return _failure == "cancel" ? throw new OperationCanceledException(new CancellationToken(true))
                : _failure == "coupon-throw" ? throw new InvalidOperationException("coupon failure")
                : _failure == "coupon" ? new CheckoutResponse { HttpStatusCode = 400 } : null!;
        }
        internal override async Task PersistReservationsAsync(IEnumerable<string> ids, IOrderInfo order, CancellationToken ct)
        {
            if (_failure == "save") throw new InvalidOperationException("save failure");
            if (PersistAction != null) await PersistAction(ids);
            var saved = ids.ToArray();
            _ids.Clear();
            _ids.AddRange(saved);
            Saved = true;
        }
        protected override Task<string> CreateOrderTitleAsync(PaymentRequest request, IOrderInfo order, IStore store, CancellationToken ct)
            => _failure == "title" ? throw new InvalidOperationException("title failure") : Task.FromResult("order");
        protected override Task<CheckoutResponse> ProcessPaymentAsync(PaymentRequest request, IOrderInfo order, string title, CancellationToken ct)
        {
            Assert.True(Saved);
            PaymentCalled = true;
            IdsAtPayment = order.ReservationIds.ToList();
            if (_failure == "payment") throw new InvalidOperationException("provider outcome unknown");
            return Task.FromResult(new CheckoutResponse { HttpStatusCode = 230 });
        }
    }
}
