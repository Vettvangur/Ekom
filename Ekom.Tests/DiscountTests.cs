using Ekom.Tests.MockClasses;
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

            var orderSvc = new OrderServiceMocks().orderSvc;
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

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLine(product, 2, store);

            Assert.IsTrue(orderSvc.ApplyDiscountToOrder(discount, store.Alias, null, oi));

            Assert.IsTrue(oi.SubTotal.Value == 1500);
        }

        [TestMethod]
        public void DeterminesBestDiscount()
        {
            var container = Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var discountFixed500 = Objects.Objects.Get_Discount_fixed_500();
            var discountPerc50 = Objects.Objects.Get_Discount_percentage_50();

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLine(product, 1, store);

            Assert.IsTrue(orderSvc.ApplyDiscountToOrder(
                discountFixed500,
                store.Alias,
                null,
                oi
            ));
            Assert.IsTrue(orderSvc.ApplyDiscountToOrder(
                discountPerc50,
                store.Alias,
                null,
                oi
            ));

            Assert.IsTrue(oi.SubTotal.Value == 750);
        }

        [TestMethod]
        public void AppliesFixedDiscountToOrderLine()
        {
            var container = Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product2 = Objects.Objects.Get_Shirt2_Product();
            var product3 = Objects.Objects.Get_Shirt3_Product();

            var discountFixed500 = Objects.Objects.Get_Discount_fixed_500();

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLine(product2, 1, store);
            Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
            oi = orderSvc.AddOrderLine(product3, 1, store);

            Assert.IsTrue(orderSvc.ApplyDiscountToOrderLineProduct(
                product3,
                discountFixed500,
                store.Alias,
                coupon: null,
                orderInfo: oi
            ));

            Assert.IsTrue(oi.SubTotal.Value == 4990);
        }

        [TestMethod]
        public void AppliesFixedDiscountToOrderLine2()
        {
            var container = Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product2 = Objects.Objects.Get_Shirt2_Product();
            var product3 = Objects.Objects.Get_Shirt3_Product();

            var discountFixed500 = Objects.Objects.Get_Discount_fixed_500();

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLine(product2, 1, store);
            Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);

            Assert.IsTrue(orderSvc.ApplyDiscountToOrderLineProduct(
                product2,
                discountFixed500,
                store.Alias,
                null,
                oi
            ));

            oi = orderSvc.AddOrderLine(product3, 1, store);


            Assert.IsTrue(oi.SubTotal.Value == 4990);
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

        [TestMethod]
        public void GlobalDiscountGetsApplied()
        {
            var container = Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product2 = Objects.Objects.Get_Shirt2_Product();

            var discountPerc50 = Objects.Objects.Get_Discount_percentage_50();

            var orderSvcMocks = new OrderServiceMocks();
            orderSvcMocks.discountCache.GlobalDiscounts[store.Alias][discountPerc50.Key] = discountPerc50;
            var orderSvc = orderSvcMocks.orderSvc;

            var oi = orderSvc.AddOrderLine(product2, 1, store);
            Assert.IsTrue(oi.SubTotal.Value == 1995);
        }

        [TestMethod]
        public void NonCompliantDiscountGetsRemoved()
        {
            var container = Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product3 = Objects.Objects.Get_Shirt3_Product();
            var discount_fixed_1000_Min_2000 = Objects.Objects.Get_Discount_fixed_1000_Min_2000();

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLine(product3, 2, store);

            Assert.IsTrue(orderSvc.ApplyDiscountToOrder(
                discount_fixed_1000_Min_2000,
                store.Alias,
                null,
                oi
            ));

            Assert.IsTrue(oi.SubTotal.Value == 1000);

            Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
            oi = orderSvc.AddOrderLine(product3, -1, store);

            Assert.IsTrue(oi.SubTotal.Value == 1500);
        }
    }
}
