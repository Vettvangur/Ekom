using Ekom.Controllers;
using Microsoft.AspNetCore.Mvc;
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
}
