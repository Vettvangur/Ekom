using Ekom.Controllers;
using Ekom.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;
using System.Security.Claims;
using System.Reflection;
using Xunit;

namespace Ekom.Tests.Tests;

public class EkomBackofficeApiControllerTests
{
    [Fact]
    public void GetStores_AcceptsStringDocumentIdentity()
    {
        MethodInfo method = typeof(EkomBackofficeApiController).GetMethod(nameof(EkomBackofficeApiController.GetStores))!;
        ParameterInfo parameter = Assert.Single(method.GetParameters());
        RouteAttribute route = Assert.Single(method.GetCustomAttributes<RouteAttribute>());

        Assert.Equal(typeof(string), parameter.ParameterType);
        Assert.Equal("Stores/{id}", route.Template);
    }

    [Fact]
    public void PopulateCache_LogsBackofficeUsernameAndRefreshesCache()
    {
        var cacheRefreshService = new Mock<ICacheRefreshService>();
        var logger = new Mock<ILogger<EkomBackofficeApiController>>();
        using var memoryCache = new MemoryCache(new MemoryCacheOptions());
        var controller = new EkomBackofficeApiController(
            null!,
            null!,
            null!,
            null!,
            memoryCache,
            null!,
            null!,
            null!,
            cacheRefreshService.Object,
            logger.Object)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "admin")]))
                }
            }
        };

        var result = controller.PopulateCache();

        Assert.True(result);
        cacheRefreshService.Verify(x => x.RefreshCache(), Times.Once);
        logger.Verify(
            x => x.Log(
                LogLevel.Information,
                It.IsAny<EventId>(),
                It.Is<It.IsAnyType>((state, _) => state.ToString()!.Contains("backoffice user admin")),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
