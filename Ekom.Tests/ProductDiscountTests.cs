using Ekom.Tests.Objects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Umbraco.Core.Composing;

namespace Ekom.Tests
{
    [TestClass]
    public class ProductDiscountTests
    {
        [TestCleanup]
        public void TearDown()
        {
            Current.Reset();
        }

        [TestCategory("Calculate ProductDiscount")]
        [TestMethod]
        public void Percentages_NoRange_IS()
        {
            Helpers.RegisterAll();
            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.json, store, ProductDiscount_Percent_20.json2);
            Assert.IsTrue(product.Price.OriginalValue - product.Price.OriginalValue * 0.20m == product.Price.WithVat.Value);
        }
        [TestMethod]
        public void Fixed_WithinRange_ShouldGiveDiscount_IS()
        {
            Helpers.RegisterAll();
            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.json, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue - 1000 == product.Price.WithVat.Value);

        }
        [TestMethod]
        public void Fixed_WithinRange_ShouldNotGiveDiscount_ProductPriceTooLow_IS()
        {
            Helpers.RegisterAll();
            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_Low, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        }
        [TestMethod]
        public void Fixed_WithinRange_ShouldNotGiveDiscount_ProductPriceTooHigh_IS()
        {
            Helpers.RegisterAll();
            var store = Objects.Objects.Get_IS_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_High, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        }
        [TestMethod]
        public void Percentages_NoRange_DK()
        {
            Helpers.RegisterAll();
            var store = Objects.Objects.Get_DK_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.json, store, ProductDiscount_Percent_20.json2);
            Assert.IsTrue(product.Price.OriginalValue - product.Price.OriginalValue * 0.20m == product.Price.WithVat.Value);

        }
        [TestMethod]
        public void Fixed_WithinRange_ShouldGiveDiscount_DK()
        {
            Helpers.RegisterAll();
            var store = Objects.Objects.Get_DK_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.json, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue - 5 == product.Price.WithVat.Value);

        }
        [TestMethod]
        public void Fixed_WithinRange_ShouldNotGiveDiscount_ProductPriceTooLow_DK()
        {
            Helpers.RegisterAll();
            var store = Objects.Objects.Get_DK_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_Low, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        }
        [TestMethod]
        public void Fixed_WithinRange_ShouldNotGiveDiscount_ProductPriceTooHigh_DK()
        {
            Helpers.RegisterAll();
            var store = Objects.Objects.Get_DK_Store_Vat_Included();
            var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_High, store, ProductDiscount_Fixed.limited1000);
            Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        }
    }
}
