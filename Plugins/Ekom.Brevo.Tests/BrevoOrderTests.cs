using Ekom.Brevo.Events;
using Ekom.Brevo.Mappers;
using Ekom.Brevo.Models;
using Ekom.Brevo.Services;
using Ekom.Events;
using Ekom.Models;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Diagnostics.CodeAnalysis;
using System.Net;
using Xunit;

namespace Ekom.Brevo.Tests;

public sealed class BrevoOrderTests
{
    internal static Mock<IOrderInfo> Order()
    {
        var currency = new CurrencyModel { CurrencyValue = "en-US", CurrencyFormat = "C" };
        var store = new StoreInfo(Guid.NewGuid(), currency, [currency], "en-US", "shop", true, 20, true);
        var order = new Mock<IOrderInfo>();
        order.SetupGet(x => x.UniqueId).Returns(Guid.Parse("bc86b90b-18cb-4a9f-99a8-d61fd86d3470"));
        order.SetupGet(x => x.StoreInfo).Returns(store);
        order.SetupGet(x => x.OrderNumber).Returns("123");
        order.SetupGet(x => x.GrandTotal).Returns(Amount(125));
        order.SetupGet(x => x.ChargedAmount).Returns(Amount(25));
        order.SetupGet(x => x.ChargedVat).Returns(Amount(20));
        order.SetupGet(x => x.DiscountAmountWithVat).Returns(Amount(5));
        order.SetupGet(x => x.CreateDate).Returns(new DateTime(2026, 11, 1, 12, 0, 0, DateTimeKind.Utc));
        order.SetupGet(x => x.UpdateDate).Returns(new DateTime(2026, 11, 2, 12, 0, 0, DateTimeKind.Utc));
        order.SetupGet(x => x.CustomerInformation).Returns(new CustomerInfo
        {
            Customer = new Customer { Properties = new() { ["customerEmail"] = "customer@example.com", ["customerFirstName"] = "First", ["customerAddress"] = "Street 1" } },
            Shipping = new CustomerShippingInfo { Properties = new() { ["shippingName"] = "Recipient", ["shippingAddress"] = "Street 2" } },
        });
        var price = new Mock<IPrice>();
        price.SetupGet(x => x.WithVat).Returns(Amount(100));
        var line = new Mock<IOrderLine>();
        line.SetupGet(x => x.Quantity).Returns(2.5m);
        line.SetupGet(x => x.ProductKey).Returns(Guid.Parse("af60bfd2-e3ef-49bf-b8fc-a2a5968f918b"));
        line.SetupGet(x => x.Amount).Returns(price.Object);
        line.SetupGet(x => x.Product).Returns(new OrderedProduct { Properties = new Dictionary<string, string> { ["sku"] = "SKU-1" } });
        order.SetupGet(x => x.OrderLines).Returns([line.Object]);
        return order;
    }

    private static ICalculatedPrice Amount(decimal value)
    {
        var price = new Mock<ICalculatedPrice>();
        price.SetupGet(x => x.Value).Returns(value);
        return price.Object;
    }

    [Fact]
    public void MappingPreservesRevenueBeforeGiftCardsAndFractionalQuantities()
    {
        var payload = Order().Object.ToBrevoOrder();
        Assert.Equal(125m, payload["amount"]!.GetValue<decimal>());
        Assert.Equal(40m, payload["products"]![0]!["price"]!.GetValue<decimal>());
        Assert.Equal(2.5m, payload["products"]![0]!["quantityFloat"]!.GetValue<decimal>());
        Assert.Equal("SHOP:af60bfd2e3ef49bfb8fca2a5968f918b", payload["products"]![0]!["productId"]!.GetValue<string>());
        Assert.Equal("USD", payload["metaInfo"]!["currency"]!.GetValue<string>());
        Assert.Equal(5m, payload["metaInfo"]!["discount"]!.GetValue<decimal>());
        Assert.EndsWith("Z", payload["createdAt"]!.GetValue<string>(), StringComparison.Ordinal);
        Assert.Equal("Street 1", payload["billing"]!["address"]!.GetValue<string>());
        Assert.Equal("Street 2", payload["shipping"]!["address"]!.GetValue<string>());
        Assert.Equal("Recipient", payload["shipping"]!["name"]!.GetValue<string>());
    }

