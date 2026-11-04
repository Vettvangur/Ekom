using Ekom.Models;
using Ekom.Printer.Configuration;
using Ekom.Printer.Migrations;
using Ekom.Printer.Rendering;
using Ekom.Printer.Services;
using Ekom.Printer.Storage;
using Ekom.Utilities;
using LinqToDB;
using Microsoft.Extensions.Options;
using System.Text.Json;
using Xunit;

namespace Ekom.Printer.Tests;

public sealed class StatusCaptureTests
{
    [Fact]
    public async Task CaptureSurvivesRestartAndMaterializesOriginalOrderOnlyOnce()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        var options = Options.Create(Settings());
        var order = Order();
        await using (var db = fixture.Factory.GetDatabase())
        {
            await using var transaction = await db.BeginTransactionAsync();
            await new PrinterStatusCapture(options, TimeProvider.System).PersistAsync(db, order, "Incomplete");
            await transaction.CommitAsync();
        }
        order.OrderNumber = "changed-after-capture";
        // A new processor simulates recovery without receiving a post-save event.
        var jobs = new PrinterJobStore(fixture.Factory);
        var processor = new CapturedStatusStore(fixture.Factory, jobs, TimeProvider.System);
        var captured = Assert.Single(await processor.GetPendingAsync());
        await processor.MaterializeAsync(captured, new TestContextFactory());
        await processor.MaterializeAsync(captured, new TestContextFactory()); // crash/replay after job insert
        var job = Assert.Single(await jobs.GetPendingAsync());
        Assert.Equal("original", JsonSerializer.Deserialize<PickingSlipContext>(job.SnapshotJson!)!.OrderNumber);
        Assert.Empty(await processor.GetPendingAsync());
    }

    [Fact]
    public async Task CaptureRollbackAndDisabledConfigurationCreateNoPendingWork()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        var settings = Settings();
        await using var db = fixture.Factory.GetDatabase();
        await using (var transaction = await db.BeginTransactionAsync())
        {
            await new PrinterStatusCapture(Options.Create(settings), TimeProvider.System).PersistAsync(db, Order(), "Incomplete");
            await transaction.RollbackAsync();
        }
        settings.Enabled = false;
        await new PrinterStatusCapture(Options.Create(settings), TimeProvider.System).PersistAsync(db, Order(), "Incomplete");
        Assert.Empty(await db.GetTable<CapturedStatus>().ToListAsync());
    }

    [Theory]
    [InlineData(PrintPolicy.OncePerOrder, 1)]
    [InlineData(PrintPolicy.OncePerStatus, 2)]
    public async Task CapturedTransitionsRespectConfiguredDuplicatePolicy(PrintPolicy policy, int expected)
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        var settings = Settings();
        settings.Stations[0].PickingSlip.PrintPolicy = policy;
        var capture = new PrinterStatusCapture(Options.Create(settings), TimeProvider.System);
        var order = Order();
        await using var db = fixture.Factory.GetDatabase();
        await using var transaction = await db.BeginTransactionAsync();
        await capture.PersistAsync(db, order, "Incomplete");
        order.OrderStatus = OrderStatus.ReadyForDispatch;
        await capture.PersistAsync(db, order, "OfflinePayment");
        await capture.PersistAsync(db, order, "ReadyForDispatch");
        await transaction.CommitAsync();
        Assert.Equal(expected, await db.GetTable<CapturedStatus>().CountAsync());
    }

    [Fact]
    public async Task FactoryFailureRetainsCapturedOrderForLaterRetry()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        await using var db = fixture.Factory.GetDatabase();
        await new PrinterStatusCapture(Options.Create(Settings()), TimeProvider.System).PersistAsync(db, Order(), "Incomplete");
        var store = new CapturedStatusStore(fixture.Factory, new PrinterJobStore(fixture.Factory), TimeProvider.System);
        var captured = Assert.Single(await store.GetPendingAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => store.MaterializeAsync(captured, new TestContextFactory { Fail = true }));
        await store.DeferAsync(captured.Id);
        Assert.Empty(await store.GetPendingAsync());
        Assert.Equal("SnapshotCreationFailed", (await db.GetTable<CapturedStatus>().SingleAsync()).ErrorCode);
        Assert.Empty(await db.GetTable<PrintJob>().ToListAsync());
    }

    [Fact]
    public async Task DefaultFactoryReconstructsSavedOrderAndRendersHtml()
    {
        using var fixture = new PrinterTests.DatabaseFixture();
        await new PrinterMigration(fixture.Factory).ApplyAsync();
        var order = Order();
        order.OrderInfo = JsonSerializer.Serialize(new
        {
            StoreInfo = new
            {
                Key = Guid.NewGuid(), Alias = "main", Culture = "en-US", VatIncludedInPrice = false, Vat = 0,
                Currency = new { CurrencyValue = "en-US", CurrencyFormat = "C" }, Currencies = Array.Empty<object>(),
            },
            OrderLines = Array.Empty<object>(),
        });
        await using var db = fixture.Factory.GetDatabase();
        await new PrinterStatusCapture(Options.Create(Settings()), TimeProvider.System).PersistAsync(db, order, "Incomplete");
        var jobs = new PrinterJobStore(fixture.Factory);
        var captures = new CapturedStatusStore(fixture.Factory, jobs, TimeProvider.System);
        await captures.MaterializeAsync(Assert.Single(await captures.GetPendingAsync()), new PickingSlipContextFactory());
        var job = Assert.Single(await jobs.GetPendingAsync());
        var context = JsonSerializer.Deserialize<PickingSlipContext>(job.SnapshotJson!)!;
        await jobs.SaveHtmlAsync(job.Id, await new DefaultPickingSlipHtmlRenderer().RenderAsync(context));
        var rendered = await db.GetTable<PrintJob>().SingleAsync();
        Assert.Equal(PrintJobState.Ready, rendered.State);
        Assert.Contains("original", rendered.Html, StringComparison.Ordinal);
        Assert.Empty(await captures.GetPendingAsync());
    }

    private static PrinterOptions Settings() => new()
    {
        Enabled = true,
        Stations = [new PrinterStationOptions
        {
            Alias = "warehouse", StoreAliases = ["main"],
            PickingSlip = new PickingSlipOptions { TriggerStatuses = [OrderStatus.OfflinePayment, OrderStatus.ReadyForDispatch] },
        }],
    };

    private static OrderData Order() => new()
    {
        UniqueId = Guid.NewGuid(), OrderNumber = "original", StoreAlias = "main", Currency = "USD",
        OrderStatus = OrderStatus.OfflinePayment, OrderInfo = string.Empty,
    };

    private sealed class TestContextFactory : IPickingSlipContextFactory
    {
        public bool Fail { get; init; }
        public PickingSlipContext Create(IOrderInfo order, string stationAlias, OrderStatus triggerStatus)
        {
            if (Fail) throw new InvalidOperationException("Simulated template data failure.");
            return new(order.UniqueId, order.OrderNumber, "main", stationAlias, triggerStatus, DateTime.UtcNow, []);
        }
    }
}
