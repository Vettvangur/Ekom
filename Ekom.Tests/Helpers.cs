﻿using CommonServiceLocator;
using Ekom.API;
using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Security;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.BaseRest;
using Umbraco.Core.Configuration.Dashboard;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Dictionary;
using Umbraco.Core.Logging;
using Umbraco.Core.Models;
using Umbraco.Core.Persistence;
using Umbraco.Core.Persistence.SqlSyntax;
using Umbraco.Core.Profiling;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.Routing;
using Umbraco.Web.Security;

namespace Ekom.Tests
{
    static class Helpers
    {
        public static void InitDI()
        {
            Ekom.App_Start.TinyIoCActivator.Start();
        }

        public static OrderService GetOrderService()
        {
            InitMockContainer();
            var httpCtx = GetHttpContext();
            var logFac = MockLogFac();
            var discountCache = MockDiscountCache();
            var orderSvc = MockOrderService(discountCache);
            discountCache.GlobalDiscounts["is-IS"] = new ConcurrentStack<IDiscount>();

            var ekmReq = new ContentRequest(new HttpContextWrapper(httpCtx), logFac);

            new PrivateObject(orderSvc, new PrivateType(typeof(OrderService)))
                .SetField("_ekmRequest", ekmReq);

            return orderSvc;
        }

        public static Catalog GetCatalogApi()
            => new Catalog(
                GetSetAppCtx(),
                new Configuration(),
                MockLogFac(),
                (new Mock<IPerStoreCache<IProduct>> { DefaultValue = DefaultValue.Mock }).Object,
                Mock.Of<IPerStoreCache<ICategory>>(),
                Mock.Of<IPerStoreCache<IVariant>>(),
                Mock.Of<IStoreService>()
            );

        public static Mock<IServiceLocator> InitMockContainer()
        {
            var mockLocator = new Mock<IServiceLocator> { DefaultValue = DefaultValue.Mock };
            Configuration.container = mockLocator.Object;

            return mockLocator;
        }
        public static ILogFactory MockLogFac()
        {
            var logFac = new Mock<ILogFactory>() { DefaultValue = DefaultValue.Mock };
            return logFac.Object;
        }
        public static DiscountCache MockDiscountCache()
            => new DiscountCache(
                MockLogFac(),
                Mock.Of<Configuration>(),
                Mock.Of<IBaseCache<IStore>>(),
                Mock.Of<IPerStoreFactory<IDiscount>>()
            );
        public static OrderService MockOrderService(DiscountCache discountCache)
            => new OrderService(
                (new Mock<IOrderRepository> { DefaultValue = DefaultValue.Mock }).Object,
                MockLogFac(),
                Mock.Of<IStoreService>(),
                GetSetAppCtx(),
                discountCache,
                GetHttpContextBase()
            );

        public static HttpContext GetHttpContext()
        {
            var tw = new Mock<TextWriter>();
            var req = new HttpRequest("", "http://127.0.0.1/", "");
            var resp = new HttpResponse(tw.Object);

            return new HttpContext(req, resp);
        }
        public static HttpContextBase GetHttpContextBase()
        {
            var httpReqMock = new Mock<HttpRequestBase> { DefaultValue = DefaultValue.Mock };
            httpReqMock.Setup(req => req.Cookies).Returns(new HttpCookieCollection());
            var httpRespMock = new Mock<HttpResponseBase> { DefaultValue = DefaultValue.Mock };
            httpRespMock.Setup(resp => resp.Cookies).Returns(new HttpCookieCollection());
            var httpSessMock = new Mock<HttpSessionStateBase> { DefaultValue = DefaultValue.Mock };
            var httpCtxMock = new Mock<HttpContextBase> { DefaultValue = DefaultValue.Mock };
            httpCtxMock.Setup(h => h.Request).Returns(httpReqMock.Object);
            httpCtxMock.Setup(h => h.Response).Returns(httpRespMock.Object);
            httpCtxMock.Setup(h => h.Session).Returns(httpSessMock.Object);

            return httpCtxMock.Object;
        }

        public static ApplicationContext GetSetAppCtx()
        {
            var appCtx = new ApplicationContext(CacheHelper.CreateDisabledCacheHelper(), new ProfilingLogger(Mock.Of<ILogger>(), Mock.Of<IProfiler>()));
            return ApplicationContext.EnsureContext(appCtx, true);
        }

