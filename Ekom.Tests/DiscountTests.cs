using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ekom.Tests
{
    [TestClass]
    public class DiscountTests
    {
        [TestMethod]
        public void AppliesFixedDiscountToOrder()
        {
            var container = Helpers.InitMockContainer();

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var discount = Objects.Objects.Get_Discount_fixed_500();

            var orderSvc = Helpers.GetOrderService();
            var oi = orderSvc.AddOrderLine(product, 2, store);

            Assert.IsTrue(orderSvc.ApplyDiscountToOrder(discount, store.Alias, null, oi));

            Assert.IsTrue(oi.SubTotal.Value == 2000);
        }

        [TestMethod]
        public void AppliesPercentageDiscountToOrder()
        {
            var container = Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var discount = Objects.Objects.Get_Discount_percentage_50();

            var orderSvc = Helpers.GetOrderService();
            var oi = orderSvc.AddOrderLine(product, 2, store);

            Assert.IsTrue(orderSvc.ApplyDiscountToOrder(discount, store.Alias, null, oi));

            Assert.IsTrue(oi.SubTotal.Value == 1500);
        }

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

        //[TestMethod]
        public void GlobalDiscountGetsApplied()
        {

        }

        //[TestMethod]
        public void NonCompliantDiscountGetsRemoved()
        {

        }
    }
}
