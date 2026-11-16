using Ekom.Shipping;
using Xunit;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;

namespace Ekom.Tests.Tests;

public sealed class PickupLocationEndpointTests
{
    [Fact]
    public async Task GetAsync_ReturnsNormalizedLocations()
    {
        var expected = new PickupLocation(
            "location-1",
            "Póstbox",
            "Street 1",
            "101",
            "Reykjavík",
            64.1,
            -21.9);
        var query = new StubPickupLocationQueryService([expected]);
        var controller = CreateController(query);
        var providerKey = Guid.NewGuid();

        var response = await controller.GetAsync(providerKey, "main", "is", "101");

        var result = Assert.IsType<OkObjectResult>(response.Result);
        var locations = Assert.IsAssignableFrom<IReadOnlyList<PickupLocation>>(result.Value);
        Assert.Same(expected, Assert.Single(locations));
        Assert.Equal(providerKey, query.ProviderKey);
        Assert.Equal("main", query.StoreAlias);
        Assert.Equal("IS", query.CountryCode);
        Assert.Equal("101", query.PostalCode);
    }

    [Theory]
    [InlineData("Iceland")]
    [InlineData("ÍS")]
    public async Task GetAsync_RejectsInvalidCountryCodeBeforeLookup(string countryCode)
    {
        var query = new StubPickupLocationQueryService([]);
        var controller = CreateController(query);

        var response = await controller.GetAsync(Guid.NewGuid(), countryCode: countryCode);

        var result = Assert.IsType<BadRequestObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status400BadRequest, problem.Status);
        Assert.Equal(0, query.CallCount);
    }

    [Fact]
    public async Task GetAsync_ReturnsNotFoundForUnknownProvider()
    {
        var query = new StubPickupLocationQueryService((IReadOnlyList<PickupLocation>?)null);
        var controller = CreateController(query);

        var response = await controller.GetAsync(Guid.NewGuid());

        var result = Assert.IsType<NotFoundObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status404NotFound, problem.Status);
    }

    [Fact]
    public async Task GetAsync_ReturnsBadGatewayWhenCarrierFails()
    {
        var query = new StubPickupLocationQueryService(
            new ShippingProviderException("carrier", "Upstream details"));
        var controller = CreateController(query);

        var response = await controller.GetAsync(Guid.NewGuid());

        var result = Assert.IsType<ObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.Equal("Carrier unavailable", problem.Title);
        Assert.DoesNotContain("Upstream details", problem.Detail);
    }

    [Fact]
    public async Task GetAsync_ReturnsBadGatewayWhenCarrierTransportFails()
    {
        var query = new StubPickupLocationQueryService(new HttpRequestException("Transport details"));
        var controller = CreateController(query);

        var response = await controller.GetAsync(Guid.NewGuid());

        var result = Assert.IsType<ObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status502BadGateway, result.StatusCode);
        Assert.DoesNotContain("Transport details", problem.Detail);
    }

    [Fact]
    public async Task GetAsync_ReturnsGatewayTimeoutWhenCarrierTimesOut()
    {
        var query = new StubPickupLocationQueryService(new TaskCanceledException("Timeout details"));
        var controller = CreateController(query);

        var response = await controller.GetAsync(Guid.NewGuid());

        var result = Assert.IsType<ObjectResult>(response.Result);
        var problem = Assert.IsType<ProblemDetails>(result.Value);
        Assert.Equal(StatusCodes.Status504GatewayTimeout, result.StatusCode);
        Assert.DoesNotContain("Timeout details", problem.Detail);
    }

    private static EkomPickupLocationsController CreateController(IEkomPickupLocationQueryService queryService) =>
        new(queryService, NullLogger<EkomPickupLocationsController>.Instance);

    private sealed class StubPickupLocationQueryService : IEkomPickupLocationQueryService
    {
        private readonly IReadOnlyList<PickupLocation>? _locations;
        private readonly Exception? _exception;

        public StubPickupLocationQueryService(IReadOnlyList<PickupLocation>? locations)
        {
            _locations = locations;
        }

        public StubPickupLocationQueryService(Exception exception)
        {
            _exception = exception;
        }

        public int CallCount { get; private set; }
        public Guid ProviderKey { get; private set; }
        public string? StoreAlias { get; private set; }
        public string? CountryCode { get; private set; }
        public string? PostalCode { get; private set; }

        public Task<IReadOnlyList<PickupLocation>?> GetAsync(
            Guid providerKey,
            string? storeAlias,
            string countryCode,
            string? postalCode,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            ProviderKey = providerKey;
            StoreAlias = storeAlias;
            CountryCode = countryCode;
            PostalCode = postalCode;
            return _exception is null
                ? Task.FromResult(_locations)
                : Task.FromException<IReadOnlyList<PickupLocation>?>(_exception);
        }
    }
}
