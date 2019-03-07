using Ekom.Interfaces;
using Ekom.Tests.Objects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Ekom.Tests
{
    [TestClass]
    public class ProductDiscountTests
    {
        [TestCategory( "Calculate ProductDiscount" )]
        [TestMethod]
        public void TestProductDiscountPercentagesNoRange_IS()
        {
            Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.json, store,ProductDiscount_Percent_20.json2);
            Assert.IsTrue(product.Price.OriginalValue - product.Price.OriginalValue * 0.20m == product.Price.WithVat.Value);
            
        }
        [TestMethod]
        public void TestProductDiscountFixedWithinRangeShouldGiveDiscount_IS()
        {
            Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.json, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue - 1000 == product.Price.WithVat.Value);

        }
        [TestMethod]
        public void TestProductDiscountFixedWithinRangeShouldNotGiveDiscountProductPriceTooLow_IS()
        {
            Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_Low, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        }
        [TestMethod]
        public void TestProductDiscountFixedWithinRangeShouldNotGiveDiscountProductPriceTooHigh_IS()
        {
            Helpers.InitMockContainer();
            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_High, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        }
        [TestMethod]
        public void TestProductDiscountPercentagesNoRange_DK()
        {
            Helpers.InitMockContainer();
            var store = Objects.Objects.Get_DK_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.json, store, ProductDiscount_Percent_20.json2);
            Assert.IsTrue(product.Price.OriginalValue - product.Price.OriginalValue * 0.20m == product.Price.WithVat.Value);

        }
        [TestMethod]
        public void TestProductDiscountFixedWithinRangeShouldGiveDiscount_DK()
        {
            Helpers.InitMockContainer();
            var store = Objects.Objects.Get_DK_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.json, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue - 5 == product.Price.WithVat.Value);

        }
        [TestMethod]
        public void TestProductDiscountFixedWithinRangeShouldNotGiveDiscountProductPriceTooLow_DK()
        {
            Helpers.InitMockContainer();
            var store = Objects.Objects.Get_DK_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_Low, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        }
        [TestMethod]
        public void TestProductDiscountFixedWithinRangeShouldNotGiveDiscountProductPriceTooHigh_DK()
        {
            Helpers.InitMockContainer();
            var store = Objects.Objects.Get_DK_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_High, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        }
    }
}
