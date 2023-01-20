//using Ekom.Cache;
//using Ekom.Interfaces;
//using Ekom.Models;
//using Ekom.Services;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Moq;
//using System;
//using System.Collections.Concurrent;
//using System.Web;
//using Umbraco.Core.Cache;
//using Umbraco.Core.Logging;
//using Umbraco.Core.Services;

//namespace Ekom.Tests.MockClasses
//{
//    class OrderServiceMocks
//    {
//        public OrderService orderSvc;
//        public DiscountCache discountCache;
//        public Mock<IOrderRepository> orderRepo;
//        public Mock<IActivityLogRepository> activityRepo;
//        public HttpContextMocks httpCtxMocks;
//        public AppCaches AppCaches;
//        public Mock<IAppCache> RequestCache;
//        public Mock<IAppPolicyCache> RuntimeCache;
//        public ContentRequest ContentRequest;

//        public OrderServiceMocks()
//        {
//            discountCache = Helpers.MockDiscountCache();
//            orderRepo = new Mock<IOrderRepository> { DefaultValue = DefaultValue.Mock };
//            activityRepo = new Mock<IActivityLogRepository> { DefaultValue = DefaultValue.Mock };
//            RequestCache = new Mock<IAppCache> { DefaultValue = DefaultValue.Mock };
//            RuntimeCache = new Mock<IAppPolicyCache> { DefaultValue = DefaultValue.Mock };
//            AppCaches = new AppCaches(RuntimeCache.Object, RequestCache.Object, new IsolatedCaches(_ => NoAppCache.Instance));
//            httpCtxMocks = new HttpContextMocks();
//            ContentRequest = new ContentRequest(httpCtxMocks.httpCtxMock.Object, Mock.Of<ILogger>());

//            RequestCache.Setup(
//                x => x.Get(It.Is<string>(y => y == "ekmRequest"))
//            )
//                .Returns(ContentRequest);

//            orderSvc = new OrderService(
//                orderRepo.Object,
//                Mock.Of<ICouponRepository>(),
//                activityRepo.Object,
//                Mock.Of<ILogger>(),
//                Mock.Of<IStoreService>(),
//                AppCaches,
//                Mock.Of<IMemberService>(),
//                discountCache,
//                httpCtxMocks.httpCtxMock.Object
//            );

//            var ekmReq = new ContentRequest(httpCtxMocks.httpCtxMock.Object, Mock.Of<ILogger>());

//            new PrivateObject(orderSvc, new PrivateType(typeof(OrderService)))
//                .SetField("_ekmRequest", ekmReq);

//            InitDiscountCache();
//        }

//        public void InitDiscountCache()
//        {
//            discountCache.Cache["IS"] = new ConcurrentDictionary<Guid, IDiscount>();
//        }
//    }
//}
