using Ekom.Models;
using Ekom.Models.Shipments;
using Ekom.Shipping;
using LinqToDB;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Ekom.Tests.Tests;

[Collection("Reservations")]
public sealed class ShippingFulfillmentTests
{
    [Fact]
    public async Task BookingStoresTheShipmentAndLeavesTheOrderDataAlone()
    {
        using var fixture = new ShipmentFixture();
        var site = new Site(fixture);

        var record = await site.Fulfillment.CreateAsync(site.OrderId);

        Assert.Equal(ShipmentState.Created, record.State);
        Assert.Equal("CARRIER-1", record.ShipmentId);
        Assert.Equal("TRACK-1", record.TrackingNumber);
        Assert.Equal("BARCODE-1", record.BookingReference);
        Assert.Equal("https://carrier.example/TRACK-1", record.TrackingUrl);
        Assert.Equal(1, record.AttemptCount);
        // Nothing about the shipment is written back to the order.
        Assert.DoesNotContain(site.Order.ShippingProvider!.CustomData.Keys, x => x.Contains("Shipment", StringComparison.OrdinalIgnoreCase));

        var view = await fixture.Shipments.GetForOrderAsync(site.OrderId);
        var current = view.Current!;
        Assert.Equal("Home delivery", current.Provider);
        Assert.Equal("test-carrier", current.CarrierAlias);
        Assert.Equal("Home delivery service", current.ServiceName);
        Assert.Equal("Kringlan", current.PickupLocationName);
        Assert.Equal(["Processing", "Processing", "Submitting", "Created"], current.Events.Select(x => x.State));

        // Asking again returns the same shipment instead of booking another.
        var again = await site.Fulfillment.CreateAsync(site.OrderId);
        Assert.Equal(record.RecordId, again.RecordId);
        Assert.Equal(1, site.Carrier.Created);
    }

    [Fact]
    public async Task FailedBookingIsRetriedOnTheSameShipment()
    {
        using var fixture = new ShipmentFixture();
        var site = new Site(fixture);
        site.Carrier.Failure = new ShippingProviderException("test-carrier", "Carrier rejected the postal code.");

        var failed = await site.Fulfillment.CreateAsync(site.OrderId);

        Assert.Equal(ShipmentState.Failed, failed.State);
        Assert.Equal("Carrier rejected the postal code.", failed.LastError);
        await Assert.ThrowsAsync<ShippingException>(() => site.Fulfillment.CreateAsync(site.OrderId));

        site.Carrier.Failure = null;
        var retried = await site.Fulfillment.RetryAsync(site.OrderId);

        Assert.Equal(ShipmentState.Created, retried.State);
        Assert.Equal(failed.RecordId, retried.RecordId);
        Assert.Equal(2, retried.AttemptCount);
        Assert.Null(retried.LastError);
        // The barcode reserved for the first attempt is reused.
        Assert.Equal(1, site.Carrier.Reserved);
        var view = await fixture.Shipments.GetForOrderAsync(site.OrderId);
        Assert.Single(view.Shipments);
        Assert.Contains(view.Current!.Events, x => x.State == "Failed" && x.Message == "Carrier rejected the postal code.");
    }

    [Fact]
    public async Task UnknownOutcomeIsNotRetried()
    {
        using var fixture = new ShipmentFixture();
        var site = new Site(fixture);
        site.Carrier.Failure = new ShipmentOutcomeUnknownException("The connection dropped.", new IOException("reset"));

        var record = await site.Fulfillment.CreateAsync(site.OrderId);

        Assert.Equal(ShipmentState.OutcomeUnknown, record.State);
        await Assert.ThrowsAsync<ShippingException>(() => site.Fulfillment.CreateAsync(site.OrderId));
        await Assert.ThrowsAsync<ShippingException>(() => site.Fulfillment.RetryAsync(site.OrderId));
    }

