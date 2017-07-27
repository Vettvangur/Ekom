using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using uWebshop.Models;
using uWebshop.Utilities;

namespace uWebshop
{
    class HttpModule : IHttpModule
    {
        /// <summary>
        /// No actions needed
        /// </summary>
        public void Dispose() { }

        public void Init(HttpApplication context)
        {
            var url = context.Request.Url;
            
            var queryObj = HttpUtility.ParseQueryString(url.Query);

            var path = url.AbsolutePath.ToLower().AddTrailing();
            var store = queryObj["store"];
            var currency = queryObj["currency"];

            var uwbsRequest = new ContentRequest(newq context.Context, new LogFactory())
            {
                Store = store,
                Currency = Currency,
                DomainPrefix = path,
                Product = product,
                Category = category
            };

            var appCache = umbracoContext.Application.ApplicationCache;

            appCache.RequestCache.GetCacheItem("uwbsRequest", () => uwbsRequest);
        }
    }
}
