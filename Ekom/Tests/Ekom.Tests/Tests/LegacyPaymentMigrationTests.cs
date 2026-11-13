using Ekom.Models;
using Ekom.Models.Orders;
using Ekom.Models.Payments;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

/// <summary>
/// Payments made with Ekom.Payments 1.0 and earlier are copied onto their orders as payment history.
/// </summary>
[Collection("Reservations")]
public sealed class LegacyPaymentMigrationTests
{
    [Fact]
    public async Task PaidPaymentIsCopiedWithItsCaptureAndProviderDetailsButWithoutSecrets()
    {
        using var fixture = new Fixture();
        var orderId = fixture.AddOrder("IS-1001", paymentStatus: PaymentStatus.Paid);
        var payment = fixture.AddLegacy(1, orderId, paid: true, provider: "valitor",
            response: """{"ReferenceNumber":"r","DigitalSignatureResponse":"abc123","CardType":"VISA","CardNumberMasked":"************1111","AuthorizationNumber":"654321","TransactionNumber":778899,"AdditionalData":{"Token":"card-token","AuthCode":"x"}}""");

        var result = await fixture.Migration.MigrateAsync();

        Assert.Equal(new LegacyPaymentMigrationResult(1, 0, 0, 1), result);
        using var db = fixture.Factory.GetDatabase();
        var copied = await db.OrderPayments.SingleAsync();
        Assert.Equal(payment, copied.PaymentId);
        Assert.Equal(orderId, copied.OrderUniqueId);
        Assert.Equal("IS-1001", copied.OrderNumber);
        Assert.Equal("valitor", copied.ProviderType);
        Assert.Equal("Card payment", copied.ProviderName);
        Assert.Equal("ISK", copied.Currency);
        Assert.Equal("main", copied.StoreAlias);
        Assert.Equal(Fixture.AttemptId, copied.AttemptId);
        Assert.Null(copied.ProviderReference);
        Assert.Equal(DateTimeKind.Utc, DateTime.SpecifyKind(copied.CreatedUtc, DateTimeKind.Utc).Kind);

        var rows = await db.OrderPaymentTransactions.OrderBy(x => x.Id).ToListAsync();
        Assert.Equal(new[] { PaymentTransactionType.Requested, PaymentTransactionType.Captured }, rows.Select(x => x.Type));
        var capture = rows[1];
        Assert.Equal(1990m, capture.Amount);
        Assert.Equal("778899", capture.TransactionId);
        Assert.Equal("654321", capture.AuthorizationCode);
        Assert.Equal("VISA", capture.CardBrand);
        Assert.Equal("************1111", capture.MaskedCardNumber);
        Assert.DoesNotContain("abc123", capture.RawData!, StringComparison.Ordinal);
        Assert.DoesNotContain("card-token", capture.RawData!, StringComparison.Ordinal);
        Assert.Contains("778899", capture.RawData!, StringComparison.Ordinal);

        // History only: the order, its status and its activity log are untouched.
        var order = await db.OrderData.SingleAsync();
        Assert.Equal(1, order.StatusRevision);
        Assert.Equal(PaymentStatus.Paid, order.Status.PaymentStatus);
        Assert.Equal(0, await db.OrderActivityLog.CountAsync());

        var view = await fixture.Payments.GetForOrderAsync(orderId);
        Assert.Equal("Paid", view.Current!.Status);
        Assert.Equal("654321", view.Current.AuthorizationCode);
    }

    [Fact]
    public async Task AuthorizationCodeStoredInPlaceOfAResponseAndUnpaidAttemptsAreCopied()
    {
        using var fixture = new Fixture();
        var orderId = fixture.AddOrder("IS-1002");
        var declined = fixture.AddLegacy(1, orderId, paid: false, provider: "borgun");
        var paid = fixture.AddLegacy(2, orderId, paid: true, provider: "borgun", response: "123456", cardNumber: "****1111");

        await fixture.Migration.MigrateAsync();

        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(PaymentTransactionType.Requested, (await db.OrderPaymentTransactions.SingleAsync(x => x.PaymentId == declined)).Type);
        var capture = await db.OrderPaymentTransactions.SingleAsync(x => x.PaymentId == paid && x.TypeCol == "Captured");
        Assert.Equal("123456", capture.AuthorizationCode);
        Assert.Equal("****1111", capture.MaskedCardNumber);
        Assert.Null(capture.RawData);
        var view = await fixture.Payments.GetForOrderAsync(orderId);
        Assert.Equal(2, view.Attempts.Count);
        Assert.Equal(paid, view.Current!.PaymentId);
    }

