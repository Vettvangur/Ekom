using Ekom.API;
using Ekom.Cache;
using Ekom.Controllers;
using Ekom.Models;
using Ekom.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using System.Collections.Concurrent;
using Xunit;

namespace Ekom.Tests.Tests;

public class ManagerPaymentProviderTests
{
    [Fact]
    public void GetPaymentProviders_Uses_Default_Store_Culture_And_Enabled_Cache_Entries()
    {
        var store = new Mock<IStore>();
        store.SetupGet(x => x.Alias).Returns("HVerslun");
        store.SetupGet(x => x.Cultures).Returns([
            new CultureInfoDto { Name = "is-IS" },
            new CultureInfoDto { Name = "en-US" }
        ]);

        var first = new Mock<IPaymentProvider>();
        first.SetupGet(x => x.SortOrder).Returns(1);
        var second = new Mock<IPaymentProvider>();
        second.SetupGet(x => x.SortOrder).Returns(2);
        var enabled = new ConcurrentDictionary<Guid, IPaymentProvider>();
        enabled[Guid.NewGuid()] = second.Object;
        enabled[Guid.NewGuid()] = first.Object;

        var controller = CreateController(store.Object, enabled);
        controller.HttpContext.Features.Set<IRequestCultureFeature>(
            new RequestCultureFeature(new RequestCulture("en-US"), null));

        var result = Assert.IsType<OkObjectResult>(controller.GetPaymentProviders("HVerslun"));
        var providers = Assert.IsAssignableFrom<IReadOnlyList<IPaymentProvider>>(result.Value);

        Assert.Equal([first.Object, second.Object], providers);
        Assert.Equal("is-IS", controller.HttpContext.Features.Get<IRequestCultureFeature>()?.RequestCulture.Culture.Name);
    }

    [Fact]
    public void GetPaymentProviders_Rejects_Stores_Without_Access()
    {
        var controller = CreateController(null, new ConcurrentDictionary<Guid, IPaymentProvider>());

        Assert.IsType<ForbidResult>(controller.GetPaymentProviders("HVerslun"));
    }

    private static EkomManagerController CreateController(IStore? store, ConcurrentDictionary<Guid, IPaymentProvider> enabled)
    {
        var access = new Mock<IManagerAccessService>();
        access.Setup(x => x.CanAccessStore("HVerslun")).Returns(store != null);
        access.Setup(x => x.GetAllowedStores()).Returns(store == null ? [] : [store]);

        var paymentCache = new Mock<IPerStoreCache<IPaymentProvider>>();
        paymentCache.Setup(x => x["HVerslun"]).Returns(enabled);
        var providers = new Providers(
            null!,
            NullLogger<Providers>.Instance,
            Mock.Of<IPerStoreCache<IShippingProvider>>(),
            paymentCache.Object,
            Mock.Of<IBaseCache<IZone>>(),
            Mock.Of<IStoreService>(),
            null!);

        return new EkomManagerController(
            null!,
            access.Object,
            Mock.Of<INodeService>(),
            Mock.Of<IOrderActivityLogService>(),
            Mock.Of<IOrderManagerActionService>(),
            providers,
            NullLogger<EkomManagerController>.Instance)
        {
            ControllerContext = new ControllerContext { HttpContext = new DefaultHttpContext() }
        };
    }
}
