using Ekom.Models;
using Ekom.Services;
using log4net;
using System;
using System.Reflection;
using System.Web;
using Umbraco.Core;
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
        protected static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );

        /// <summary>
        /// ModuleName
        /// </summary>
        public String ModuleName
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

        private void Application_BeginRequest(Object source, EventArgs e)
        {
            try
            {
                HttpApplication application = (HttpApplication)source;
                HttpContext httpCtx = application.Context;

                var umbCtx = Configuration.container.GetInstance<UmbracoContext>();

                // No umbraco context exists for static file requests
                if (umbCtx != null)
                {
                    #region Currency 

                    // Unfinished - move to currency service

                    HttpCookie storeInfo = httpCtx.Request.Cookies["StoreInfo"];

                    object Currency = storeInfo != null ? /* CurrencyHelper.Get(*/storeInfo.Values["Currency"] : null;

                    #endregion

                    var appCtx = Configuration.container.GetInstance<ApplicationContext>();
                    var logFac = Configuration.container.GetInstance<ILogFactory>();

                    var appCache = appCtx.ApplicationCache;
                    appCache.RequestCache.GetCacheItem("ekmRequest", () =>
                        new ContentRequest(new HttpContextWrapper(httpCtx), logFac)
                        {
                            Currency = Currency,
                            User = new User()
                        }
                    );
                }
            }
            catch (Exception ex)
            {
                Log.Error("Http module Begin Request failed", ex);
            }
        }

        private void Application_AuthenticateRequest(Object source, EventArgs e)
        {
            try
            {
                HttpApplication application = (HttpApplication)source;
                HttpContext httpCtx = application.Context;

                if (httpCtx.User?.Identity.IsAuthenticated == true)
                {
                    var appCtx = Configuration.container.GetInstance<ApplicationContext>();

                    var appCache = appCtx.ApplicationCache;

                    if (appCache.RequestCache.GetCacheItem("ekmRequest") is ContentRequest ekmRequest)
                    {
                        // This is always firing!, ekmRequest.User.Username is always empty
                        if (ekmRequest.User.Username != httpCtx.User.Identity.Name)
                        {
                            var umbracoContext = UmbracoContext.Current;
                            var memberShipHelper = new MembershipHelper(umbracoContext);

                            var member = memberShipHelper.GetByUsername(httpCtx.User.Identity.Name);

                            if (member is MemberPublishedContent memberContent)
                            {
                                ekmRequest.User = new User
                                {
                                    Email = memberContent.Email,
                                    Username = memberContent.UserName,
                                    UserId = memberContent.Id,
                                    Name = memberContent.Name
                                };
                            }
                        }
                    }
                }

            }
            catch (Exception ex)
            {
                Log.Error("AuthenticateRequest Failed", ex);
            }
        }

        /// <summary>
        /// No actions needed
        /// </summary>
        public void Dispose() { }
    }
}
