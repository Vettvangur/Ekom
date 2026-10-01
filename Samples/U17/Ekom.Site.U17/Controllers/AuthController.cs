using Ekom.API;
using Ekom.Site.U17.Models;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Logging;
using Umbraco.Cms.Core.Routing;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Web;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Web.Common.Filters;
using Umbraco.Cms.Web.Common.Security;
using Umbraco.Cms.Web.Website.Controllers;

namespace Ekom.Site.U17.Controllers;

public sealed class AuthController(
    IUmbracoContextAccessor umbracoContextAccessor,
    IUmbracoDatabaseFactory databaseFactory,
    ServiceContext services,
    AppCaches appCaches,
    IProfilingLogger profilingLogger,
    IPublishedUrlProvider publishedUrlProvider,
    IMemberSignInManager memberSignInManager,
    Order orderApi,
    IWebHostEnvironment environment) : SurfaceController(
        umbracoContextAccessor, databaseFactory, services, appCaches, profilingLogger, publishedUrlProvider)
{
    [HttpPost]
    [ValidateAntiForgeryToken]
    [ValidateUmbracoFormRouteString]
    public async Task<IActionResult> Login(Login model)
    {
        if (!ModelState.IsValid)
            return RedirectToCurrentUmbracoPage(QueryString.Create("error", "invalidData"));

        var result = await memberSignInManager.PasswordSignInAsync(model.Username, model.Password, false, true);
        return result.Succeeded ? RedirectToCurrentUmbracoPage()
            : RedirectToCurrentUmbracoPage(QueryString.Create("error", "incorrectPassword"));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ValidateUmbracoFormRouteString]
    public async Task<IActionResult> Logout()
    {
        await memberSignInManager.SignOutAsync();
        return RedirectToCurrentUmbracoPage();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [ValidateUmbracoFormRouteString]
    public async Task<IActionResult> Reinitialize(string storeAlias, CancellationToken ct)
    {
        if (!environment.IsDevelopment()) return NotFound();
        var order = await orderApi.GetOrderAsync(storeAlias, ct).ConfigureAwait(false);
        if (order != null && order.OrderLines.Count > 0)
            await orderApi.ReInitializeOrder(storeAlias, new OrderSettings { OrderInfo = order }, ct).ConfigureAwait(false);
        return RedirectToCurrentUmbracoPage(QueryString.Create("pricesRefreshed", "true"));
    }
}
