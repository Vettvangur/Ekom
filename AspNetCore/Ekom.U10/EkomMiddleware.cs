using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Web;

namespace Ekom.Umb;

/// <summary>
/// Ekom middleware, ensures an ekmRequest object exists in the runtime cache for all
/// controller requests. <br />
/// The module checks for existence of a store querystring parameter and if found,
/// creates an ekmRequest object with DomainPrefix and currency if applicable. <br />
/// <br />
/// ConventionalMiddleware https://docs.microsoft.com/en-us/aspnet/core/fundamentals/middleware/extensibility?view=aspnetcore-6.0
/// </summary>
class EkomMiddleware
{
    private readonly RequestDelegate _next;
    private ILogger<EkomMiddleware> _logger;
    private HttpContext _context;
    public EkomMiddleware(RequestDelegate next)
        => _next = next;

    /// <summary>
    /// 
    /// </summary>
    public async Task InvokeAsync(
        HttpContext context,
        ILogger<EkomMiddleware> logger,
        IUmbracoContextFactory umbracoContextFac,
        AppCaches appCaches,
        IMemberService memberService
    )
    {
        _logger = logger;
        _context = context;

        OnBeginRequest(umbracoContextFac, appCaches);
        await OnAuthenticateRequest(appCaches, memberService);

        await _next.Invoke(context);

        OnPostRequestHandlerExecute(umbracoContextFac);
    }

    /// <summary>
    /// We store the requests umbraco domain in a cookie
    /// This ensures that ajax requests with no ufprt form value 
    /// can still resolve the correct urls for a product.
    /// See Url property on Product/Category.
    /// 
    /// Another option would have been to always return the list of urls for a product/category,
    /// leaving it to the frontend to match, sub-par solution but simpler?
    /// </summary>
    private void OnPostRequestHandlerExecute(IUmbracoContextFactory umbracoContextFac)
    {
        try
        {
            using var umbCtx = umbracoContextFac.EnsureUmbracoContext();
            if (umbCtx?.UmbracoContext.PublishedRequest?.Domain?.Uri != null)
            {
                CookieHelper.SetUmbracoDomain(
                    _context.Response.Cookies,
                    umbCtx.UmbracoContext.PublishedRequest.Domain.Uri);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Http module PostRequestHandlerExecute failed, make sure to have domain set on the store root node.");
        }
    }

    private void OnBeginRequest(IUmbracoContextFactory umbracoContextFac, AppCaches appCaches)
    {
        try
        {
            if (_context == null)
            {
                return;
            }

            var requestPath = _context.Request?.Path.ToString();

            if (!AllowPath(requestPath))
            {
                return;
            }

            using var umbCtx = umbracoContextFac.EnsureUmbracoContext();
            // No umbraco context exists for static file requests
            if (umbCtx?.UmbracoContext != null)
            {
                appCaches.RequestCache.Get("ekmRequest", () =>
                    new ContentRequest()
                    {
                        User = new User(),
                    });
            }


        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Http module Begin Request failed");
        }
    }

    private bool AllowPath(string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return false;
        }
        if (
            path.StartsWith("/umbraco/surface", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/umbraco/api", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/umbraco/backoffice/api", StringComparison.InvariantCultureIgnoreCase)
            )
        {
            return true;
        }
        if (
            path.StartsWith("/umbraco/", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/media/", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/app_plugins/", StringComparison.InvariantCultureIgnoreCase) ||
            path.StartsWith("/build/", StringComparison.InvariantCultureIgnoreCase)
            )
        {
            return false;
        }
        return true;
    }

    private async Task OnAuthenticateRequest(
        AppCaches appCaches,
        IMemberService memberService)
    {

        try
        {
            if (_context == null)
            {
                return;
            }

            var requestPath = _context.Request?.Path.HasValue == true ? _context.Request?.Path.Value : "";

            if (!AllowPath(requestPath))
            {
                return;
            }

            if (_context.User?.Identity == null)
            {
                return;
            }

            if (_context.User.Identity.IsAuthenticated == false)
            {
                return;
            }

            var username = _context.User.Identity.Name;

            if (string.IsNullOrEmpty(username))
            {
                return;
            }

            if (appCaches.RequestCache.Get("ekmRequest", () => new ContentRequest()) is ContentRequest ekmRequest)
            {
                var memberContent = memberService.GetByUsername(username);

                if (memberContent != null)
                {
                    ekmRequest.User = new User
                    {
                        Email = memberContent.Email,
                        Username = memberContent.UserName,
                        UserId = memberContent.Id,
                        Name = memberContent.Name,
                    };

                    var orderid = memberContent.OrderId;

                    if (!string.IsNullOrEmpty(orderid) && Guid.TryParse(orderid, out var guid))
                    {
                        ekmRequest.User.OrderId = guid;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            //_logger.LogError(ex, "AuthenticateRequest Failed");
        }
    }
}
