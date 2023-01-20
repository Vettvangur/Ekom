using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Core.Services;
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
        IMemberService memberService,
        AppCaches appCaches,
        IMemberManager memberManager
    )
    {
        _logger = logger;
        _context = context;

        Application_BeginRequest(umbracoContextFac, appCaches);
        await Application_AuthenticateRequestAsync(appCaches, memberService, memberManager).ConfigureAwait(false);

        Context_PostRequestHandlerExecute(umbracoContextFac);

        await _next.Invoke(context);
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
    private void Context_PostRequestHandlerExecute(IUmbracoContextFactory umbracoContextFac)
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

    private void Application_BeginRequest(IUmbracoContextFactory umbracoContextFac, AppCaches appCaches)
    {
        try
        {
            using (var umbCtx = umbracoContextFac.EnsureUmbracoContext())
            {
                // No umbraco context exists for static file requests
                if (umbCtx?.UmbracoContext != null)
                {
                    appCaches.RequestCache.Get("ekmRequest", () =>
                        new ContentRequest(_context)
                        {
                            User = new User(),
                        });
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Http module Begin Request failed");
        }
    }

    private async Task Application_AuthenticateRequestAsync(
        AppCaches appCaches,
        IMemberService memberService,
        IMemberManager memberManager)
    {
        try
        {
            var user = await memberManager.GetCurrentMemberAsync().ConfigureAwait(false);

            var ekmRequest = appCaches.RequestCache.Get("ekmRequest", () => new ContentRequest(_context)) as ContentRequest;
            if (ekmRequest is not null)
            {
                // This is always firing!, ekmRequest.User.Username is always empty
                // ..makes sense, new request with new cache each time this is run
                if (user != null && ekmRequest.User.Username != user.Name)
                {
                    var member = memberService.GetByUsername(user.Name);
                    if (member != null)
                    {
                        ekmRequest.User = new User
                        {
                            Email = member.Email,
                            Username = member.Username,
                            UserId = member.Id,
                            Name = member.Name,
                        };
                        var orderid = member.GetValue<string>("orderId");
                        if (!string.IsNullOrEmpty(orderid))
                        {
                            ekmRequest.User.OrderId = Guid.Parse(orderid);
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AuthenticateRequest Failed");
        }
    }
}
