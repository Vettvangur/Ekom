using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core.Logging;
using uWebshop.Services;

namespace uWebshop
{
    public class UrlRewriting : IHttpModule
    {
        public void Init(HttpApplication app)
        {
            app.BeginRequest += RewriteUrlsOnAppBeginRequest;      
        }

        public void Dispose()
        {
        }

        private void RewriteUrlsOnAppBeginRequest(object source, EventArgs e)
        {
            LogHelper.Info(this.GetType(), "RewriteUrlsOnAppBeginRequest...");

            var context = ((HttpApplication)source).Context;
            var contextAbsPath = context.Request.Url.AbsolutePath;

            try
            {
                //var service = new UrlRewritingService();

                //service.Rewrite(contextAbsPath);
            }
            catch (Exception ex)
            {
                LogHelper.Info(this.GetType(), "Exception while urlRewriting: " + ex + ", url: " + contextAbsPath);
            }
        }

    }
}
