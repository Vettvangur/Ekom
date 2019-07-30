using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Ekom.Tests.MockClasses;
using Examine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.IO;
using System.Web;
using System.Web.Security;
using Umbraco.Core;
using Umbraco.Core.Cache;
using Umbraco.Core.Composing;
using Umbraco.Core.Dictionary;
using Umbraco.Core.Logging;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Core.Persistence;
using Umbraco.Core.Services;
using Umbraco.Web;
using Umbraco.Web.PublishedCache;
using Umbraco.Web.Security;

namespace Ekom.Tests
{
    static class Helpers
    {
        public static HttpContext GetHttpContext()
        {
            var tw = new Mock<TextWriter>();
            var req = new HttpRequest("", "", "");
            var resp = new HttpResponse(tw.Object);

            return new HttpContext(req, resp);
        }

        public static (IFactory, IRegister) RegisterAll()
        {
            var register = RegisterFactory.Create();
            var factory = register.CreateFactory();
            //new Registrations().DoCompose(register);

            RegisterMockedHttpContext(register);
            RegisterMockedUmbracoTypes(register, factory);

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
    }
}
