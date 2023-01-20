//using Ekom.Cache;
//using Ekom.Interfaces;
//using Ekom.Models;
//using Ekom.Services;
//using Ekom.Tests.MockClasses;
//using Examine;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Moq;
//using System;
//using System.Collections.Concurrent;
//using System.Collections.Generic;
//using System.IO;
//using System.Linq;
//using System.Text;
//using System.Web;
//using System.Web.Caching;
//using System.Web.Hosting;
//using System.Web.Security;
//using Umbraco.Core;
//using Umbraco.Core.Cache;
//using Umbraco.Core.Composing;
//using Umbraco.Core.Configuration;
//using Umbraco.Core.Configuration.UmbracoSettings;
//using Umbraco.Core.Dictionary;
//using Umbraco.Core.Logging;
//using Umbraco.Core.Models.PublishedContent;
//using Umbraco.Core.Persistence;
//using Umbraco.Core.Persistence.SqlSyntax;
//using Umbraco.Core.Services;
//using Umbraco.Web;
//using Umbraco.Web.PublishedCache;
//using Umbraco.Web.Routing;
//using Umbraco.Web.Security;

//namespace Ekom.Tests
//{
//    static class Helpers
//    {
//        // dummy TextWriter that does not write
//        private class NullWriter : TextWriter
//        {
//            public override Encoding Encoding => Encoding.UTF8;
//        }

//        public static HttpContext GetSetHttpContext()
//            => HttpContext.Current = new HttpContext(
//                new SimpleWorkerRequest("/", "", "null.aspx", "", new NullWriter())
//            );

//        public static (IFactory, IRegister) RegisterAll()
//        {
//            var register = RegisterFactory.Create();
//            var factory = register.CreateFactory();
//            //new Registrations().DoCompose(register);

//            RegisterMockedHttpContext(register);
//            RegisterMockedUmbracoTypes(register, factory);
//            RegisterEkom(register, factory);
//            return (factory, register);
//        }

//        public static void RegisterMockedHttpContext(IRegister register)
//        {
//            register.Register(Mock.Of<HttpContextBase>());
//        }
//        public static void RegisterMockedUmbracoTypes(IRegister register, IFactory factory)
//        {
//            register.Register(Mock.Of<ILogger>());
//            register.Register(Mock.Of<IExamineManager>());
//            register.Register(f => AppCaches.Disabled);
//            register.Register(Mock.Of<IUmbracoContextAccessor>());
//            register.Register(Mock.Of<IUmbracoDatabaseFactory>());
//            register.Register(ServiceContext.CreatePartial());
//            register.Register(Mock.Of<IProfilingLogger>());
//            register.Register(Mock.Of<IUmbracoContextAccessor>());
//            //register.Register(Mock.Of<AppCaches>());

//            Current.Factory = factory;
//        }
//        public static void RegisterEkom(IRegister register, IFactory factory)
//        {
//            register.Register(new Configuration());
//            register.Register(Mock.Of<IBaseCache<IZone>>());
//            register.Register(Mock.Of<IExamineService>());
//        }

//        /// <summary>
//        /// For some inexplicable reason the following registrations block further registrations from
//        /// overriding previous ones. Even for different types, f.x. a .Register<IExamineService>
//        /// </summary>
//        /// <param name="register"></param>
//        /// <param name="factory"></param>
//        public static void RegisterUmbracoHelper(IRegister register, IFactory factory)
//        {
//            var membershipHelper = new MembershipHelper(
//                factory.GetInstance<HttpContextBase>(),
//                Mock.Of<IPublishedMemberCache>(),
//                Mock.Of<MembershipProvider>(),
//                Mock.Of<RoleProvider>(),
//                Mock.Of<IMemberService>(),
//                Mock.Of<IMemberTypeService>(),
//                Mock.Of<IUserService>(),
//                Mock.Of<IPublicAccessService>(),
//                Mock.Of<AppCaches>(),
//                Mock.Of<ILogger>());
//            var umbHelper = new UmbracoHelper(
//                Mock.Of<IPublishedContent>(),
//                Mock.Of<ITagQuery>(),
//                Mock.Of<ICultureDictionaryFactory>(),
//                Mock.Of<IUmbracoComponentRenderer>(),
//                Mock.Of<IPublishedContentQuery>(),
//                membershipHelper);
//            register.Register(umbHelper);
//        }

//        public static void AddOrderInfoToHttpSession(OrderInfo orderInfo, IStore store, OrderServiceMocks orderSvcMocks)
//        {
//            // Setup HttpContext Session to return same OrderInfo
//            string sessKey = new PrivateObject(orderSvcMocks.orderSvc, new PrivateType(typeof(OrderService)))
//                .Invoke("CreateKey", store.Alias)
//                as string;
//            orderSvcMocks.RuntimeCache.Setup(
//                s => s.Get(
//                    It.Is<string>(x => x == orderInfo.UniqueId.ToString()), 
//                    It.IsAny<Func<object>>(),
//                    It.IsAny<TimeSpan>(),
//                    It.IsAny<bool>(),
//                    It.IsAny<CacheItemPriority>(),
//                    It.IsAny<CacheItemRemovedCallback>(),
//                    It.IsAny<string[]>()
//                )).Returns(orderInfo);

//            // Setup HttpRequest Cookies to retun oi guid
//            var cookie = new HttpCookie(sessKey)
//            {
//                Value = orderInfo.UniqueId.ToString(),
//            };
//            orderSvcMocks.httpCtxMocks.httpReqMock.Object.Cookies.Add(cookie);
//        }

//        public static DiscountCache MockDiscountCache()
//            => new DiscountCache(
//                Mock.Of<Configuration>(),
//                Mock.Of<ILogger>(),
//                Mock.Of<IFactory>(),
//                Mock.Of<IBaseCache<IStore>>(),
//                Mock.Of<IPerStoreFactory<IDiscount>>()
//            );

//        public static IPerStoreCache<IProductDiscount> CreateGlobalDiscountCacheWithDiscount(string storeAlias, params IProductDiscount[] discounts)
//        {
//            var dictionary = new ConcurrentDictionary<string, ConcurrentDictionary<Guid, IProductDiscount>>();
//            dictionary[storeAlias] = new ConcurrentDictionary<Guid, IProductDiscount>();
//            foreach (var discount in discounts)
//            {
//                dictionary[storeAlias][discount.Key] = discount;
//            }

//            var mockGlobalDiscountCache = new Mock<IPerStoreCache<IProductDiscount>>();
//            mockGlobalDiscountCache.Setup(x => x.Cache).Returns(dictionary);

//            return mockGlobalDiscountCache.Object;
//        }
//    }
//}
