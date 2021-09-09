using Ekom.Models;
using Ekom.Utilities;
using System;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.Security;

namespace Ekom
{
    /// <summary>
    /// Ekom HttpModule, ensures an ekmRequest object exists in the runtime cache for all
    /// controller requests.
    /// The module checks for existence of a store querystring parameter and if found,
    /// creates an ekmRequest object with DomainPrefix and currency if applicable.
    /// </summary>
    class HttpModule : IHttpModule
    {
        /// <summary>
        /// ModuleName
        /// </summary>
        public string ModuleName
        {
            get { return "Ekom HttpModule"; }
        }

        /// <summary>
        /// <see cref="IHttpModule"/> init method
        /// </summary>
        /// <param name="context"></param>
        public void Init(HttpApplication context)
        {
            context.BeginRequest += new EventHandler(Application_BeginRequest);
            context.AuthenticateRequest += new EventHandler(Application_AuthenticateRequest);
            context.PostRequestHandlerExecute += Context_PostRequestHandlerExecute;
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
        private void Context_PostRequestHandlerExecute(object sender, EventArgs e)
        {
            var logger = Current.Factory.GetInstance<ILogger>();

            try
            {
               var umbCtx = Current.Factory.GetInstance<UmbracoContext>();
                if (umbCtx?.PublishedRequest?.Domain.Uri != null)
                {

                    HttpApplication application = (HttpApplication)sender;

                    HttpContext httpCtx = application.Context;

                    CookieHelper.SetUmbracoDomain(
                        httpCtx.Response.Cookies,
                        umbCtx.PublishedRequest.Domain.Uri);
                }
            }
            catch (Exception ex)
            {
                logger.Error<HttpModule>(ex, "Http module PostRequestHandlerExecute failed, make sure to have domain set on the store root node.");
            }
        }

        private void Application_BeginRequest(object source, EventArgs e)

        {
            var logger = Current.Factory.GetInstance<ILogger>();

            try
            {
                HttpApplication application = (HttpApplication)source;
                HttpContext httpCtx = application.Context;

                var umbCtx = Current.Factory.GetInstance<UmbracoContext>();

                // No umbraco context exists for static file requests
                if (umbCtx != null)
                {
                    var appCaches = Current.Factory.GetInstance<AppCaches>();

                    appCaches.RequestCache.GetCacheItem("ekmRequest", () =>
                        new ContentRequest(new HttpContextWrapper(httpCtx), logger)
                        {
                            User = new User(),

                        }
                    );
                }
            }
            catch (Exception ex)
            {
                logger.Error<HttpModule>(ex, "Http module Begin Request failed");
            }
        }

        private void Application_AuthenticateRequest(object source, EventArgs e)
        {
            try
            {
                HttpApplication application = (HttpApplication)source;
                HttpContext httpCtx = application.Context;

                if (httpCtx.User?.Identity.IsAuthenticated == true)
                {
                    var appCaches = Current.Factory.GetInstance<AppCaches>();

                    if (appCaches.RequestCache.GetCacheItem<ContentRequest>("ekmRequest") is ContentRequest ekmRequest)
                    {
                        // This is always firing!, ekmRequest.User.Username is always empty
                        if (ekmRequest.User.Username != httpCtx.User.Identity.Name)
                        {
                            var memberShipHelper = Current.Factory.GetInstance<MembershipHelper>();

                            var member = memberShipHelper.GetByUsername(httpCtx.User.Identity.Name);

                            if (member != null)
                            {
                                ekmRequest.User = new User
                                {
                                    Email = member.Value<string>("Email"),
                                    Username = member.Value<string>("UserName"),
                                    UserId = member.Id,
                                    Name = member.Name,
                                };
                                var orderid = member.Value<string>("orderId", fallback: Fallback.ToDefaultValue);
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
                var logger = Current.Factory.GetInstance<ILogger>();

                logger.Error<HttpModule>(ex, "AuthenticateRequest Failed");
            }
        }

        /// <summary>
        /// No actions needed
        /// </summary>
        public void Dispose() { }
    }
}
