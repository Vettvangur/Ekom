using Ekom.API;
using Ekom.Cache;
using Ekom.Controllers;
using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Newtonsoft.Json.Linq;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

public class ManagerShippingProviderTests
{
    [Fact]
    public void GetShippingProviders_ReturnsConfiguredProvidersForAllowedStore()
    {
        var store = new Mock<IStore>();
        store.SetupGet(x => x.Alias).Returns("main");
        store.SetupGet(x => x.Cultures).Returns([new CultureInfoDto { Name = "en-US" }]);

        var first = new Mock<IShippingProvider>();
        first.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        first.SetupGet(x => x.SortOrder).Returns(1);
        first.SetupGet(x => x.Properties).Returns(new Dictionary<string, string> { ["title"] = "Pickup" });
        var second = new Mock<IShippingProvider>();
        second.SetupGet(x => x.Key).Returns(Guid.NewGuid());
        second.SetupGet(x => x.SortOrder).Returns(2);
        second.SetupGet(x => x.Properties).Returns(new Dictionary<string, string> { ["title"] = "Delivery" });

        var enabled = new ConcurrentDictionary<Guid, IShippingProvider>();
        enabled[second.Object.Key] = second.Object;
        enabled[first.Object.Key] = first.Object;

        var controller = CreateController(store.Object, enabled);
        var result = Assert.IsType<OkObjectResult>(controller.GetShippingProviders("main"));
        var providers = JArray.FromObject(result.Value!);

        Assert.Equal(first.Object.Key, providers[0]?["Key"]?.Value<Guid>());
        Assert.Equal("Pickup", providers[0]?["Title"]?.Value<string>());
        Assert.Equal(second.Object.Key, providers[1]?["Key"]?.Value<Guid>());
    }

    [Fact]
    public void GetShippingProviders_RejectsUnauthorizedStore()
    {
        var controller = CreateController(null, new ConcurrentDictionary<Guid, IShippingProvider>());

        Assert.IsType<ForbidResult>(controller.GetShippingProviders("main"));
    }

    [Fact]
    public async Task UpdateOrderShippingProvider_RejectsMissingProviderId()
    {
        var controller = CreateController(null, new ConcurrentDictionary<Guid, IShippingProvider>());

        Assert.IsType<BadRequestObjectResult>(await controller.UpdateOrderShippingProviderAsync(Guid.NewGuid(),
            new Ekom.Models.Manager.OrderShippingProviderUpdateRequest()));
    }

    private static EkomManagerController CreateController(IStore? store, ConcurrentDictionary<Guid, IShippingProvider> enabled)
    {
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore("main")).Returns(store != null);
        access.Setup(x => x.GetAllowedStores()).Returns(store == null ? [] : [store]);

        var shippingCache = new Mock<IPerStoreCache<IShippingProvider>>();
        shippingCache.Setup(x => x["main"]).Returns(enabled);
        var providers = new Providers(null!, NullLogger<Providers>.Instance,
            shippingCache.Object, Mock.Of<IPerStoreCache<IPaymentProvider>>(),
            Mock.Of<IBaseCache<IZone>>(), Mock.Of<IStoreService>(), null!);

        return new EkomManagerController(null!, access.Object, Mock.Of<INodeService>(),
            Mock.Of<IOrderActivityLogService>(), Mock.Of<IOrderManagerActionService>(),
            providers, NullLogger<EkomManagerController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
