using Ekom.Models;
using Ekom.Utilities;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Cache;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Web.Common.UmbracoContext;

namespace Ekom.Umb
{
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
        private ILogger _logger;
        private HttpContext _context;

        public EkomMiddleware(RequestDelegate next)
            => _next = next;

        /// <summary>
        /// <see cref="IHttpModule"/> init method
        /// </summary>
        /// <param name="context"></param>
        public async Task InvokeAsync(
            HttpContext context,
            ILogger<EkomMiddleware> logger,
            UmbracoContextFactory umbracoContextFac,
            AppCaches appCaches,
            IMemberService memberService
        )
        {
            _logger = logger;
            _context = context;

            Application_BeginRequest(umbracoContextFac, appCaches);
            Application_AuthenticateRequest(appCaches, memberService);

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
        private void Context_PostRequestHandlerExecute(UmbracoContextFactory umbracoContextFac)
        {
            try
            {
                using var umbCtx = umbracoContextFac.EnsureUmbracoContext();
                if (umbCtx?.UmbracoContext.PublishedRequest?.Domain.Uri != null)
                {
                    CookieHelper.SetUmbracoDomain(
                        _context.Response.Cookies,
                        umbCtx.UmbracoContext.PublishedRequest.Domain.Uri);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Http module PostRequestHandlerExecute failed, make sure to have domain set on the store root node.");
            }
        }

        private void Application_BeginRequest(UmbracoContextFactory umbracoContextFac, AppCaches appCaches)

        {
            try
            {
                using var umbCtx = umbracoContextFac.EnsureUmbracoContext();

                // No umbraco context exists for static file requests
                if (umbCtx?.UmbracoContext != null)
                {
                    appCaches.RequestCache.Get("ekmRequest", () =>
                        new ContentRequest(_context)
                        {
                            User = new User(),
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Http module Begin Request failed");
            }
        }

        private void Application_AuthenticateRequest(
            AppCaches appCaches,
            IMemberService memberService)
        {
            try
            {
                if (_context.User?.Identity.IsAuthenticated == true)
                {
                    if (appCaches.RequestCache.Get("ekmRequest") is ContentRequest ekmRequest)
                    {
                        // This is always firing!, ekmRequest.User.Username is always empty
                        if (ekmRequest.User.Username != _context.User.Identity.Name)
                        {
                            var member = memberService.GetByUsername(_context.User.Identity.Name);

                            if (member != null)
                            {
                                ekmRequest.User = new User
                                {
                                    Email = member.GetValue<string>("Email"),
                                    Username = member.GetValue<string>("UserName"),
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

            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AuthenticateRequest Failed");
            }
        }
    }
}
