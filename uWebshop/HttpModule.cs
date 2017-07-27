using Microsoft.Practices.Unity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core;
using uWebshop.App_Start;
using uWebshop.Models;
using uWebshop.Services;
using uWebshop.Utilities;

namespace uWebshop
{
    /// <summary>
    /// uWebshop HttpModule, ensures an uwbsRequest object exists in the runtime cache for all
    /// controller requests.
    /// The module checks for existence of a store querystring parameter and if found,
    /// creates an uwbsRequest object with DomainPrefix and currency if applicable.
    /// </summary>
    public class HttpModule : IHttpModule
    {
        /// <summary>
        /// ModuleName
        /// </summary>
        public String ModuleName
        {
            get { return "uWebshop HttpModule"; }
        }

        /// <summary>
        /// <see cref="IHttpModule"/> init method
        /// </summary>
        /// <param name="context"></param>
        public void Init(HttpApplication context)
        {
            context.BeginRequest += new EventHandler(Application_BeginRequest);
        }

        private void Application_BeginRequest(Object source, EventArgs e)
        {
            HttpApplication application = (HttpApplication)source;
            HttpContext context = application.Context;

            var url = context.Request.Url;

            var queryObj = HttpUtility.ParseQueryString(url.Query);

            var storeAlias = queryObj["store"];

            // Requests with a store query parameter are assumed to surface or api controller requests
            // in which case we must construct the uwbsRequest object as the CatalogContentFinder will not run
            if (!string.IsNullOrEmpty(storeAlias))
            {
                var container = UnityConfig.GetConfiguredContainer();

                var storeSvc = container.Resolve<StoreService>();

                var store = storeSvc.GetStoreByAlias(storeAlias);

                if (store != null)
                {
                    var appCtx = container.Resolve<ApplicationContext>();

                    var appCache = appCtx.ApplicationCache;
                    appCache.RequestCache.GetCacheItem("uwbsRequest", () =>
                    {
                        var path = url.AbsolutePath.ToLower().AddTrailing();
                        var currency = queryObj["currency"];

                        return new ContentRequest(new HttpContextWrapper(context), new LogFactory())
                        {
                            Store = store,
                            Currency = currency,
                            DomainPrefix = path,
                        };
                    });
                }
            }
        }

        /// <summary>
        /// No actions needed
        /// </summary>
        public void Dispose() { }
    }
}
