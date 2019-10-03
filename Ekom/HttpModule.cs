using Ekom.Models;
using Ekom.Services;
using System;
using System.Reflection;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
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
                    #region Currency 

                    // Unfinished - move to currency service

                    HttpCookie storeInfo = httpCtx.Request.Cookies["StoreInfo"];

                    object Currency = storeInfo != null ? /* CurrencyHelper.Get(*/storeInfo.Values["Currency"] : null;

                    #endregion

                    var appCaches = Current.Factory.GetInstance<AppCaches>();

                    appCaches.RequestCache.GetCacheItem("ekmRequest", () => 
                        new ContentRequest(new HttpContextWrapper(httpCtx), logger)
                        {
                            Currency = Currency,
                            User = new User()
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