    [Fact]
    public async Task DuplicateOrderIsSuppressedBeforeAndAfterAcceptance()
    {
        using var handler = new RecordingHandler();
        using var host = BrevoTests.CreateHost(handler);
        var orders = host.Services.GetRequiredService<IBrevoOrderService>();
        var order = Order().Object;
        var first = await orders.TrackOrderAsync(order);
        Assert.Equal(BrevoSubmissionStatus.Duplicate, (await orders.TrackOrderAsync(order)).Status);
        await host.StartAsync();
        Assert.Equal(BrevoDeliveryStatus.Accepted, (await BrevoTests.Complete(first)).Status);
        Assert.Equal(BrevoSubmissionStatus.Duplicate, (await orders.TrackOrderAsync(order)).Status);
        Assert.Equal("/v3/orders/status", Assert.Single(handler.Requests).Path);
        await host.StopAsync();
    }

    [Fact]
    public async Task UnknownOrderOutcomeIsNotReplayed()
    {
        using var handler = new RecordingHandler(_ => new(HttpStatusCode.InternalServerError));
        using var host = BrevoTests.CreateHost(handler);
        await host.StartAsync();
        var orders = host.Services.GetRequiredService<IBrevoOrderService>();
        var order = Order().Object;
        Assert.Equal(BrevoDeliveryStatus.Unknown, (await BrevoTests.Complete(await orders.TrackOrderAsync(order))).Status);
        Assert.Equal(BrevoSubmissionStatus.Duplicate, (await orders.TrackOrderAsync(order)).Status);
        Assert.Single(handler.Requests);
        await host.StopAsync();
    }

    [Fact]
    public async Task DefinitiveOrderRejectionAllowsExplicitRetry()
    {
        int calls = 0;
        using var handler = new RecordingHandler(_ => new(++calls == 1 ? HttpStatusCode.BadRequest : HttpStatusCode.NoContent));
        using var host = BrevoTests.CreateHost(handler);
        await host.StartAsync();
        var orders = host.Services.GetRequiredService<IBrevoOrderService>();
        var order = Order().Object;
        Assert.Equal(BrevoDeliveryStatus.Failed, (await BrevoTests.Complete(await orders.TrackOrderAsync(order))).Status);
        Assert.Equal(BrevoDeliveryStatus.Accepted, (await BrevoTests.Complete(await orders.TrackOrderAsync(order))).Status);
        Assert.Equal(2, handler.Requests.Count);
        await host.StopAsync();
    }

    [Theory]
    [InlineData(true, true, 1, 1)]
    [InlineData(true, false, 1, 0)]
    [InlineData(false, true, 0, 1)]
    [InlineData(false, false, 0, 0)]
    [SuppressMessage("Maintainability", "CA1506", Justification = "Integration test intentionally exercises the checkout component, DI configuration, and service contracts together.")]
    public async Task CheckoutSubscriptionAndOrderFlagsAreIndependent(bool consent, bool trackOrders, int subscriptions, int orders)
    {
        using var handler = new RecordingHandler();
        using var host = BrevoTests.CreateHost(handler, o => o.Orders.TrackCompletedCheckouts = trackOrders);
        var contacts = new Mock<IBrevoContactsService>();
        var orderService = new Mock<IBrevoOrderService>();
        contacts.Setup(x => x.SubscribeAsync(It.IsAny<BrevoContact>(), null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BrevoSubmission(BrevoSubmissionStatus.Queued, "contact"));
        orderService.Setup(x => x.TrackOrderAsync(It.IsAny<IOrderInfo>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BrevoSubmission(BrevoSubmissionStatus.Queued, "order"));
        var services = new ServiceCollection();
        services.AddSingleton(contacts.Object);
        services.AddSingleton(orderService.Object);
        services.AddSingleton(host.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<BrevoOptions>>());
        using var provider = services.BuildServiceProvider();
        var component = new BrevoEkomEvents(provider.GetRequiredService<IServiceScopeFactory>(), NullLogger<BrevoEkomEvents>.Instance);
        var order = Order().Object;
        order.CustomerInformation.Customer.Properties["customerBrevoConsentToSubscribeEmail"] = consent ? "true" : "false";
        await component.OnCompleteCheckoutAsync(this, new CompleteCheckoutEventArgs { OrderInfo = order, OrderData = new() }, CancellationToken.None);
        contacts.Verify(x => x.SubscribeAsync(It.IsAny<BrevoContact>(), null, It.IsAny<CancellationToken>()), Times.Exactly(subscriptions));
        contacts.Verify(x => x.UnsubscribeAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        orderService.Verify(x => x.TrackOrderAsync(order, It.IsAny<CancellationToken>()), Times.Exactly(orders));
    }
}
