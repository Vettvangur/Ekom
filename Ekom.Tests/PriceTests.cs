using Ekom.Models;
using Ekom.Tests.MockClasses;
using Ekom.Tests.Objects;
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
            Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLine(product, 2, store);

            Assert.AreEqual(3000m, oi.OrderLineTotal.Value);
        }

        [TestMethod]
        public void CalculatesVatNotIncludedStorePrice()
        {
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLine(product, 2, store);

            Assert.AreEqual(300m, oi.Vat.Value);
            Assert.AreEqual(3300m, oi.ChargedAmount.Value);
        }

        [TestMethod]
        public void CalculatesVatIncludedStorePrice()
        {
            Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.oldjson, store);

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLine(product, 2, store);

            Assert.AreEqual(500m, oi.Vat.Value);
            Assert.AreEqual(3000m, oi.ChargedAmount.Value);
        }

        [TestMethod]
        public void CanGetWithoutVatFromVatIncluded()
        {
            Helpers.InitMockContainer();

            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.oldjson, store);

            var oi = new OrderInfo(new Models.Data.OrderData(), store);
            var ol = new OrderLine(product, 1, Guid.NewGuid(), oi, null);

            Assert.AreEqual(1500 / 1.2m, ol.Amount.WithoutVat.Value);
        }
    }
}
