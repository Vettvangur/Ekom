using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using System.Web.Security;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Dictionary;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Profiling;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Routing;
using Umbraco.Web.Security;

namespace uWebshop.Tests
{
    public static class Helpers
    {
        public static HttpContext GetHttpContext()
        {
            var tw = new Mock<TextWriter>();
            var req = new HttpRequest("", "", "");
            var resp = new HttpResponse(tw.Object);

            return new HttpContext(req, resp);
        }

        public static ApplicationContext GetSetAppCtx()
        {
            var appCtx = new ApplicationContext(CacheHelper.CreateDisabledCacheHelper(), new ProfilingLogger(Mock.Of<ILogger>(), Mock.Of<IProfiler>()));
            return ApplicationContext.EnsureContext(appCtx, true);
        }

        public static UmbracoHelper GetUHelper()
        {
            var appCtx = new ApplicationContext(CacheHelper.CreateDisabledCacheHelper(), new ProfilingLogger(Mock.Of<ILogger>(), Mock.Of<IProfiler>()));

            var umbCtx = UmbracoContext.EnsureContext(
                new Mock<HttpContextBase>().Object,
                appCtx,
                new Mock<WebSecurity>(null, null).Object,
                Mock.Of<IUmbracoSettingsSection>(section => section.WebRouting == Mock.Of<IWebRoutingSection>(routingSection => routingSection.UrlProviderMode == "AutoLegacy")),
                Enumerable.Empty<IUrlProvider>(),
                true);

            var helper = new UmbracoHelper(
                umbCtx,
                Mock.Of<IPublishedContent>(),
                Mock.Of<ITypedPublishedContentQuery>(query => query.TypedContent(It.IsAny<int>()) ==
                    //return mock of IPublishedContent for any call to GetById
                    Mock.Of<IPublishedContent>(content => content.Id == 2)),
                Mock.Of<IDynamicPublishedContentQuery>(),
                Mock.Of<ITagQuery>(),
                Mock.Of<IDataTypeService>(),
                new UrlProvider(umbCtx, Enumerable.Empty<IUrlProvider>()),
                Mock.Of<ICultureDictionary>(),
                Mock.Of<IUmbracoComponentRenderer>(),
                new MembershipHelper(umbCtx, Mock.Of<MembershipProvider>(), Mock.Of<RoleProvider>()));

            return helper;
        }


    }

    internal class ActivatorServiceProvider : IServiceProvider
    {
        public object GetService(Type serviceType)
        {
            return Activator.CreateInstance(serviceType);
        }
    }

    public class CacheMocks
    {
        public Mock<IRuntimeCacheProvider> runtimeCache;
        public Mock<ICacheProvider> staticCache;
        public Mock<ICacheProvider> requestCache;
        public CacheHelper cacheHelper;

        public CacheMocks()
        {
            runtimeCache = new Mock<IRuntimeCacheProvider>();
            staticCache = new Mock<ICacheProvider>();
            requestCache = new Mock<ICacheProvider>();

            cacheHelper = new CacheHelper(runtimeCache.Object, staticCache.Object, requestCache.Object);
        }
    }
}
