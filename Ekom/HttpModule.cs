using Ekom.Models;
using Ekom.Services;
using Ekom.Utilities;
using log4net;
using System;
using System.IO;
using System.Reflection;
using System.Web;
using System.Web.Caching;
using System.Web.Hosting;
using System.Web.Script.Serialization;
using Umbraco.Core;
using Umbraco.Core.Configuration;
using Umbraco.Web;
using Umbraco.Web.Routing;
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
            // Gives error when examine is empty. Need better fix
            try
            {
                HttpApplication application = (HttpApplication)source;
                HttpContext httpCtx = application.Context;

                var url = httpCtx.Request.Url;
                var uri = url.AbsoluteUri;

                if (uri.Contains(".css") || uri.Contains(".js")) { return; }

                var storeSvc = Configuration.container.GetInstance<StoreService>();

                Store store = storeSvc.GetStoreByDomain(url.Host + url.AbsolutePath);

                if (store != null)
                {
                    #region Currency 

                    // Unfinished - move to currency service

                    HttpCookie storeInfo = httpCtx.Request.Cookies["StoreInfo"];

                    object Currency = storeInfo != null ? /* CurrencyHelper.Get(*/storeInfo.Values["Currency"] : null;

                    #endregion
                    
                    var path = url.AbsolutePath.ToLower().AddTrailing();

                    var appCtx = Configuration.container.GetInstance<ApplicationContext>();
                    
                    var appCache = appCtx.ApplicationCache;
                    appCache.RequestCache.GetCacheItem("ekmRequest", () =>
                        new ContentRequest(new HttpContextWrapper(httpCtx), new LogFactory())
                        {
                            Store = store,
                            Currency = Currency,
                            DomainPrefix = path,
                            User = new User()
                        }
                    );
                }

            } catch(Exception ex)
            {
                Log.Error("Http module Begin Request failed",ex);
            }

        }

        private void Application_AuthenticateRequest(Object source, EventArgs e)
        {
            try
            {
                HttpApplication application = (HttpApplication)source;
                HttpContext httpCtx = application.Context;

                if (httpCtx != null && httpCtx.User != null && httpCtx.User.Identity.IsAuthenticated)
                {
                    var context = new HttpContextWrapper(new HttpContext(new SimpleWorkerRequest("/", string.Empty, new StringWriter())));
                    UmbracoContext.EnsureContext(
                                    context,
                                    ApplicationContext.Current,
                                    new WebSecurity(context, ApplicationContext.Current),
                                    UmbracoConfig.For.UmbracoSettings(),
                                    UrlProviderResolver.Current.Providers,
                                    false);


                    var appCtx = Configuration.container.GetInstance<ApplicationContext>();

                    var appCache = appCtx.ApplicationCache;
                    var ekmRequest = appCache.RequestCache.GetCacheItem("ekmRequest") as ContentRequest;

                    // This is always firing!, ekmRequest.User.Username is always empty
                    if (ekmRequest != null && ekmRequest.User.Username != httpCtx.User.Identity.Name)
                    {
                        var umbracoContext = UmbracoContext.Current;
                        var memberShipHelper = new MembershipHelper(umbracoContext);

                        var member = memberShipHelper.GetByUsername(httpCtx.User.Identity.Name);

                        if (member != null)
                        {
                            var u = new User()
                            {
                                Email = member.GetPropertyValue<string>("Email"),
                                Username = httpCtx.User.Identity.Name,
                                UserId = member.Id,
                                Name = member.Name
                            };

                            ekmRequest.User = u;
                        }



                    } 
                }

            }
            catch(Exception ex)
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
