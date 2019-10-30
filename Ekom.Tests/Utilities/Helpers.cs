using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Tests.MockClasses;
using Examine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Web;
using System.Web.Hosting;
using System.Web.Security;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Configuration;
using Umbraco.Core.Configuration.UmbracoSettings;
using Umbraco.Core.Dictionary;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.Routing;
using Umbraco.Web.Security;

namespace Ekom.Tests
{
    static class Helpers
    {
        // dummy TextWriter that does not write
        private class NullWriter : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }

        public static HttpContext GetSetHttpContext()
            => HttpContext.Current = new HttpContext(
                new SimpleWorkerRequest("/", "", "null.aspx", "", new NullWriter())
            );

        public static (IFactory, IRegister) RegisterAll()
        {
            var register = RegisterFactory.Create();
            var factory = register.CreateFactory();
            //new Registrations().DoCompose(register);

            RegisterMockedHttpContext(register);
            RegisterMockedUmbracoTypes(register, factory);
            RegisterOther(register, factory);
            return (factory, register);
        }

        public static void RegisterMockedHttpContext(IRegister register)
        {
            register.Register(Mock.Of<HttpContextBase>());
        }
        public static void RegisterMockedUmbracoTypes(IRegister register, IFactory factory)
        {
            register.Register(Mock.Of<ILogger>());
            register.Register(Mock.Of<IExamineManager>());
            register.Register(f => AppCaches.Disabled);
            register.Register(Mock.Of<IUmbracoContextAccessor>());
            register.Register(Mock.Of<IUmbracoDatabaseFactory>());
            register.Register(ServiceContext.CreatePartial());
            register.Register(Mock.Of<IProfilingLogger>());
            register.Register(Mock.Of<IUmbracoContextAccessor>());
            var membershipHelper = new MembershipHelper(
                factory.GetInstance<HttpContextBase>(),
                Mock.Of<IPublishedMemberCache>(),
                Mock.Of<MembershipProvider>(),
                Mock.Of<RoleProvider>(),
                Mock.Of<IMemberService>(),
                Mock.Of<IMemberTypeService>(),
                Mock.Of<IUserService>(),
                Mock.Of<IPublicAccessService>(),
                Mock.Of<AppCaches>(),
                Mock.Of<ILogger>());
            var umbHelper = new UmbracoHelper(
                Mock.Of<IPublishedContent>(),
                Mock.Of<ITagQuery>(),
                Mock.Of<ICultureDictionaryFactory>(),
                Mock.Of<IUmbracoComponentRenderer>(),
                Mock.Of<IPublishedContentQuery>(),
                membershipHelper);
            register.Register(umbHelper);

            Current.Factory = factory;
        }
        public static void RegisterOther(IRegister register, IFactory factory)
        {
        }
        public static void AddOrderInfoToHttpSession(OrderInfo orderInfo, IStore store, OrderServiceMocks orderSvcMocks)
        {
            // Setup HttpContext Session to return same OrderInfo
            string sessKey = new PrivateObject(orderSvcMocks.orderSvc, new PrivateType(typeof(OrderService)))
                .Invoke("CreateKey", store.Alias)
                as string;
            orderSvcMocks.httpCtxMocks.httpSessMock.Setup(s => s[sessKey]).Returns(orderInfo);
            // Setup HttpRequest Cookies to retun oi guid
            var cookie = new HttpCookie(sessKey)
            {
                Value = orderInfo.UniqueId.ToString(),
            };
            orderSvcMocks.httpCtxMocks.httpReqMock.Object.Cookies.Add(cookie);
        }

        public static DiscountCache MockDiscountCache()
            => new DiscountCache(
                Mock.Of<Configuration>(),
                Mock.Of<ILogger>(),
                Mock.Of<IFactory>(),
                Mock.Of<IBaseCache<IStore>>(),
                Mock.Of<IPerStoreFactory<IDiscount>>()
            );
    }
}
