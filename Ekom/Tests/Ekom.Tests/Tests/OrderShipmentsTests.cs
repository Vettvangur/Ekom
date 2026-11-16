using Ekom.Events;
using Ekom.Models;
using Ekom.Models.Shipments;
using Ekom.Services;
using LinqToDB;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class OrderShipmentsTests
{
    [Fact]
    public async Task CreatedShipmentKeepsItsHistoryAndLogsOnce()
    {
        using var fixture = new ShipmentFixture();
        var orderId = Guid.NewGuid();
        var recorded = new List<ShipmentRecordedEventArgs>();
        Func<object, ShipmentRecordedEventArgs, CancellationToken, Task> handler = (_, args, _) => { recorded.Add(args); return Task.CompletedTask; };
        OrderEvents.ShipmentRecordedAsync += handler;
        try
        {
            var shipment = await fixture.Shipments.CreateAsync(fixture.NewShipment(orderId));
            await fixture.Shipments.RecordAsync(new ShipmentUpdate { ShipmentId = shipment.ShipmentId, State = ShipmentState.Submitting, Attempts = 1 });
            await fixture.Shipments.RecordAsync(new ShipmentUpdate
            {
                ShipmentId = shipment.ShipmentId,
                State = ShipmentState.Failed,
                Error = "Carrier rejected the postal code.",
            });
            await fixture.Shipments.RecordAsync(new ShipmentUpdate { ShipmentId = shipment.ShipmentId, State = ShipmentState.Submitting, Attempts = 2 });
            var created = await fixture.Shipments.RecordAsync(new ShipmentUpdate
            {
                ShipmentId = shipment.ShipmentId,
                State = ShipmentState.Created,
                TrackingNumber = "1Z999AA10123456784",
                TrackingUrl = "https://carrier.example/track/1Z999AA10123456784",
                CarrierShipmentId = "SHIP-1",
                Details = [new ShipmentDetail("awb", "AWB", "1Z999AA10123456784", true)],
            });

            Assert.True(created.Recorded);
            Assert.Equal(ShipmentState.Created, created.Shipment.State);
            Assert.Null(created.Shipment.LastError);
            Assert.Equal(2, created.Shipment.Attempts);

            var view = await fixture.Shipments.GetForOrderAsync(orderId);
            Assert.NotNull(view.Current);
            Assert.Equal("Created", view.Current!.State);
            Assert.Equal("1Z999AA10123456784", view.Current.TrackingNumber);
            Assert.Equal("Home delivery", view.Current.Provider);
            Assert.Equal("AWB", Assert.Single(view.Current.Details).Label);
            Assert.Equal(["Processing", "Submitting", "Failed", "Submitting", "Created"], view.Current.Events.Select(x => x.State));
            Assert.Equal("Carrier rejected the postal code.", view.Current.Events[2].Message);

            using var db = fixture.Factory.GetDatabase();
            var logs = await db.OrderActivityLog.Where(x => x.Key == orderId).OrderBy(x => x.Date).ToListAsync();
            // Steps on the way to the carrier are history, not activity log entries.
            Assert.Equal(2, logs.Count);
            Assert.Contains(logs, x => x.LogType == OrderActivityLogType.Alert
                && x.Log == "Shipment creation entered state Failed: Carrier rejected the postal code.");
            Assert.Contains(logs, x => x.LogType == OrderActivityLogType.Success
                && x.Log == "Shipment created with dropp. Tracking number: 1Z999AA10123456784.");

            var own = recorded.Where(x => x.Shipment.ShipmentId == shipment.ShipmentId).ToList();
            Assert.Equal(5, own.Count);
            Assert.Equal(ShipmentState.Submitting, own[^1].PreviousState);
        }
        finally
        {
            OrderEvents.ShipmentRecordedAsync -= handler;
        }
    }

    [Fact]
    public async Task RepeatedCarrierUpdateIsStoredOnce()
    {
        using var fixture = new ShipmentFixture();
        var orderId = Guid.NewGuid();
        var shipment = await fixture.Shipments.CreateAsync(fixture.NewShipment(orderId, ShipmentState.Created));
        var update = new ShipmentUpdate
        {
            ShipmentId = shipment.ShipmentId,
            Type = ShipmentEventType.Tracking,
            CarrierStatus = "delivered",
            CarrierStatusText = "Delivered",
            Location = "Reykjavik",
            DeliveredUtc = new DateTime(2026, 11, 10, 9, 30, 0, DateTimeKind.Utc),
            IdempotencyKey = $"{shipment.ShipmentId:N}:delivered",
        };

        var first = await fixture.Shipments.RecordAsync(update);
        var second = await fixture.Shipments.RecordAsync(update);

        Assert.True(first.Recorded);
        Assert.False(second.Recorded);
        var current = (await fixture.Shipments.GetForOrderAsync(orderId)).Current!;
        Assert.Equal("Created", current.State);
        Assert.Equal("delivered", current.CarrierStatus);
        Assert.Equal(new DateTime(2026, 11, 10, 9, 30, 0, DateTimeKind.Utc), current.DeliveredUtc);
        Assert.Equal(2, current.Events.Count);
        Assert.Equal("Reykjavik", current.Events[1].Location);
    }

    [Fact]
    public async Task DeletedShipmentIsKeptAndANewOneBecomesCurrent()
    {
        using var fixture = new ShipmentFixture();
        var orderId = Guid.NewGuid();
        var first = await fixture.Shipments.CreateAsync(fixture.NewShipment(orderId, ShipmentState.Created) with { TrackingNumber = "OLD-1" });
        await fixture.Shipments.RecordAsync(new ShipmentUpdate { ShipmentId = first.ShipmentId, State = ShipmentState.Deleted });

        Assert.Equal(first.ShipmentId, (await fixture.Shipments.GetCurrentAsync(orderId))!.ShipmentId);

        var second = await fixture.Shipments.CreateAsync(fixture.NewShipment(orderId, ShipmentState.Created) with { TrackingNumber = "NEW-1" });

        var view = await fixture.Shipments.GetForOrderAsync(orderId);
        Assert.Equal(second.ShipmentId, view.Current!.ShipmentId);
        Assert.Equal(["NEW-1", "OLD-1"], view.Shipments.Select(x => x.TrackingNumber));
        Assert.Equal("Deleted", view.Shipments[1].State);

        var current = await fixture.Shipments.GetCurrentShipmentsAsync([orderId, Guid.NewGuid()]);
        Assert.Equal("NEW-1", Assert.Single(current).Value.TrackingNumber);
    }

    [Fact]
    public async Task ManualTrackingChangeIsLoggedWithTheUser()
    {
        using var fixture = new ShipmentFixture();
        var orderId = Guid.NewGuid();
        var shipment = await fixture.Shipments.CreateAsync(new NewOrderShipment
        {
            OrderUniqueId = orderId,
            ProviderName = "Courier",
            State = ShipmentState.Created,
            TrackingNumber = "AB-1",
            Actor = "anna",
        });

        await fixture.Shipments.RecordAsync(new ShipmentUpdate
        {
            ShipmentId = shipment.ShipmentId,
            Type = ShipmentEventType.Manual,
            TrackingNumber = "AB-2",
            TrackingUrl = string.Empty,
            Actor = "anna",
        });

        var stored = await fixture.Shipments.GetAsync(shipment.ShipmentId);
        Assert.Equal("AB-2", stored!.TrackingNumber);
        Assert.Null(stored.TrackingUrl);
        using var db = fixture.Factory.GetDatabase();
        var log = await db.OrderActivityLog.SingleAsync(x => x.Key == orderId && x.Log.StartsWith("Shipment tracking"));
        Assert.Equal("Shipment tracking number set to AB-2.", log.Log);
        Assert.Equal("anna", log.UserName);
    }

    [Fact]
    public async Task ImportedShipmentAddsNoActivityLog()
    {
        using var fixture = new ShipmentFixture();
        var orderId = Guid.NewGuid();
        var created = new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc);

        var shipment = await fixture.Shipments.CreateAsync(fixture.NewShipment(orderId, ShipmentState.Created) with
        {
            TrackingNumber = "LEGACY-1",
            CreatedUtc = created,
            Imported = true,
        });

        Assert.Equal(created, shipment.CreatedUtc);
        using var db = fixture.Factory.GetDatabase();
        Assert.False(await db.OrderActivityLog.AnyAsync(x => x.Key == orderId));
    }

    [Fact]
    public async Task FindsOrderByTrackingNumberCarrierIdAndReference()
    {
        using var fixture = new ShipmentFixture();
        var orderId = Guid.NewGuid();
        var shipment = await fixture.Shipments.CreateAsync(fixture.NewShipment(orderId, ShipmentState.Created) with
        {
            TrackingNumber = "TRACK-9",
            CarrierShipmentId = "CARRIER-9",
            ShipmentReference = "REF-9",
        });

        Assert.Equal(orderId, await fixture.Shipments.FindOrderAsync(" TRACK-9 "));
        Assert.Equal(orderId, await fixture.Shipments.FindOrderAsync("CARRIER-9"));
        Assert.Equal(orderId, await fixture.Shipments.FindOrderAsync("REF-9"));
        Assert.Equal(orderId, await fixture.Shipments.FindOrderAsync(shipment.ShipmentId.ToString()));
        Assert.Null(await fixture.Shipments.FindOrderAsync("missing"));
    }

    [Fact]
    public async Task UnknownShipmentThrows()
    {
        using var fixture = new ShipmentFixture();
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Shipments.RecordAsync(
            new ShipmentUpdate { ShipmentId = Guid.NewGuid(), State = ShipmentState.Created }));
    }
}

internal sealed class ShipmentFixture : IDisposable
{
    private readonly SqliteConnection _connection;
    public DatabaseFactory Factory { get; }
    public IOrderShipments Shipments { get; }

    public ShipmentFixture()
    {
        var connectionString = $"Data Source=order-shipments-{Guid.NewGuid():N};Mode=Memory;Cache=Shared;Pooling=False";
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
        // Creating the shipment tables is repeatable.
        schema.EnsureOrderShipmentTables();
        Shipments = new OrderShipmentService(Factory, NullLogger<OrderShipmentService>.Instance);
    }

    public NewOrderShipment NewShipment(Guid orderId, ShipmentState state = ShipmentState.Processing) => new()
    {
        OrderUniqueId = orderId,
        OrderNumber = "IS-1042",
        StoreAlias = "main",
        State = state,
        ProviderName = "Home delivery",
        CarrierAlias = "dropp",
        AccountReference = "main",
        ServiceId = "home-delivery",
    };

    public void Dispose() => _connection.Dispose();
}
