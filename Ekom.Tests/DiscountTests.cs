using Ekom;
using Ekom.App_Start;
using Ekom.Cache;
using Ekom.Controllers;
using Ekom.Interfaces;
using Ekom.Models.Discounts;
using Ekom.Repository;
using Ekom.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Persistence;

namespace Ekom.Tests
{
    [TestClass]
    public class DiscountTests
    {
        //[TestMethod]
        //public void AppliesDiscountToOrder()
        //{
        //    var appCtx = Helpers.GetSetAppCtx();
        //    var orderRepo = new OrderRepository(new Configuration(), appCtx, Mock.Of<ILogFactory>());
        //    var orderSvc = new OrderService(orderRepo, Mock.Of<ILogFactory>(), Mock.Of<IStoreService>(), Mock.Of<IPerStoreCache<Discount>>(), appCtx);

        //    //orderSvc.ApplyDiscountToOrder()
        //}

        //[TestMethod]
        //public void ControllerResponseBadRequestWithBadParameters_AddOrderLine()
        //{
        //    Helpers.GetSetAppUmbracoCtx();

        //    var container = UnityConfig.GetConfiguredContainer();

        //    var httpCtx = new Mock<HttpContextBase>();
        //    container.RegisterInstance(httpCtx.Object);

        //    var ctrl = container.Resolve<OrderController>();

        //    //ctrl.ApplyDiscountToOrder()
        //}
    }
}
