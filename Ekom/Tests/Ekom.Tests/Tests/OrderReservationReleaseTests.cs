using Ekom.API;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Tests.Objects;
using Ekom.Tracking;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderReservationReleaseTests
{
    [Fact]
    public async Task CancelEditAndSubmitFreshAttemptIgnoreExpiredHistoryAndFenceStaleReturn()
    {
        using var fixture = new Fixture();
        await fixture.SeedStockAsync();
        await fixture.SaveAsync();
        var checkout = fixture.Database.NewCheckout();
        var historicalIds = new List<string>();
        var originalRequirements = await checkout.GetRequirementsAsync(fixture.Order, default);
        await checkout.PrepareAsync(fixture.Order.UniqueId, originalRequirements, historicalIds, default);
        await fixture.Database.MakeDueAsync(historicalIds.Single());
        await fixture.Database.Service.ExpireAsync(historicalIds.Single());
        await fixture.PrepareAsync(seedStock: false);
        var submittedSnapshot = (await fixture.AttemptAsync()).SubmittedOrderInfo;

        await fixture.Attempts.ReleaseAsync(fixture.Order.UniqueId, fixture.AttemptId, "payment-cancel");
        var edited = await fixture.Service.UpdateOrderLineQuantityAsync(fixture.LineId, 2, "main", fixture.Settings());
        await using var operation = await fixture.Attempts.BeginCheckoutAsync(edited);
        using var capability = operation.Enter();
        var nextIds = new List<string>();
        var updatedRequirements = await checkout.GetRequirementsAsync(edited, default);
        await checkout.PrepareAsync(edited.UniqueId, updatedRequirements, nextIds, default);
        edited.ReservationIds = nextIds;
        var data = (await fixture.Repository.GetOrderAsync(edited.UniqueId))!;
        data.OrderInfo = JsonConvert.SerializeObject(edited, EkomJsonDotNet.Settings);
        data.TotalAmount = edited.ChargedAmount.Value;
        await fixture.Repository.UpdateOrderAsync(data);
        var nextAttempt = await fixture.Attempts.SubmitAsync(edited);

        Assert.NotEqual(fixture.AttemptId, nextAttempt);
        Assert.Single(nextIds);
        Assert.Equal(1, await fixture.Database.StockAsync(fixture.ProductKey));
        Assert.Null(await fixture.Attempts.ReleaseAsync(edited.UniqueId, fixture.AttemptId, "stale cancel"));
        Assert.Equal(1, await fixture.Database.StockAsync(fixture.ProductKey));
        Assert.Equal(submittedSnapshot, (await fixture.AttemptAsync()).SubmittedOrderInfo);
        using var db = fixture.Database.Factory.GetDatabase();
        Assert.Equal(StockReservationState.Expired,
            (await db.StockReservations.SingleAsync(x => x.Id == historicalIds.Single())).State);
        Assert.Equal(CheckoutPaymentAttemptState.Submitted,
            (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == nextAttempt)).State);
    }

    [Fact]
    public async Task QuantityEditRestoresHeldStockBeforeAvailabilityValidationAndKeepsFrozenAttempt()
    {
        using var fixture = new Fixture(claimedGiftcard: true);
        await fixture.PrepareAsync();
        var frozen = await fixture.AttemptAsync();
        Assert.Equal(0, await fixture.Database.StockAsync(fixture.ProductKey));

        var updated = await fixture.Service.UpdateOrderLineQuantityAsync(fixture.LineId, 2, "main", fixture.Settings());

        Assert.Equal(fixture.Order.UniqueId, updated.UniqueId);
        Assert.Equal(2, Assert.Single(updated.OrderLines).Quantity);
        Assert.Equal(3, await fixture.Database.StockAsync(fixture.ProductKey));
        Assert.Equal(OrderStatus.Incomplete, updated.OrderStatus);
        Assert.Empty(updated.ReservationIds);
        var selection = Assert.Single(updated.Giftcards);
        Assert.Equal("selected-card", selection.Code);
        Assert.Equal(5, selection.Amount);
        Assert.False(selection.Claimed);
        Assert.Null(selection.ClaimId);
        Assert.Null(selection.ClaimDate);
        Assert.Null(selection.TransactionId);
        Assert.Equal(fixture.GiftcardValidity, selection.ValidUntil);
        fixture.GiftcardClaims.Verify(x => x.ReleaseAsync(updated.UniqueId, fixture.AttemptId,
            It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>()), Times.Once);
        var released = await fixture.AttemptAsync();
        Assert.Equal(CheckoutPaymentAttemptState.Released, released.State);
        Assert.Equal(frozen.SubmittedOrderInfo, released.SubmittedOrderInfo);
        Assert.Equal(frozen.SubmittedOrderData, released.SubmittedOrderData);
        Assert.Equal(frozen.ReservationIds, released.ReservationIds);
        Assert.False(await fixture.Attempts.CompleteAsync(updated.UniqueId, fixture.AttemptId,
            () => throw new InvalidOperationException("Old callback must not run.")));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RemovalAndProviderChangeReleaseOnSameOrder(bool changeProvider)
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync();
        var updated = changeProvider
            ? await fixture.Service.UpdatePaymentInformationAsync(fixture.AddPaymentProvider(), "main", [], fixture.Settings())
            : await fixture.Service.RemoveOrderLineAsync(fixture.LineId, "main", fixture.Settings());

        Assert.Equal(fixture.Order.UniqueId, updated.UniqueId);
        Assert.Equal(OrderStatus.Incomplete, updated.OrderStatus);
        Assert.Empty(updated.ReservationIds);
        Assert.Equal(3, await fixture.Database.StockAsync(fixture.ProductKey));
        Assert.Single(updated.Giftcards);
        Assert.Equal(CheckoutPaymentAttemptState.Released, (await fixture.AttemptAsync()).State);
        if (changeProvider) Assert.NotNull(updated.PaymentProvider);
        else Assert.Empty(updated.OrderLines);
        using var db = fixture.Database.Factory.GetDatabase();
        Assert.Single(await db.OrderData.ToListAsync());
        Assert.Null((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Fact]
    public async Task MetadataEditUsesReleasedSnapshotWithoutChangingAttemptHistory()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync();
        var frozen = (await fixture.AttemptAsync()).SubmittedOrderInfo;

        var updated = await fixture.Service.UpdateOrderLineMetadataAsync(fixture.Order.UniqueId,
            [new OrderLineMetadataUpdate
            {
                LineId = fixture.LineId,
                Properties = new Dictionary<string, string> { ["orderlineNote"] = "edited" },
            }]);

        Assert.Equal(OrderStatus.Incomplete, updated.OrderStatus);
        Assert.Empty(updated.ReservationIds);
        Assert.Equal("edited", Assert.Single(updated.OrderLines).OrderLineInfo.Properties["orderlineNote"]);
        Assert.Equal(3, await fixture.Database.StockAsync(fixture.ProductKey));
        Assert.Equal(frozen, (await fixture.AttemptAsync()).SubmittedOrderInfo);
    }

    [Fact]
    public async Task ReadingPendingBasketHasNoReleaseSideEffects()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync();
        var read = await fixture.Service.GetOrderAsync(fixture.Order.UniqueId);

        Assert.NotNull(read);
        Assert.Equal(OrderStatus.Pending, read.OrderStatus);
        Assert.Single(read.ReservationIds);
        Assert.Equal(0, await fixture.Database.StockAsync(fixture.ProductKey));
        Assert.Equal(CheckoutPaymentAttemptState.Submitted, (await fixture.AttemptAsync()).State);
        using var db = fixture.Database.Factory.GetDatabase();
        Assert.Empty(await db.GetTable<OrderActivityLog>().ToListAsync());
    }

    [Fact]
    public async Task SubmittedAttemptBlocksUnscopedWritesEvenWithUnchangedJson()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync();
        var stale = (await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!;
        stale.OrderStatus = OrderStatus.Incomplete;

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Repository.UpdateOrderAsync(stale));
        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Repository.TryUpdateOrderInfoAsync(stale.UniqueId,
            stale.OrderInfo, stale.OrderInfo, DateTime.Now));
        Assert.Equal(OrderStatus.Pending, (await fixture.Repository.GetOrderAsync(stale.UniqueId))!.OrderStatus);
        Assert.Equal(0, await fixture.Database.StockAsync(fixture.ProductKey));
    }

    [Fact]
    public async Task FirstLineOnNullPersistedOrderInfoPreservesStoreAndSavesUnderLease()
    {
        using var fixture = new Fixture(freshEmptyCart: true);
        await fixture.SeedStockAsync();
        Assert.Null((await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!.OrderInfo);

        var updated = await fixture.Service.AddOrderLineAsync(fixture.ProductKey, 1, "main",
            new AddOrderSettings { OrderInfo = fixture.Order, FireEvents = false, FireOnOrderUpdatedEvent = false });

        Assert.Equal("main", updated.StoreInfo.Alias);
        Assert.Equal("USD", updated.StoreInfo.Currency.ISOCurrencySymbol);
        Assert.Equal(fixture.ProductKey, Assert.Single(updated.OrderLines).ProductKey);
        var data = (await fixture.Repository.GetOrderAsync(updated.UniqueId))!;
        Assert.False(string.IsNullOrWhiteSpace(data.OrderInfo));
        var persisted = new OrderInfo(data);
        Assert.Equal("main", persisted.StoreInfo.Alias);
        Assert.Equal(1, Assert.Single(persisted.OrderLines).Quantity);
        using var db = fixture.Database.Factory.GetDatabase();
        Assert.Null((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Fact]
    public async Task ExternalReservationAttachmentKeepsIncomingHoldActiveAndReleasesBothOwners()
    {
        using var fixture = new Fixture();
        await fixture.SeedStockAsync();
        await fixture.SaveAsync();
        var hold = await fixture.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = fixture.ProductKey, Quantity = 3, StoreAlias = "main", OrderId = fixture.Order.UniqueId.ToString(),
        });

        await fixture.Service.AddReservationsToOrderAsync("main", [hold.ReservationId!], fixture.Order);

        var persisted = new OrderInfo((await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!);
        Assert.Equal(hold.ReservationId, Assert.Single(persisted.ReservationIds));
        Assert.Equal(0, await fixture.Database.StockAsync(fixture.ProductKey));
        using var db = fixture.Database.Factory.GetDatabase();
        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync()).State);
        Assert.Null((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
        Assert.Null((await db.GetTable<CheckoutPreparationData>().SingleAsync()).Owner);
        Assert.Empty(await db.GetTable<CheckoutPaymentAttemptData>().ToListAsync());
    }

    [Fact]
    public async Task RemovingUntrackedReservationMetadataDoesNotReleaseTheUnderlyingHold()
    {
        using var fixture = new Fixture();
        await fixture.SeedStockAsync();
        await fixture.SaveAsync();
        var hold = await fixture.Database.Service.ReserveAsync(new StockReservationRequest
        {
            Key = fixture.ProductKey, Quantity = 3, StoreAlias = "main", OrderId = fixture.Order.UniqueId.ToString(),
        });
        await fixture.Service.AddReservationsToOrderAsync("main", [hold.ReservationId!], fixture.Order);

        await fixture.Service.RemoveReservationsFromOrderAsync("main", default);

        var persisted = new OrderInfo((await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!);
        Assert.Empty(persisted.ReservationIds);
        Assert.Equal(0, await fixture.Database.StockAsync(fixture.ProductKey));
        using var db = fixture.Database.Factory.GetDatabase();
        Assert.Equal(StockReservationState.Active, (await db.StockReservations.SingleAsync()).State);
        Assert.Null((await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    [Fact]
    public async Task RemovingReservationMetadataRejectsSubmittedAttemptBeforeClearingIds()
    {
        using var fixture = new Fixture();
        await fixture.PrepareAsync();
        // Model a stale basket view predating the pending status update.
        var staleData = fixture.Order.OrderDataClone();
        staleData.OrderStatus = OrderStatus.Incomplete;
        fixture.CacheBasket(new OrderInfo(staleData));

        await Assert.ThrowsAnyAsync<Exception>(() => fixture.Service.RemoveReservationsFromOrderAsync("main", default));

        Assert.Single(new OrderInfo((await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!).ReservationIds);
        Assert.Equal(0, await fixture.Database.StockAsync(fixture.ProductKey));
        Assert.Equal(CheckoutPaymentAttemptState.Submitted, (await fixture.AttemptAsync()).State);
    }

    [Fact]
    public async Task SequentialEditsFromTwoCachedSnapshotsRefreshBeforeApplyingTheSecondIntent()
    {
        using var fixture = new Fixture();
        await fixture.SeedStockAsync();
        await fixture.SaveAsync();
        var first = new OrderInfo((await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!);
        var second = new OrderInfo((await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!);
        await fixture.Service.UpdateOrderLineQuantityAsync(fixture.LineId, 2, "main",
            new OrderSettings { OrderInfo = first, FireEvents = false, FireOnOrderUpdatedEvent = false });

        var updated = await fixture.Service.UpdateCustomerInformationAsync(new Dictionary<string, string>
        {
            ["storeAlias"] = "main", ["customerName"] = "Second edit",
        }, new OrderSettings { OrderInfo = second, FireEvents = false, FireOnOrderUpdatedEvent = false });

        Assert.Equal(2, Assert.Single(updated.OrderLines).Quantity);
        Assert.Equal("Second edit", updated.CustomerInformation.Customer.Name);
        var persisted = new OrderInfo((await fixture.Repository.GetOrderAsync(updated.UniqueId))!);
        Assert.Equal(2, Assert.Single(persisted.OrderLines).Quantity);
        Assert.Equal("Second edit", persisted.CustomerInformation.Customer.Name);
    }

    [Fact]
    public async Task UntrackedStalePayloadIsRejectedEvenInsideAnAuthorizedEditScope()
    {
        using var fixture = new Fixture();
        await fixture.SaveAsync();
        var stale = fixture.Order.OrderDataClone();
        await using var operation = await fixture.Attempts.BeginEditAsync(fixture.Order, "untracked stale test");
        using var capability = operation.Enter();

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Repository.UpdateOrderAsync(stale));
    }

    [Fact]
    public async Task StaleIncompleteSaveCannotRevertPaidStatusWhenJsonDidNotChange()
    {
        using var fixture = new Fixture();
        await fixture.SaveAsync();
        var stale = (await fixture.Repository.GetOrderAsync(fixture.Order.UniqueId))!;
        await using (var operation = await fixture.Attempts.BeginEditAsync(fixture.Order, "test paid status"))
        {
            using var capability = operation.Enter();
            var paid = (await fixture.Repository.GetOrderAsync(stale.UniqueId))!;
            paid.OrderStatus = OrderStatus.ReadyForDispatch;
            paid.PaidDate = DateTime.Now;
            await fixture.Repository.UpdateOrderAsync(paid);
        }

        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Repository.UpdateOrderAsync(stale));
        Assert.False(await fixture.Repository.TryUpdateOrderInfoAsync(stale.UniqueId, stale.OrderInfo,
            stale.OrderInfo, DateTime.Now, expectedStatus: stale.OrderStatusCol));
        Assert.Equal(OrderStatus.ReadyForDispatch, (await fixture.Repository.GetOrderAsync(stale.UniqueId))!.OrderStatus);
    }

    [Fact]
    public async Task NestedCheckoutProviderUpdateDoesNotReleaseItsPreparingAttempt()
    {
        using var fixture = new Fixture();
        await fixture.SaveAsync();
        await using var operation = await fixture.Attempts.BeginCheckoutAsync(fixture.Order);
        using var capability = operation.Enter();
        var updated = await fixture.Service.UpdatePaymentInformationAsync(fixture.AddPaymentProvider(), "main", [], fixture.Settings());

        Assert.Equal(fixture.Order.UniqueId, updated.UniqueId);
        using var db = fixture.Database.Factory.GetDatabase();
        Assert.Equal(CheckoutPaymentAttemptState.Preparing,
            (await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync()).State);
        Assert.Equal(operation.Owner, (await db.GetTable<CheckoutPaymentOperationData>().SingleAsync()).Owner);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly ConfigurationScope _scope;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        private readonly ConcurrentDictionary<Guid, IPaymentProvider> _payments = new();

        public Fixture(bool claimedGiftcard = false, bool freshEmptyCart = false)
        {
            var store = new Mock<IStore>();
            store.SetupGet(x => x.Alias).Returns("main");
            store.SetupGet(x => x.Culture).Returns(new CultureInfoDto { Name = "en-US" });
            store.SetupGet(x => x.Cultures).Returns([new CultureInfoDto { Name = "en-US" }]);
            store.SetupGet(x => x.Currencies).Returns([new CurrencyModel { CurrencyValue = "en-US" }]);
            var stores = new Mock<IStoreService>();
            stores.Setup(x => x.GetStoreByAlias("main")).Returns(store.Object);
            stores.Setup(x => x.GetStoreFromCache()).Returns(store.Object);
            var products = new Mock<IPerStoreIndexedCache<IProduct>>();
            var product = new Mock<IProduct>();
            product.SetupGet(x => x.Key).Returns(ProductKey);
            product.SetupGet(x => x.Stock).Returns(() => Ekom.API.Stock.Instance.GetStock(ProductKey, "main"));
            product.SetupGet(x => x.Url).Returns("");
            product.SetupGet(x => x.AllVariants).Returns(Array.Empty<IVariant>());
            product.SetupGet(x => x.Properties).Returns(new Dictionary<string, string>
            {
                ["__Key"] = ProductKey.ToString(), ["title"] = "Product", ["sku"] = "product",
            });
            IProduct? cachedProduct = product.Object;
            products.Setup(x => x.TryGetByKey("main", ProductKey, out cachedProduct)).Returns(true);
            var discounts = new Mock<IPerStoreCache<IDiscount>>();
            discounts.SetupGet(x => x.Cache).Returns(new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IDiscount>>
            {
                ["main"] = new(),
            });
            var payments = new Mock<IPerStoreCache<IPaymentProvider>>();
            payments.Setup(x => x["main"]).Returns(_payments);
            _scope = new ConfigurationScope(addServices: services =>
            {
                services.AddSingleton(Database.NewStockApi());
                services.AddSingleton(Database.NewCheckout());
                services.AddSingleton(new Ekom.API.Store(stores.Object, Mock.Of<ICacheRefreshService>()));
                services.AddSingleton(sp => new Providers(sp.GetRequiredService<Configuration>(), NullLogger<Providers>.Instance,
                    Mock.Of<IPerStoreCache<IShippingProvider>>(x => x["main"] == new ConcurrentDictionary<Guid, IShippingProvider>()),
                    payments.Object, Mock.Of<IBaseCache<IZone>>(), stores.Object, null!));
                services.AddSingleton(sp => new Discounts(sp.GetRequiredService<Configuration>(), NullLogger<Discounts>.Instance,
                    discounts.Object, stores.Object));
                services.AddSingleton(sp => new Catalog(NullLogger<Catalog>.Instance, sp.GetRequiredService<Configuration>(),
                    sp.GetRequiredService<IServiceScopeFactory>(), products.Object, Mock.Of<IPerStoreIndexedCache<ICategory>>(),
                    Mock.Of<IPerStoreCache<IProductDiscount>>(), Mock.Of<IPerStoreIndexedCache<IVariant>>(),
                    Mock.Of<IPerStoreIndexedCache<IVariantGroup>>(), stores.Object,
                    new Microsoft.AspNetCore.Http.HttpContextAccessor(), Mock.Of<IProductFilterService>()));
            });
            using var db = Database.Factory.GetDatabase();
            db.CreateTable<OrderData>();
            db.CreateTable<OrderActivityLog>();
            db.CreateTable<CheckoutPaymentOperationData>(tableOptions: TableOptions.CreateIfNotExists);
            db.CreateTable<CheckoutPaymentAttemptData>(tableOptions: TableOptions.CreateIfNotExists);
            var currency = new CurrencyModel { CurrencyValue = "en-US" };
            store.SetupGet(x => x.Currency).Returns(currency);
            var storeInfo = new StoreInfo(Guid.NewGuid(), currency, [currency], "en-US", "main", false, 0, false);
            var data = new OrderData
            {
                UniqueId = Guid.NewGuid(), StoreAlias = "main", Currency = "USD", OrderStatus = OrderStatus.Incomplete,
                OrderInfo = freshEmptyCart ? null! : JsonConvert.SerializeObject(new
                {
                    StoreInfo = storeInfo, OrderLines = Array.Empty<object>(), CustomerInformation = new CustomerInfo(),
                }),
            };
            db.Insert(data);
            Order = freshEmptyCart ? new OrderInfo(data, store.Object) : new OrderInfo(data);
            product.SetupGet(x => x.Prices).Returns([new Price(10, currency, 0, false)]);
            if (!freshEmptyCart)
            {
                Order.orderLines.Add(new OrderLine(product.Object, 3, LineId, Order, []));
                Order.Giftcards.Add(new Giftcard
                {
                    Code = "selected-card", Amount = 5, ValidUntil = GiftcardValidity,
                    Claimed = false, ClaimId = claimedGiftcard ? "old-claim" : null,
                });
            }
            GiftcardClaims.Setup(x => x.ReleaseAsync(It.IsAny<Guid>(), It.IsAny<Guid>(),
                It.IsAny<IReadOnlyList<Giftcard>>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
            Attempts = new CheckoutPaymentAttemptService(Database.Factory, Database.Service,
                new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, Database.Factory),
                NullLogger<CheckoutPaymentAttemptService>.Instance, _cache, claimedGiftcard ? GiftcardClaims.Object : null);
            Repository = new OrderRepository(NullLogger<OrderRepository>.Instance, _scope.Instance, Database.Factory, _cache, Attempts);
            var context = new Microsoft.AspNetCore.Http.DefaultHttpContext();
            context.Request.Headers.Cookie = $"ekmOrder-main={Order.UniqueId}";
            Service = new OrderService(_scope.Instance, Repository, null!, Mock.Of<IOrderActivityLogService>(),
                NullLogger<OrderService>.Instance, stores.Object, _cache, Mock.Of<IMemberService>(), null!,
                Mock.Of<IOrderTrackingService>(), new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = context },
                paymentAttempts: Attempts);
        }

        public StockReservationTests.ReservationDatabase Database { get; } = new();
        public Guid ProductKey { get; } = Guid.NewGuid();
        public Guid LineId { get; } = Guid.NewGuid();
        public Guid AttemptId { get; } = Guid.NewGuid();
        public DateTime GiftcardValidity { get; } = new(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        public Mock<ICheckoutGiftcardReservations> GiftcardClaims { get; } = new();
        public OrderInfo Order { get; private set; }
        public OrderRepository Repository { get; }
        public OrderService Service { get; }
        public CheckoutPaymentAttemptService Attempts { get; }

        public OrderSettings Settings() => new() { OrderInfo = Order, FireEvents = false, FireOnOrderUpdatedEvent = false };

        public void CacheBasket(OrderInfo order) => _cache.Set(order.UniqueId.ToString(), order);

        public async Task SaveAsync()
        {
            var data = (await Repository.GetOrderAsync(Order.UniqueId))!;
            data.OrderInfo = JsonConvert.SerializeObject(Order, EkomJsonDotNet.Settings);
            data.TotalAmount = Order.ChargedAmount.Value;
            await Repository.UpdateOrderAsync(data);
        }

        public async Task PrepareAsync(bool seedStock = true)
        {
            if (seedStock) await SeedStockAsync();
            var hold = await Database.Service.ReserveAsync(new StockReservationRequest
            {
                Key = ProductKey, Quantity = 3, StoreAlias = "main", OrderId = Order.UniqueId.ToString(), PaymentAttemptId = AttemptId.ToString(),
            });
            Order._hangfireJobs.Add(hold.ReservationId!);
            await SaveAsync();
            using var db = Database.Factory.GetDatabase();
            await db.OrderData.Where(x => x.UniqueId == Order.UniqueId).Set(x => x.OrderStatusCol, "Pending").UpdateAsync();
            var persisted = (await Repository.GetOrderAsync(Order.UniqueId))!;
            await db.InsertAsync(new CheckoutPaymentAttemptData
            {
                AttemptId = AttemptId, OrderId = Order.UniqueId, State = CheckoutPaymentAttemptState.Submitted,
                SubmittedOrderInfo = persisted.OrderInfo, SubmittedOrderData = JsonConvert.SerializeObject(persisted),
                ReservationIds = JsonConvert.SerializeObject(Order.ReservationIds), CreatedUtc = DateTime.UtcNow, SubmittedUtc = DateTime.UtcNow,
            });
            await db.InsertAsync(new CheckoutPaymentOperationData { OrderId = Order.UniqueId, ActiveAttemptId = AttemptId });
            Order = new OrderInfo(persisted);
            _cache.Set(Order.UniqueId.ToString(), Order);
        }

        public async Task SeedStockAsync()
        {
            await Database.SeedAsync(3, key: ProductKey);
            // Production availability reads the stock cache; direct SQL fixture inserts do not publish it.
            Database.StockCache[ProductKey] = new StockData
            {
                UniqueId = ProductKey.ToString(), Stock = 3, CreateDate = DateTime.UtcNow, UpdateDate = DateTime.UtcNow,
            };
        }

        public async Task<CheckoutPaymentAttemptData> AttemptAsync()
        {
            using var db = Database.Factory.GetDatabase();
            return await db.GetTable<CheckoutPaymentAttemptData>().SingleAsync(x => x.AttemptId == AttemptId);
        }

        public Guid AddPaymentProvider()
        {
            var key = Guid.NewGuid();
            var provider = new Mock<IPaymentProvider>();
            provider.SetupGet(x => x.Key).Returns(key);
            provider.SetupGet(x => x.Title).Returns("Payment");
            provider.SetupGet(x => x.Properties).Returns(new Dictionary<string, string> { ["title"] = "Payment" });
            provider.SetupGet(x => x.Prices).Returns([new Price(0, Order.StoreInfo.Currency, 0, false)]);
            var constraints = new Mock<IConstraints>();
            constraints.Setup(x => x.IsValid(It.IsAny<string>(), It.IsAny<decimal>())).Returns(true);
            provider.SetupGet(x => x.Constraints).Returns(constraints.Object);
            _payments[key] = provider.Object;
            return key;
        }

        public void Dispose()
        {
            _scope.Dispose();
            _cache.Dispose();
            Database.Dispose();
        }
    }
}
