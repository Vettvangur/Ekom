using Ekom.Models;
using Ekom.Payments;
using Ekom.Repositories;
using Ekom.Services;
using Ekom.Umb.Services;
using LinqToDB;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Newtonsoft.Json;
using System.Reflection;
using Xunit;
using ReservationDatabase = Ekom.Tests.Tests.StockReservationTests.ReservationDatabase;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class PaymentSuccessLogTests
{
    [Theory]
    [InlineData(false, "Straumur")]
    [InlineData(false, "Borgun")]
    [InlineData(false, "PayPal")]
    [InlineData(true, "Bank transfer")]
    public async Task SuccessIsPersistedDirectlyAndLoggedAtInformation(bool offline, string provider)
    {
        using var fixture = new ReservationDatabase();
        using var db = fixture.Factory.GetDatabase();
        db.CreateTable<OrderActivityLog>();
        var logger = new TestLogger<PaymentSuccessLogService>();
        var service = CreateService(fixture, logger);
        var orderId = Guid.NewGuid();

        await service.LogAsync(orderId, "IS-1234", 15000.50m, "ISK", provider, offline);

        // No dispatcher is registered or running. Awaiting LogAsync means the row is committed.
        var row = await db.GetTable<OrderActivityLog>().SingleAsync();
        Assert.Equal(orderId, row.Key);
        Assert.Equal(OrderActivityLogType.Success, row.LogType);
        Assert.Equal("Customer", row.UserName);
        Assert.StartsWith(offline ? "Offline Payment Successfull." : "Payment successful.", row.Log, StringComparison.Ordinal);
        Assert.Contains("15000.50 ISK", row.Log, StringComparison.Ordinal);
        Assert.Contains(provider, row.Log, StringComparison.Ordinal);
        Assert.Contains("IS-1234", row.Log, StringComparison.Ordinal);
        Assert.Contains(orderId.ToString(), row.Log, StringComparison.Ordinal);

        var entry = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Null(entry.Exception);
        Assert.Contains(provider, entry.Message, StringComparison.Ordinal);
        Assert.Contains("IS-1234", entry.Message, StringComparison.Ordinal);
        Assert.Contains(orderId.ToString(), entry.Message, StringComparison.Ordinal);
        Assert.Equal(15000.50m, entry.Fields["Amount"]);
        Assert.Equal("ISK", entry.Fields["Currency"]);
        Assert.Equal(provider, entry.Fields["PaymentProvider"]);
        Assert.Equal("IS-1234", entry.Fields["OrderNumber"]);
        Assert.Equal(orderId, entry.Fields["OrderUniqueId"]);
    }

    [Fact]
    public async Task DatabaseWriteFailureKeepsInformationLogAndDoesNotThrow()
    {
        using var fixture = new ReservationDatabase();
        // Deliberately omit the activity-log table to force the actual database insert to fail.
        var logger = new TestLogger<PaymentSuccessLogService>();
        var service = CreateService(fixture, logger);
        var orderId = Guid.NewGuid();

        await service.LogAsync(orderId, "IS-1234", 15000m, "ISK", "Straumur");

        Assert.Equal(2, logger.Entries.Count);
        Assert.Equal(LogLevel.Information, logger.Entries[0].Level);
        Assert.Equal(orderId, logger.Entries[0].Fields["OrderUniqueId"]);
        Assert.Equal(LogLevel.Error, logger.Entries[1].Level);
        Assert.NotNull(logger.Entries[1].Exception);
        Assert.Equal(orderId, logger.Entries[1].Fields["OrderUniqueId"]);
    }

    [Theory]
    [InlineData("Straumur")]
    [InlineData(null)]
    public async Task PaymentSuccessHandlerPersistsBeforeCheckoutCanFail(string? providerName)
    {
        using var fixture = new ReservationDatabase();
        using var db = fixture.Factory.GetDatabase();
        db.CreateTable<OrderActivityLog>();
        var logger = new TestLogger<PaymentSuccessLogService>();
        var paymentLog = CreateService(fixture, logger);
        // CheckoutService is intentionally missing: resolution fails after the payment log is written.
        using var services = new ServiceCollection().AddSingleton(paymentLog).BuildServiceProvider();
        var orderId = Guid.NewGuid();
        var providerKey = Guid.NewGuid();
        var settings = new PaymentSettings
        {
            OrderUniqueId = orderId,
            OrderNumber = "IS-1234",
            PaymentProviderKey = providerKey,
            PaymentProviderName = providerName!,
            Currency = "ISK",
            Store = "main",
            Language = "is-IS",
        };
        settings.OrderCustomData["ekomOrderUniqueId"] = orderId.ToString();
        var args = new SuccessEventArgs
        {
            OrderStatus = new Ekom.Payments.OrderStatus
            {
                Amount = 15000.50m,
                EkomPaymentSettingsData = JsonConvert.SerializeObject(settings),
            },
        };

        await Assert.ThrowsAsync<InvalidOperationException>(() => InvokePaymentSuccessHandlerAsync(services, args));

        var row = await db.GetTable<OrderActivityLog>().SingleAsync();
        Assert.Equal(orderId, row.Key);
        Assert.Contains("15000.50 ISK", row.Log, StringComparison.Ordinal);
        Assert.Contains("IS-1234", row.Log, StringComparison.Ordinal);
        Assert.Contains(providerName ?? providerKey.ToString(), row.Log, StringComparison.Ordinal);
        Assert.Equal(LogLevel.Information, Assert.Single(logger.Entries).Level);
    }

    private static PaymentSuccessLogService CreateService(ReservationDatabase fixture, TestLogger<PaymentSuccessLogService> logger)
        => new(new ActivityLogRepository(NullLogger<ActivityLogRepository>.Instance, fixture.Factory), logger);

    private Task InvokePaymentSuccessHandlerAsync(IServiceProvider services, SuccessEventArgs args)
    {
        var startupType = typeof(ImportService).Assembly.GetType("Ekom.Umb.EkomStartup", throwOnError: true)!;
        var startupLogger = typeof(NullLogger<>).MakeGenericType(startupType).GetField("Instance")!.GetValue(null);
        var startup = Activator.CreateInstance(startupType, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            binder: null, args: [startupLogger, services, null, null, null], culture: null)!;
        var handler = startupType.GetMethod("CompleteCheckoutAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        return (Task)handler.Invoke(startup, [this, args])!;
    }

    private sealed class TestLogger<T> : ILogger<T>
    {
        public List<Entry> Entries { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
            => Entries.Add(new Entry(logLevel, exception, formatter(state, exception),
                ((IEnumerable<KeyValuePair<string, object?>>)(object)state!).ToDictionary(x => x.Key, x => x.Value)));
    }

    private sealed record Entry(LogLevel Level, Exception? Exception, string Message, Dictionary<string, object?> Fields);
}
