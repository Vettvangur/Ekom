using Ekom.Events;
using Ekom.Models;
using Ekom.Models.Orders;
using Ekom.Models.Payments;
using Ekom.Repositories;
using Ekom.Services;
using LinqToDB;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderPaymentsTests
{
    private static readonly Guid PaymentA = Guid.NewGuid();
    private static readonly Guid PaymentB = Guid.NewGuid();

    public static TheoryData<string, PaymentStatus?> CalculatorCases => new()
    {
        { "", null },
        { "A:Requested", PaymentStatus.Pending },
        { "A:Requested,A:Failed", PaymentStatus.Failed },
        { "A:Requested,A:Cancelled", PaymentStatus.Pending },
        { "A:Requested,A:Failed,B:Requested", PaymentStatus.Pending },
        { "A:Requested,A:Authorized", PaymentStatus.Authorized },
        { "A:Requested,A:Authorized,A:Voided", PaymentStatus.Voided },
        { "A:Requested,A:Authorized,B:Requested,B:Failed", PaymentStatus.Authorized },
        { "A:Requested,A:Captured", PaymentStatus.Paid },
        { "A:Requested,A:Captured=40", PaymentStatus.PartiallyPaid },
        { "A:Requested,A:Captured=40,B:Requested,B:Captured=60", PaymentStatus.Paid },
        { "A:Requested,A:Captured,A:Refunded=30", PaymentStatus.PartiallyRefunded },
        { "A:Requested,A:Captured,A:Refunded", PaymentStatus.Refunded },
        // A late failure of an abandoned attempt never downgrades a paid order.
        { "A:Requested,B:Requested,B:Captured,A:Failed", PaymentStatus.Paid },
        // A manual choice overrides what came before it, and survives later failures.
        { "A:Requested,A:Failed,Manual=Paid", PaymentStatus.Paid },
        { "A:Requested,Manual=Paid,A:Failed", PaymentStatus.Paid },
        { "A:Requested,A:Captured,Manual=Pending", PaymentStatus.Pending },
        // Real money movement after a manual choice counts again.
        { "A:Requested,Manual=Pending,A:Captured", PaymentStatus.Paid },
        { "A:Requested,A:Captured,Manual=Paid,A:Refunded", PaymentStatus.Refunded },
    };

    [Theory]
    [MemberData(nameof(CalculatorCases))]
    public void CalculatorDerivesStatusFromTransactions(string history, PaymentStatus? expected)
        => Assert.Equal(expected, PaymentStatusCalculator.Calculate(Rows(history), 100m));

    [Fact]
    public async Task CaptureMarksOrderPaidLogsWithOrderNumberAndIsStoredOnce()
    {
        using var fixture = new Fixture();
        var orderId = fixture.AddOpenOrder();
        var recorded = new List<PaymentRecordedEventArgs>();
        Func<object, PaymentRecordedEventArgs, CancellationToken, Task> handler = (_, args, _) => { recorded.Add(args); return Task.CompletedTask; };
        OrderEvents.PaymentRecordedAsync += handler;
        try
        {
            var payment = await fixture.Payments.CreateAsync(fixture.NewPayment(orderId));
            var capture = new PaymentTransaction
            {
                PaymentId = payment.PaymentId,
                Type = PaymentTransactionType.Captured,
                TransactionId = "TX-1",
                AuthorizationCode = "123456",
                MaskedCardNumber = "5587-40**-****-0014",
                CardBrand = "Mastercard",
                Details = [new PaymentDetail("rrn", "RRN", "000000020512", true)],
                RawData = "{\"status\":\"OK\"}",
            };

            var first = await fixture.Payments.RecordAsync(capture);
            var second = await fixture.Payments.RecordAsync(capture);

            Assert.True(first.Recorded);
            Assert.Equal(PaymentStatus.Paid, first.PaymentStatus);
            Assert.False(second.Recorded);
            Assert.Equal(PaymentStatus.Paid, second.PaymentStatus);

            using var db = fixture.Factory.GetDatabase();
            Assert.Equal(2, await db.OrderPaymentTransactions.CountAsync(x => x.OrderUniqueId == orderId));
            var order = await db.OrderData.SingleAsync(x => x.UniqueId == orderId);
            Assert.Equal(PaymentStatus.Paid, order.Status.PaymentStatus);
            Assert.Equal(2, order.StatusRevision);
            Assert.Equal("ReadyForDispatch", order.OrderStatusCol);
            Assert.Equal("TX-1", (await db.OrderPayments.SingleAsync()).TransactionId);

            var log = await db.OrderActivityLog.SingleAsync(x => x.Key == orderId);
            Assert.Equal(OrderActivityLogType.Success, log.LogType);
            Assert.StartsWith("Payment successful. Order number: IS-1042;", log.Log, StringComparison.Ordinal);
            Assert.Contains("Transaction ID: TX-1", log.Log, StringComparison.Ordinal);
            Assert.Contains($"PaymentId: {payment.PaymentId}", log.Log, StringComparison.Ordinal);

            var args = Assert.Single(recorded, x => x.Payment.PaymentId == payment.PaymentId);
            Assert.Equal(PaymentStatus.Pending, args.PreviousPaymentStatus);
            Assert.Equal(PaymentStatus.Paid, args.PaymentStatus);

            var view = await fixture.Payments.GetForOrderAsync(orderId);
            Assert.Equal("Paid", view.Current!.Status);
            Assert.Equal("123456", view.Current.AuthorizationCode);
            Assert.Equal("Mastercard", view.Current.CardBrand);
            Assert.NotNull(view.Current.CapturedUtc);
            Assert.Equal("RRN", Assert.Single(view.Current.Details).Label);
            Assert.Equal(orderId, await fixture.Payments.FindOrderAsync("TX-1"));
            Assert.Equal(orderId, await fixture.Payments.FindOrderAsync(payment.PaymentId.ToString()));
        }
        finally
        {
            OrderEvents.PaymentRecordedAsync -= handler;
        }
    }

    [Fact]
    public async Task DeclinedAttemptThenRetryShowsBothAttemptsAndLateFailureKeepsOrderPaid()
    {
        using var fixture = new Fixture();
        var orderId = fixture.AddOpenOrder();
        var declined = await fixture.Payments.CreateAsync(fixture.NewPayment(orderId));
        var failed = await fixture.Payments.RecordAsync(new PaymentTransaction
        {
            PaymentId = declined.PaymentId, Type = PaymentTransactionType.Failed, Message = "Declined: insufficient funds",
        });
        Assert.Equal(PaymentStatus.Failed, failed.PaymentStatus);

        var retry = await fixture.Payments.CreateAsync(fixture.NewPayment(orderId));
        await fixture.Payments.RecordAsync(new PaymentTransaction { PaymentId = retry.PaymentId, Type = PaymentTransactionType.Captured, TransactionId = "TX-2" });
        var late = await fixture.Payments.RecordAsync(new PaymentTransaction
        {
            PaymentId = declined.PaymentId, Type = PaymentTransactionType.Failed, TransactionId = "late", Message = "Timed out",
        });

        Assert.True(late.Recorded);
        Assert.Equal(PaymentStatus.Paid, late.PaymentStatus);
        var view = await fixture.Payments.GetForOrderAsync(orderId);
        Assert.Equal(2, view.Attempts.Count);
        Assert.Equal(retry.PaymentId, view.Current!.PaymentId);
        Assert.Equal(retry.PaymentId, view.Attempts[0].PaymentId);
        Assert.Equal("Failed", view.Attempts[1].Status);
        Assert.Equal("Timed out", view.Attempts[1].Message);
        using var db = fixture.Factory.GetDatabase();
        Assert.Equal(OrderActivityLogType.Alert, (await db.OrderActivityLog.Where(x => x.Key == orderId).ToListAsync())[0].LogType);
    }

    [Fact]
    public async Task ManagerPaymentChangeIsStoredAsManualRowAndSurvivesLateFailure()
    {
        using var fixture = new Fixture();
        var orderId = fixture.AddOpenOrder();
        var payment = await fixture.Payments.CreateAsync(fixture.NewPayment(orderId));

        var changed = await fixture.States.ChangeAsync(orderId, new OrderStateChangeRequest(1, "PaymentPaid"));
        var late = await fixture.Payments.RecordAsync(new PaymentTransaction { PaymentId = payment.PaymentId, Type = PaymentTransactionType.Failed });

        Assert.Equal(PaymentStatus.Paid, changed.PaymentStatus);
        Assert.Equal(PaymentStatus.Paid, late.PaymentStatus);
        var view = await fixture.Payments.GetForOrderAsync(orderId);
        var manual = Assert.Single(view.ManualChanges);
        Assert.Equal("Paid", manual.ManualStatus);
        Assert.Equal("anna", manual.Actor);
    }

    [Fact]
    public async Task OrderNotYetOnIndependentStatusesKeepsItsStoredStatus()
    {
        using var fixture = new Fixture();
        var orderId = fixture.AddOpenOrder(migrated: false);
        var payment = await fixture.Payments.CreateAsync(fixture.NewPayment(orderId));

        var result = await fixture.Payments.RecordAsync(new PaymentTransaction { PaymentId = payment.PaymentId, Type = PaymentTransactionType.Captured });

        Assert.True(result.Recorded);
        using var db = fixture.Factory.GetDatabase();
        var order = await db.OrderData.SingleAsync(x => x.UniqueId == orderId);
        Assert.Equal(0, order.StatusMappingVersion);
        Assert.Equal("Pending", order.OrderStatusCol);
        Assert.Null(order.PaymentStateCol);
    }

    [Fact]
    public async Task RecordingForUnknownPaymentFails()
    {
        using var fixture = new Fixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Payments.RecordAsync(
            new PaymentTransaction { PaymentId = Guid.NewGuid(), Type = PaymentTransactionType.Captured }));
    }

    private static List<OrderPaymentTransaction> Rows(string history)
    {
        var rows = new List<OrderPaymentTransaction>();
        var start = new DateTime(2026, 11, 12, 14, 0, 0, DateTimeKind.Utc);
        foreach (var part in history.Split(',', StringSplitOptions.RemoveEmptyEntries))
        {
            var row = new OrderPaymentTransaction { Id = rows.Count + 1, OccurredUtc = start.AddMinutes(rows.Count), Amount = 100m };
            if (part.StartsWith("Manual=", StringComparison.Ordinal))
            {
                row.Type = PaymentTransactionType.Manual;
                row.ManualStatusCol = part[7..];
                row.Amount = 0;
            }
            else
            {
                var pieces = part.Split(':', '=');
                row.PaymentId = pieces[0] == "A" ? PaymentA : PaymentB;
                row.Type = Enum.Parse<PaymentTransactionType>(pieces[1]);
                if (pieces.Length > 2) row.Amount = decimal.Parse(pieces[2], System.Globalization.CultureInfo.InvariantCulture);
            }
            rows.Add(row);
        }
        return rows;
    }

    private sealed class Fixture : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly MemoryCache _cache = new(new MemoryCacheOptions());
        public DatabaseFactory Factory { get; }
        public IOrderPayments Payments { get; }
        public OrderStateService States { get; }

        public Fixture()
        {
            var connectionString = $"Data Source=order-payments-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
            _connection = new SqliteConnection(connectionString);
            _connection.Open();
            var root = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:umbracoDbDSN"] = connectionString,
                ["ConnectionStrings:umbracoDbDSN_ProviderName"] = "Microsoft.Data.Sqlite",
            }).Build();
            Factory = new DatabaseFactory(root, Mock.Of<IHostEnvironment>(x => x.ContentRootPath == AppContext.BaseDirectory));
            var schema = new DatabaseService(Factory, NullLogger<DatabaseService>.Instance, new StockReservationReadiness());
            schema.CreateTables();
            // Creating the payment tables is repeatable.
            schema.EnsureOrderPaymentTables();
            var configuration = new Configuration(root);
            var handlers = Array.Empty<IOrderStatusPersistenceHandler>();
            Payments = new OrderPaymentService(Factory, configuration, _cache, handlers, NullLogger<OrderPaymentService>.Instance);

            var access = new Mock<IManagerAccessService>();
            access.Setup(x => x.CanAccessManager()).Returns(true);
            access.Setup(x => x.CanAccessStore(It.IsAny<string>())).Returns(true);
            var attempts = new CheckoutPaymentAttemptService(Factory, Mock.Of<IStockReservationService>(),
                new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, Factory), NullLogger<CheckoutPaymentAttemptService>.Instance, _cache);
            var http = new HttpContextAccessor
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new System.Security.Claims.ClaimsPrincipal(new System.Security.Claims.ClaimsIdentity(
                        [new System.Security.Claims.Claim(System.Security.Claims.ClaimTypes.Name, "anna")], "test")),
                },
            };
            States = new OrderStateService(Factory, configuration, access.Object, _cache, attempts, http, handlers);
        }

        public Guid AddOpenOrder(bool migrated = true)
        {
            var id = Guid.NewGuid();
            using var db = Factory.GetDatabase();
            db.Insert(new OrderData
            {
                UniqueId = id,
                StoreAlias = "main",
                OrderNumber = "IS-1042",
                OrderStatusCol = "Pending",
                OrderInfo = "{}",
                TotalAmount = 100m,
                Currency = "is-IS",
                OrderStateCol = migrated ? nameof(OrderStatus.Open) : null,
                PaymentStateCol = migrated ? nameof(PaymentStatus.Pending) : null,
                FulfillmentStateCol = migrated ? nameof(FulfillmentStatus.Unfulfilled) : null,
                StatusMappingVersion = migrated ? 1 : 0,
                StatusRevision = migrated ? 1 : 0,
            });
            return id;
        }

        public NewOrderPayment NewPayment(Guid orderId) => new()
        {
            OrderUniqueId = orderId,
            OrderNumber = "IS-1042",
            StoreAlias = "main",
            ProviderName = "Borgun",
            Amount = 100m,
            Currency = "ISK",
        };

        public void Dispose()
        {
            _cache.Dispose();
            _connection.Dispose();
        }
    }
}
