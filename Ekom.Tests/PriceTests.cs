using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Tests.MockClasses;
using Ekom.Tests.Objects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using Umbraco.Core;
using Umbraco.Core.Composing;

namespace Ekom.Tests
{
    [TestClass]
    public class PriceTests
    {
        [TestCleanup]
        public void TearDown()
        {
            Current.Reset();
        }

        /// <summary>
        /// Two products with price of 1500 per item
        /// </summary>
        [TestMethod]
        public void CalculatesPriceCorrectly()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());
            Helpers.RegisterUmbracoHelper(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;

            Assert.AreEqual(3300m, oi.OrderLineTotal.Value);
        }

        [TestMethod]
        public void CalculatesVatNotIncludedStorePrice()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());
            Helpers.RegisterUmbracoHelper(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;

            Assert.AreEqual(300m, oi.Vat.Value);
            Assert.AreEqual(3300m, oi.ChargedAmount.Value);
        }

        [TestMethod]
        public void CalculatesVatIncludedStorePrice()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());
            Helpers.RegisterUmbracoHelper(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.oldjson, store);

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;

            Assert.AreEqual(500m, oi.Vat.Value);
            Assert.AreEqual(3000m, oi.ChargedAmount.Value);
        }

        [TestMethod]
        public void CanGetWithoutVatFromVatIncluded()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());

            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.oldjson, store);

            var oi = new OrderInfo(new Models.Data.OrderData(), store);
            var ol = new OrderLine(product, 1, Guid.NewGuid(), oi, null);

            Assert.AreEqual(1500 / 1.2m, ol.Amount.WithoutVat.Value);
        }
    }
}