    [Fact]
    public async Task DeletedShipmentIsKeptAndANewOneCanBeBooked()
    {
        using var fixture = new ShipmentFixture();
        var site = new Site(fixture);
        var first = await site.Fulfillment.CreateAsync(site.OrderId);

        var deleted = await site.Fulfillment.DeleteAsync(site.OrderId);

        Assert.Equal(ShipmentState.Deleted, deleted.State);
        Assert.Equal(["CARRIER-1"], site.Carrier.Deleted);

        var second = await site.Fulfillment.CreateAsync(site.OrderId);

        Assert.Equal(ShipmentState.Created, second.State);
        Assert.NotEqual(first.RecordId, second.RecordId);
        Assert.Equal("CARRIER-2", second.ShipmentId);
        var view = await fixture.Shipments.GetForOrderAsync(site.OrderId);
        Assert.Equal(["Created", "Deleted"], view.Shipments.Select(x => x.State));
        Assert.Equal(second.RecordId, view.Current!.ShipmentId);

        using var db = fixture.Factory.GetDatabase();
        var logs = await db.OrderActivityLog.Where(x => x.Key == site.OrderId).Select(x => x.Log).ToListAsync();
        Assert.Contains("Shipment created with test-carrier. Tracking number: TRACK-1.", logs);
        Assert.Contains("Shipment deleted from test-carrier. Tracking number: TRACK-1.", logs);
        Assert.Contains("Shipment created with test-carrier. Tracking number: TRACK-2.", logs);
    }

    [Fact]
    public async Task ShipmentStoredOnTheOrderIsCopiedOnce()
    {
        using var fixture = new ShipmentFixture();
        var site = new Site(fixture, new Dictionary<string, string>
        {
            ["customshippingShipmentState"] = "Created",
            ["customshippingShipmentId"] = "OLD-ID",
            ["customshippingTrackingNumber"] = "OLD-TRACK",
            ["customshippingShipmentReference"] = "OLD-REF",
            ["customshippingShipmentAttempts"] = "2",
            ["customshippingShipmentUpdatedUtc"] = "2026-05-01T12:00:00.0000000Z",
            ["customshippingCarrierAlias"] = "test-carrier",
            ["customshippingAccountReference"] = "main",
            ["customshippingServiceId"] = "home-delivery",
        });

        var record = await site.Fulfillment.GetAsync(site.OrderId);
        var again = await site.Fulfillment.GetAsync(site.OrderId);

        Assert.NotNull(record);
        Assert.Equal(ShipmentState.Created, record!.State);
        Assert.Equal("OLD-ID", record.ShipmentId);
        Assert.Equal("OLD-TRACK", record.TrackingNumber);
        Assert.Equal("OLD-REF", record.BookingReference);
        Assert.Equal(2, record.AttemptCount);
        Assert.Equal(record.RecordId, again!.RecordId);

        var view = await fixture.Shipments.GetForOrderAsync(site.OrderId);
        var shipment = Assert.Single(view.Shipments);
        Assert.Equal(new DateTime(2026, 5, 1, 12, 0, 0, DateTimeKind.Utc), shipment.CreatedUtc);
        using var db = fixture.Factory.GetDatabase();
        Assert.False(await db.OrderActivityLog.AnyAsync(x => x.Key == site.OrderId));
        Assert.Equal(0, site.Carrier.Created);
    }

    [Fact]
    public async Task OrderWithoutAShipmentHasNone()
    {
        using var fixture = new ShipmentFixture();
        var site = new Site(fixture);

        Assert.Null(await site.Fulfillment.GetAsync(site.OrderId));
        Assert.Empty((await fixture.Shipments.GetForOrderAsync(site.OrderId)).Shipments);
    }

    private sealed class Site
    {
        public Guid OrderId { get; } = Guid.NewGuid();
        public IOrderInfo Order { get; }
        public FakeCarrier Carrier { get; } = new();
        public IShippingFulfillmentService Fulfillment { get; }

