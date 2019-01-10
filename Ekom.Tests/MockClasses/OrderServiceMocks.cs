using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Concurrent;
using System.Web;

namespace Ekom.Tests.MockClasses
{
    class OrderServiceMocks
    {
        public OrderService orderSvc;
        public DiscountCache discountCache;
        public Mock<IOrderRepository> orderRepo;
        public Mock<IActivityLogRepository> activityRepo;
        public HttpContextMocks httpCtxMocks;

        public OrderServiceMocks()
        {
            Helpers.InitMockContainer();
            var httpCtx = Helpers.GetHttpContext();
            var logFac = Helpers.MockLogFac();
            discountCache = Helpers.MockDiscountCache();
            orderRepo = new Mock<IOrderRepository> { DefaultValue = DefaultValue.Mock };
            activityRepo = new Mock<IActivityLogRepository> { DefaultValue = DefaultValue.Mock };

            httpCtxMocks = new HttpContextMocks();
            orderSvc = new OrderService(
                orderRepo.Object,
                activityRepo.Object,
                Helpers.MockLogFac(),
                Mock.Of<IStoreService>(),
                Helpers.GetSetAppCtx(),
                discountCache,
                httpCtxMocks.httpCtxMock.Object
            );

            var ekmReq = new ContentRequest(new HttpContextWrapper(httpCtx), logFac);

            new PrivateObject(orderSvc, new PrivateType(typeof(OrderService)))
                .SetField("_ekmRequest", ekmReq);

            InitDiscountCache();
        }

        public void InitDiscountCache()
        {
            discountCache.GlobalDiscounts["IS"] = new ConcurrentDictionary<Guid, IDiscount>();
        }
    }
}
