using Ekom.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace Ekom.Tests
{
    [TestClass]
    public class PriceTests
    {
        /// <summary>
        /// Two products with price of 1500 per item
        /// </summary>
        [TestMethod]
        public void CalculatesPriceCorrectly()
        {
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvc = Helpers.GetOrderService();
            var oi = orderSvc.AddOrderLine(product, 2, store);

            Assert.IsTrue(oi.OrderLineTotal.Value == 3000);
        }

        [TestMethod]
        public void CalculatesVatNotIncludedStorePrice()
        {
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvc = Helpers.GetOrderService();
            var oi = orderSvc.AddOrderLine(product, 2, store);

            Assert.IsTrue(oi.Vat.Value == 300);
            Assert.IsTrue(oi.ChargedAmount.Value == 3300);
        }

        [TestMethod]
        public void CalculatesVatIncludedStorePrice()
        {
            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvc = Helpers.GetOrderService();
            var oi = orderSvc.AddOrderLine(product, 2, store);

            Assert.IsTrue(oi.Vat.Value == 500);
            Assert.IsTrue(oi.ChargedAmount.Value == 3000);
        }

        [TestMethod]
        public void CanGetWithoutVatFromVatIncluded()
        {
            Helpers.InitMockContainer();

            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = Objects.Objects.Get_Shirt3_Product();

            var oi = new OrderInfo(new Models.Data.OrderData(), store);
            var ol = new OrderLine(product, 1, Guid.NewGuid(), oi, null);

            Assert.IsTrue(ol.Amount.WithoutVat.Value == 1500 / 1.2m);
        }
    }
}