        public Site(ShipmentFixture fixture, Dictionary<string, string>? shipmentData = null)
        {
            var storeInfo = new StoreInfo(Guid.NewGuid(), new CurrencyModel { CurrencyValue = "is-IS" }, [], "is-IS", "main", true, 0, true);
            var customData = new Dictionary<string, string>
            {
                ["customshippingServiceName"] = "Home delivery service",
                ["customshippingPickupLocationId"] = "42",
                ["customshippingPickupLocationName"] = "Kringlan",
            };
            foreach (var pair in shipmentData ?? [])
            {
                customData[pair.Key] = pair.Value;
            }
            var provider = new OrderedShippingProvider(JObject.FromObject(new
            {
                Id = 1,
                Key = Guid.NewGuid(),
                Title = "Home delivery",
                Properties = new Dictionary<string, string>
                {
                    ["shippingCarrierAlias"] = "test-carrier",
                    ["shippingCarrierAccount"] = "main",
                    ["shippingCarrierService"] = "home-delivery",
                },
                CustomData = customData,
                Price = (object?)null,
            }), storeInfo);

            var order = new Mock<IOrderInfo>();
            order.SetupGet(x => x.UniqueId).Returns(OrderId);
            order.SetupGet(x => x.OrderNumber).Returns("IS-1042");
            order.SetupGet(x => x.StoreInfo).Returns(storeInfo);
            order.SetupGet(x => x.CreateDate).Returns(new DateTime(2026, 4, 30, 9, 0, 0, DateTimeKind.Utc));
            order.SetupGet(x => x.UpdateDate).Returns(new DateTime(2026, 4, 30, 9, 5, 0, DateTimeKind.Utc));
            order.SetupProperty(x => x.ShippingProvider, provider);
            Order = order.Object;

            var reader = new Mock<IShippingOrderReader>();
            reader.Setup(x => x.GetOrderAsync(OrderId, It.IsAny<CancellationToken>())).ReturnsAsync(Order);
            var mapper = new Mock<IShippingOrderMapper>();
            mapper.Setup(x => x.Map(It.IsAny<IOrderInfo>(), It.IsAny<EkomShippingMethodConfiguration>(), It.IsAny<string?>()))
                .Returns((IOrderInfo _, EkomShippingMethodConfiguration configuration, string? reference) => new ShipmentBookingRequest(
                    "IS-1042", configuration.ServiceId,
                    new ShipmentRecipient("Anna", "anna@example.com", "5551234", "Laugavegur 1", "101", "Reykjavik", "IS"),
                    [new ShipmentItem("SKU-1", "Shirt", 1)], 100m, "ISK", BookingReference: reference));

            Fulfillment = new ShippingFulfillmentService(
                reader.Object,
                new ShippingFulfillmentCarrierRegistry([Carrier]),
                mapper.Object,
                [],
                [],
                fixture.Shipments,
                NullLogger<ShippingFulfillmentService>.Instance);
        }
    }

    private sealed class FakeCarrier : IShippingFulfillmentCarrier, IShippingShipmentDeletionCarrier
    {
        public string Alias => "test-carrier";
        public ShippingCarrierCapabilities Capabilities => ShippingCarrierCapabilities.ShipmentBooking | ShippingCarrierCapabilities.ShipmentDeletion;
        public Exception? Failure { get; set; }
        public int Created { get; private set; }
        public int Reserved { get; private set; }
        public List<string> Deleted { get; } = [];

        public ShippingFulfillmentMode GetFulfillmentMode(string accountReference) => ShippingFulfillmentMode.Manual;

        public Task<string?> ReserveBookingReferenceAsync(string accountReference, CancellationToken cancellationToken = default)
        {
            Reserved++;
            return Task.FromResult<string?>($"BARCODE-{Reserved}");
        }

        public Task<ShipmentBookingResult> CreateShipmentAsync(string accountReference, ShipmentBookingRequest request, CancellationToken cancellationToken = default)
        {
            if (Failure != null) throw Failure;
            Created++;
            return Task.FromResult(new ShipmentBookingResult($"CARRIER-{Created}", request.BookingReference, $"TRACK-{Created}")
            {
                TrackingUrl = $"https://carrier.example/TRACK-{Created}",
            });
        }

        public Task<ShippingLabel> GetLabelAsync(string accountReference, string shipmentId, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<ShippingTrackingResult> GetTrackingAsync(string accountReference, string trackingNumber, string language, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task DeleteShipmentAsync(string accountReference, string shipmentId, CancellationToken cancellationToken = default)
        {
            Deleted.Add(shipmentId);
            return Task.CompletedTask;
        }
    }
}