    [Theory]
    // Where providers kept the order of a payment
    [InlineData("settings")]
    [InlineData("customData")]
    [InlineData("orderCustomData")]
    public async Task OrderIsFoundWhereverTheProviderKeptIt(string place)
    {
        using var fixture = new Fixture();
        var orderId = fixture.AddOrder("IS-1003");
        fixture.AddLegacy(1, orderId, paid: false, provider: "siminnpay", orderIdIn: place,
            customData: place == "customData" ? null : "siminn-order-key-77");

        var result = await fixture.Migration.MigrateAsync();

        Assert.Equal(1, result.Migrated);
        using var db = fixture.Factory.GetDatabase();
        var copied = await db.OrderPayments.SingleAsync();
        Assert.Equal(orderId, copied.OrderUniqueId);
        // A provider's own lookup key is kept so its callbacks still find the payment.
        Assert.Equal(place == "customData" ? null : "siminn-order-key-77", copied.ProviderReference);
    }

    [Fact]
    public async Task PaymentsWithoutAnOrderAreReportedAndLeftInTheOldTable()
    {
        using var fixture = new Fixture();
        var orderId = fixture.AddOrder("IS-1004");
        fixture.AddLegacy(1, Guid.NewGuid(), paid: true, provider: "borgun");
        fixture.AddLegacy(2, orderId, paid: false, provider: "borgun", settingsJson: "not json", customData: "also not an id");
        fixture.AddLegacy(3, orderId, paid: false, provider: "borgun");

        var result = await fixture.Migration.MigrateAsync();

        Assert.Equal(new LegacyPaymentMigrationResult(1, 0, 2, 3), result);
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(1, await db.OrderPayments.CountAsync());
        Assert.Equal(3, await db.GetTable<LegacyPaymentOrderRow>().CountAsync());
    }

    [Fact]
    public async Task RunningAgainChangesNothingAndLaterRowsAreCopiedInSmallBatches()
    {
        using var fixture = new Fixture();
        var orderId = fixture.AddOrder("IS-1005");
        for (var i = 1; i <= 5; i++) fixture.AddLegacy(i, orderId, paid: i % 2 == 0, provider: "netgiro");

        var first = await fixture.Migration.MigrateAsync(batchSize: 2);
        var second = await fixture.Migration.MigrateAsync(batchSize: 2);
        fixture.AddLegacy(6, orderId, paid: true, provider: "netgiro");
        var third = await fixture.Migration.MigrateAsync(batchSize: 2);

        Assert.Equal(new LegacyPaymentMigrationResult(5, 0, 0, 5), first);
        Assert.Equal(new LegacyPaymentMigrationResult(0, 0, 0, 5), second);
        Assert.Equal(new LegacyPaymentMigrationResult(1, 0, 0, 6), third);
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(6, await db.OrderPayments.CountAsync());
        Assert.Equal(6 + 3, await db.OrderPaymentTransactions.CountAsync());
    }

    [Fact]
    public async Task PaymentAlreadyOnItsOrderIsNotCopiedAndARepeatedCallbackAfterTheCopyIsNotStoredAgain()
    {
        using var fixture = new Fixture();
        var orderId = fixture.AddOrder("IS-1006", paymentStatus: PaymentStatus.Paid);
        var copiedByCallback = fixture.AddLegacy(1, orderId, paid: false, provider: "borgun");
        await fixture.Payments.CreateAsync(new NewOrderPayment { OrderUniqueId = orderId, PaymentId = copiedByCallback, Amount = 1990m });
        var paid = fixture.AddLegacy(2, orderId, paid: true, provider: "borgun", response: "123456");

        var result = await fixture.Migration.MigrateAsync();
        // The gateway repeats its callback for the migrated payment, using the key providers use for a capture.
        var repeated = await fixture.Payments.RecordAsync(new PaymentTransaction
        {
            PaymentId = paid, Type = PaymentTransactionType.Captured, IdempotencyKey = $"{paid:N}:Captured",
        });

        Assert.Equal(new LegacyPaymentMigrationResult(1, 1, 0, 2), result);
        Assert.False(repeated.Recorded);
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(1, await db.OrderPaymentTransactions.CountAsync(x => x.PaymentId == paid && x.TypeCol == "Captured"));
    }

