using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Core;
using Umbraco.Core.Logging;
using Umbraco.Web.Routing;
using uWebshop.Cache;
using uWebshop.Services;

namespace uWebshop
{
    public class Application : IApplicationEventHandler
    {
        public void OnApplicationInitialized(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            LogHelper.Info(GetType(), "OnApplicationInitialized...");

            // Fill Caches
            StoreDomainCache.Instance.FillCache();
            StoreCache.Instance.FillCache();
            VariantCache.Instance.FillCache();
            VariantGroupCache.Instance.FillCache();
            CategoryCache.Instance.FillCache();
            ProductCache.Instance.FillCache();
            ZoneCache.Instance.FillCache();
            PaymentProviderCache.Instance.FillCache();
        }

        public void OnApplicationStarting(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            LogHelper.Info(GetType(), "OnApplicationStarting...");
            
            ContentFinderResolver.Current.InsertTypeBefore<ContentFinderByPageIdQuery, CatalogContentFinder>();
            UrlProviderResolver.Current.InsertTypeBefore<DefaultUrlProvider, CatalogUrlProvider>();
        }

        public void OnApplicationStarted(UmbracoApplicationBase umbracoApplication, ApplicationContext applicationContext)
        {
            LogHelper.Info(GetType(), "OnApplicationStarted...");
        }
    }
}
