using Ekom.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Analytics;

public static class AnalyticsAuthorization
{
    public const string Policy = "EkomAnalyticsBackoffice";

    internal static bool HasBackofficeUser(AuthorizationHandlerContext context)
    {
        var httpContext = context.Resource switch
        {
            HttpContext http => http,
            AuthorizationFilterContext filter => filter.HttpContext,
            _ => null,
        };
        // The Umbraco adapters resolve groups from the authenticated backoffice
        // ticket and persisted user, not a storefront member's claims. An empty
        // manager permission configuration alone must not grant analytics access.
        var security = httpContext?.RequestServices.GetService<ISecurityService>();
        return security?.GetUmbracoUserGroups().Count > 0;
    }
}