        public static ApplicationContext GetSetAppCtx(CacheHelper cacheHelper)
        {
            var appCtx = new ApplicationContext(cacheHelper, new ProfilingLogger(Mock.Of<ILogger>(), Mock.Of<IProfiler>()));
            return ApplicationContext.EnsureContext(appCtx, true);
        }

        public static void GetSetAppUmbracoCtx()
        {
            var appCtx = ApplicationContext.EnsureContext(
                new DatabaseContext(Mock.Of<IDatabaseFactory2>(), Mock.Of<ILogger>(), new SqlSyntaxProviders(new[] { Mock.Of<ISqlSyntaxProvider>() })),
                new ServiceContext(),
                CacheHelper.CreateDisabledCacheHelper(),
                new ProfilingLogger(
                    Mock.Of<ILogger>(),
                    Mock.Of<IProfiler>()
                ),
                true
            );
            ApplicationContext.EnsureContext(appCtx, true);

            var umbCtx = UmbracoContext.EnsureContext(
                new Mock<HttpContextBase>().Object,
                appCtx,
                new Mock<WebSecurity>(null, null).Object,
                Mock.Of<IUmbracoSettingsSection>(section => section.WebRouting == Mock.Of<IWebRoutingSection>(routingSection => routingSection.UrlProviderMode == "AutoLegacy")),
                Enumerable.Empty<IUrlProvider>(),
                true
            );
        }

        public static void GetSetAppUmbracoCtx(CacheHelper cacheHelper)
        {
            var appCtx = ApplicationContext.EnsureContext(
                new DatabaseContext(Mock.Of<IDatabaseFactory2>(), Mock.Of<ILogger>(), new SqlSyntaxProviders(new[] { Mock.Of<ISqlSyntaxProvider>() })),
                new ServiceContext(),
                cacheHelper,
                new ProfilingLogger(
                    Mock.Of<ILogger>(),
                    Mock.Of<IProfiler>()
                ),
                true
            );

            var umbCtx = UmbracoContext.EnsureContext(
                new Mock<HttpContextBase>().Object,
                appCtx,
                new Mock<WebSecurity>(null, null).Object,
                Mock.Of<IUmbracoSettingsSection>(section => section.WebRouting == Mock.Of<IWebRoutingSection>(routingSection => routingSection.UrlProviderMode == "AutoLegacy")),
                Enumerable.Empty<IUrlProvider>(),
                true
            );
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

#pragma warning disable CS0618 // Type or member is obsolete
            cacheHelper = new CacheHelper(runtimeCache.Object, staticCache.Object, requestCache.Object);
#pragma warning restore CS0618 // Type or member is obsolete
        }
    }

    public class UmbracoSettingsMocks
    {
        public Mock<IUmbracoSettingsSection> umbracoSettings;
        public Mock<IBaseRestSection> baseRest;
        public Mock<IDashboardSection> dashboard;
        public UmbracoConfig umbracoConfig;

        public UmbracoSettingsMocks()
        {
            umbracoSettings = new Mock<IUmbracoSettingsSection>();
            baseRest = new Mock<IBaseRestSection>();
            dashboard = new Mock<IDashboardSection>();

            umbracoConfig = new UmbracoConfig(umbracoSettings.Object, baseRest.Object, dashboard.Object);
        }
    }

    public class UHelperMocks
    {
        public Mock<ITypedPublishedContentQuery> typedQuery;
        public UmbracoHelper uHelper;

        public UHelperMocks()
        {
            typedQuery = new Mock<ITypedPublishedContentQuery>();
            typedQuery.Setup(query => query.TypedContent(It.IsAny<int>()) ==
                //return mock of IPublishedContent for any call to GetById
                Mock.Of<IPublishedContent>(content => content.Id == 2)
            );

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
                typedQuery.Object,
                Mock.Of<IDynamicPublishedContentQuery>(),
                Mock.Of<ITagQuery>(),
                Mock.Of<IDataTypeService>(),
                new UrlProvider(umbCtx, Enumerable.Empty<IUrlProvider>()),
                Mock.Of<ICultureDictionary>(),
                Mock.Of<IUmbracoComponentRenderer>(),
                new MembershipHelper(umbCtx, Mock.Of<MembershipProvider>(), Mock.Of<RoleProvider>()));

            uHelper = helper;
        }
    }
}