    [Fact]
    public async Task SiteWithoutOldPaymentTablesHasNothingToCopy()
    {
        using var fixture = new Fixture(legacyTables: false);

        Assert.Equal(new LegacyPaymentMigrationResult(0, 0, 0, 0), await fixture.Migration.MigrateAsync());
    }

    private sealed class Fixture : IDisposable
    {
        public static readonly Guid AttemptId = Guid.NewGuid();
        private readonly SqliteConnection _connection;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public DatabaseFactory Factory { get; }
        public LegacyPaymentMigrationService Migration { get; }
        public IOrderPayments Payments { get; }

        public Fixture(bool legacyTables = true)
        {
            var connectionString = $"Data Source=legacy-payments-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            var root = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = connectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            Factory = new DatabaseFactory(root, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
            new DatabaseService(Factory, NullLogger<DatabaseService>.Instance, new StockReservationReadiness()).CreateTables();
            if (legacyTables)
            {
                using var db = Factory.GetDatabase();
                db.CreateTable<LegacyPaymentOrderRow>();
                db.CreateTable<LegacyPaymentDataRow>();
            }
            Migration = new LegacyPaymentMigrationService(Factory, NullLogger<LegacyPaymentMigrationService>.Instance);
            Payments = new OrderPaymentService(Factory, new Configuration(root), _cache,
                Array.Empty<IOrderStatusPersistenceHandler>(), NullLogger<OrderPaymentService>.Instance);
        }

        public Guid AddOrder(string orderNumber, PaymentStatus paymentStatus = PaymentStatus.Pending)
        {
            var id = Guid.NewGuid();
            using var db = Factory.GetDatabase();
            db.Insert(new OrderData
            {
                UniqueId = id, StoreAlias = "main", OrderNumber = orderNumber, OrderStatusCol = "ReadyForDispatch", OrderInfo = "{}",
                TotalAmount = 1990m, Currency = "is-IS", OrderStateCol = nameof(OrderStatus.Open),
                PaymentStateCol = paymentStatus.ToString(), FulfillmentStateCol = nameof(FulfillmentStatus.Unfulfilled),
                StatusMappingVersion = 1, StatusRevision = 1,
            });
            return id;
        }

        /// <summary>A payment row as Ekom.Payments 1.0 stored it.</summary>
        public Guid AddLegacy(long referenceId, Guid orderId, bool paid, string provider, string? response = null,
            string? cardNumber = null, string orderIdIn = "settings", string? customData = "order", string? settingsJson = null)
        {
            var paymentId = Guid.NewGuid();
            var settings = settingsJson ?? System.Text.Json.JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["OrderUniqueId"] = orderIdIn == "settings" ? orderId : Guid.Empty,
                ["OrderNumber"] = "",
                ["PaymentProviderKey"] = Guid.NewGuid(),
                ["PaymentProviderName"] = "Card payment",
                ["Currency"] = "ISK",
                ["EkomPropertyKeys"] = new Dictionary<string, string> { ["Language"] = "is-IS", ["Store"] = "main" },
                ["OrderCustomData"] = new Dictionary<string, string>
                {
                    ["ekomOrderUniqueId"] = orderIdIn == "orderCustomData" ? orderId.ToString() : "",
                    ["ekomPaymentAttemptId"] = AttemptId.ToString(),
                },
            });
            using var db = Factory.GetDatabase();
            db.Insert(new LegacyPaymentOrderRow
            {
                UniqueId = paymentId, ReferenceId = referenceId, Amount = 1990m, Date = new DateTime(2026, 3, 1, 12, 0, 0),
                IPAddress = "203.0.113.7", UserAgent = "test", Paid = paid, EkomPaymentSettingsData = settings,
                EkomPaymentProviderData = "{\"MerchantId\":\"1\",\"ekomPaymentProvider\":\"" + provider + "\"}",
                CustomData = customData == "order" ? (orderIdIn == "orderCustomData" ? "siminn-order-key-77" : orderId.ToString()) : customData ?? orderId.ToString(),
            });
            if (paid && (response != null || cardNumber != null))
            {
                db.Insert(new LegacyPaymentDataRow
                {
                    Id = paymentId, CustomData = response, CardNumber = cardNumber, Date = new DateTime(2026, 3, 1, 12, 1, 0),
                });
            }
            return paymentId;
        }

        public void Dispose()
        {
            _cache.Dispose();
            _connection.Dispose();
        }
    }
}
