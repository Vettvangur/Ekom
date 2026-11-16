using Ekom.Shipping;
using Xunit;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Tests.Tests;

public sealed class ShippingSelectionValidatorTests
{
    [Fact]
    public void BuildOrderData_ReplacesClientPickupSnapshot()
    {
        var services = new ServiceCollection();
        services.AddEkomShipping();
        using var provider = services.BuildServiceProvider();
        var checkout = provider.GetRequiredService<IEkomShippingCheckoutService>();
        var selection = new ValidatedShippingSelection(
            new ShippingSelection("fake", "main", "pickup", "location-1"),
            new ShippingService("pickup", "Pickup", true),
            new PickupLocation("location-1", "Trusted name", "Street 1", "101", "Reykjavík", ExternalId: "EXT1"));

        var data = checkout.BuildOrderData(selection, new Dictionary<string, string>
        {
            [EkomShippingPropertyAliases.CustomPickupLocationName.ToUpperInvariant()] = "Untrusted name",
            ["customerEmail"] = "customer@example.test",
        });

        Assert.Equal("Trusted name", data[EkomShippingPropertyAliases.CustomPickupLocationName]);
        Assert.Equal("EXT1", data[EkomShippingPropertyAliases.CustomPickupLocationExternalId]);
        Assert.Equal("customer@example.test", data["customerEmail"]);
        Assert.Single(data.Keys.Where(x => string.Equals(
            x,
            EkomShippingPropertyAliases.CustomPickupLocationName,
            StringComparison.OrdinalIgnoreCase)));
    }

    [Fact]
    public async Task ValidateAsync_ReturnsServerLocationSnapshot()
    {
        var services = new ServiceCollection();
        services.AddEkomShippingCore();
        services.AddSingleton<IShippingCarrier>(new FakeCarrier());
        await using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IShippingSelectionValidator>();

        var result = await validator.ValidateAsync(
            new ShippingSelection("fake", "main", "pickup", "location-1"),
            new ShippingLookupRequest("store", "IS", "101"));

        Assert.Equal("Server supplied name", result.PickupLocation?.Name);
    }

    [Fact]
    public async Task ValidateAsync_RejectsUnknownLocation()
    {
        var services = new ServiceCollection();
        services.AddEkomShippingCore();
        services.AddSingleton<IShippingCarrier>(new FakeCarrier());
        await using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IShippingSelectionValidator>();

        await Assert.ThrowsAsync<InvalidShippingSelectionException>(() => validator.ValidateAsync(
            new ShippingSelection("fake", "main", "pickup", "unknown"),
            new ShippingLookupRequest("store", "IS", "101")));
    }

    [Fact]
    public async Task ValidateAsync_UnavailableServiceMessageIncludesLookupContext()
    {
        var services = new ServiceCollection();
        services.AddEkomShippingCore();
        services.AddSingleton<IShippingCarrier>(new FakeCarrier());
        await using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IShippingSelectionValidator>();

        var exception = await Assert.ThrowsAsync<InvalidShippingSelectionException>(() => validator.ValidateAsync(
            new ShippingSelection("fake", "main", "home-delivery"),
            new ShippingLookupRequest("store", "IS", "999")));

        Assert.Equal(
            "Service 'home-delivery' is not available from 'fake' " +
            "(account 'main', store 'store', country 'IS', postal code '999'). " +
            "Available services: pickup.",
            exception.Message);
    }

    [Fact]
    public void Registry_RejectsDuplicateAliases()
    {
        var services = new ServiceCollection();
        services.AddEkomShippingCore();
        services.AddSingleton<IShippingCarrier>(new FakeCarrier());
        services.AddSingleton<IShippingCarrier>(new FakeCarrier());
        using var provider = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(
            () => provider.GetRequiredService<IShippingCarrierRegistry>());
    }

    private sealed class FakeCarrier : IShippingCarrier
    {
        public string Alias => "fake";

        public ShippingCarrierCapabilities Capabilities =>
            ShippingCarrierCapabilities.Services | ShippingCarrierCapabilities.PickupLocations;

        public Task<IReadOnlyList<ShippingService>> GetServicesAsync(
            string accountReference,
            ShippingLookupRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<ShippingService> services =
            [
                new ShippingService("pickup", "Pickup", true),
            ];
            return Task.FromResult(services);
        }

        public Task<IReadOnlyList<PickupLocation>> GetPickupLocationsAsync(
            string accountReference,
            string serviceId,
            ShippingLookupRequest request,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<PickupLocation> locations =
            [
                new PickupLocation("location-1", "Server supplied name", "Street 1", "101", "Reykjavík"),
            ];
            return Task.FromResult(locations);
        }
    }
}
