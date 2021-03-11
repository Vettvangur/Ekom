using Ekom.Interfaces;
using Ekom.Tests.Objects;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Moq;
using System;
using Ekom.Cache;
using System.Collections.Concurrent;
using Ekom.Services;
using Ekom.Tests.MockClasses;
using Umbraco.Core.Cache;
using Umbraco.Core.Logging;

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

        //[TestCategory("Calculate ProductDiscount")]
        //[TestMethod]
        //public void Percentages_NoRange_IS()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    var store = Objects.Objects.Get_IS_Store_Vat_Included();

        //    var storeSvc = new Mock<API.Store>(
        //        AppCaches.Disabled,
        //        Mock.Of<ILogger>(),
        //        Mock.Of<IStoreService>(x => x.GetStoreFromCache() == store)
        //    );
        //    reg.Register(storeSvc.Object);

        //    var discount = new CustomDiscount(ProductDiscount_Percent_20.json2, store);
        //    var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discount);
        //    var productDiscountService = new ProductDiscountService(cache);
        //    reg.Register<IProductDiscountService>(productDiscountService);

        //    var product = new CustomProduct(Shirt_product_3.json, store);

        //    Assert.IsTrue(product.Price.OriginalValue - product.Price.OriginalValue * 0.20m == product.Price.WithVat.Value);
        //}

        //[TestMethod]
        //public void GlobalDiscountGetsApplied()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();

        //    new UmbracoHelperCreator(reg, fac);

        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();

        //    var discountPerc50 = Objects.Objects.Get_ProductDiscount_percentage_50();

        //    var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discountPerc50);
        //    var productDiscountService = new ProductDiscountService(cache);
        //    reg.Register<IProductDiscountService>(productDiscountService);

        //    var product2 = Objects.Objects.Get_Shirt2_Product();

        //    var orderSvc = new OrderServiceMocks().orderSvc;

        //    var oi = orderSvc.AddOrderLineAsync(product2, 1, store).Result;
        //    Assert.AreEqual(1995, oi.SubTotal.Value);
        //}

        //[TestMethod]
        //public void Fixed_PriceRange_ShouldGiveDiscount_IS()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    var store = Objects.Objects.Get_IS_Store_Vat_Included();

        //    var storeSvc = new Mock<API.Store>(
        //        AppCaches.Disabled,
        //        Mock.Of<ILogger>(),
        //        Mock.Of<IStoreService>(x => x.GetStoreFromCache() == store)
        //    );
        //    reg.Register(storeSvc.Object);

        //    var discount = new CustomDiscount(ProductDiscount_Fixed.limited1000, store);
        //    var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discount);
        //    var productDiscountService = new ProductDiscountService(cache);
        //    reg.Register<IProductDiscountService>(productDiscountService);

        //    var product = new CustomProduct(Shirt_product_3.json, store);
        //    Assert.IsTrue(product.Price.OriginalValue - 1000 == product.Price.WithVat.Value);
        //}

        //[TestMethod]
        //public void Fixed_PriceRange_ShouldNotGiveDiscount_ProductPriceTooLow_IS()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    var store = Objects.Objects.Get_IS_Store_Vat_Included();

        //    var storeSvc = new Mock<API.Store>(
        //        AppCaches.Disabled,
        //        Mock.Of<ILogger>(),
        //        Mock.Of<IStoreService>(x => x.GetStoreFromCache() == store)
        //    );
        //    reg.Register(storeSvc.Object);

        //    var discount = new CustomDiscount(ProductDiscount_Fixed.limited1000, store);
        //    var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discount);
        //    var productDiscountService = new ProductDiscountService(cache);
        //    reg.Register<IProductDiscountService>(productDiscountService);

        //    var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_Low, store);
        //    Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        //}
        //[TestMethod]
        //public void Fixed_PriceRange_ShouldNotGiveDiscount_ProductPriceTooHigh_IS()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    var store = Objects.Objects.Get_IS_Store_Vat_Included();

        //    var storeSvc = new Mock<API.Store>(
        //        AppCaches.Disabled,
        //        Mock.Of<ILogger>(),
        //        Mock.Of<IStoreService>(x => x.GetStoreFromCache() == store)
        //    );
        //    reg.Register(storeSvc.Object);

        //    var discount = new CustomDiscount(ProductDiscount_Fixed.limited1000, store);
        //    var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discount);
        //    var productDiscountService = new ProductDiscountService(cache);
        //    reg.Register<IProductDiscountService>(productDiscountService);

        //    var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_High, store);
        //    Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        //}
        //[TestMethod]
        //public void Percentages_NoRange_DK()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    var store = Objects.Objects.Get_DK_Store_Vat_Included();

        //    var storeSvc = new Mock<API.Store>(
        //        AppCaches.Disabled,
        //        Mock.Of<ILogger>(),
        //        Mock.Of<IStoreService>(x => x.GetStoreFromCache() == store)
        //    );
        //    reg.Register(storeSvc.Object);

        //    var discount = new CustomDiscount(ProductDiscount_Percent_20.json2, store);
        //    var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discount);
        //    var productDiscountService = new ProductDiscountService(cache);
        //    reg.Register<IProductDiscountService>(productDiscountService);

        //    var product = new CustomProduct(Shirt_product_3.json, store);
        //    Assert.IsTrue(product.Price.OriginalValue - product.Price.OriginalValue * 0.20m == product.Price.WithVat.Value);
        //}
        //[TestMethod]
        //public void Fixed_PriceRange_ShouldGiveDiscount_DK()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    var store = Objects.Objects.Get_DK_Store_Vat_Included();

        //    var storeSvc = new Mock<API.Store>(
        //        AppCaches.Disabled,
        //        Mock.Of<ILogger>(),
        //        Mock.Of<IStoreService>(x => x.GetStoreFromCache() == store)
        //    );
        //    reg.Register(storeSvc.Object);

        //    var discount = new CustomDiscount(ProductDiscount_Fixed.limited1000, store);
        //    var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discount);
        //    var productDiscountService = new ProductDiscountService(cache);
        //    reg.Register<IProductDiscountService>(productDiscountService);

        //    var product = new CustomProduct(Shirt_product_3.json, store);
        //    Assert.IsTrue(product.Price.OriginalValue - 5 == product.Price.WithVat.Value);

        //}
        //[TestMethod]
        //public void Fixed_PriceRange_ShouldNotGiveDiscount_ProductPriceTooLow_DK()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    var store = Objects.Objects.Get_DK_Store_Vat_Included();

        //    var storeSvc = new Mock<API.Store>(
        //        AppCaches.Disabled,
        //        Mock.Of<ILogger>(),
        //        Mock.Of<IStoreService>(x => x.GetStoreFromCache() == store)
        //    );
        //    reg.Register(storeSvc.Object);

        //    var discount = new CustomDiscount(ProductDiscount_Fixed.limited1000, store);
        //    var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discount);
        //    var productDiscountService = new ProductDiscountService(cache);
        //    reg.Register<IProductDiscountService>(productDiscountService);

        //    var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_Low, store);
        //    Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        //}
        //[TestMethod]
        //public void Fixed_PriceRange_ShouldNotGiveDiscount_ProductPriceTooHigh_DK()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    var store = Objects.Objects.Get_DK_Store_Vat_Included();

        //    var storeSvc = new Mock<API.Store>(
        //        AppCaches.Disabled,
        //        Mock.Of<ILogger>(),
        //        Mock.Of<IStoreService>(x => x.GetStoreFromCache() == store)
        //    );
        //    reg.Register(storeSvc.Object);

        //    var discount = new CustomDiscount(ProductDiscount_Fixed.limited1000, store);
        //    var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discount);
        //    var productDiscountService = new ProductDiscountService(cache);
        //    reg.Register<IProductDiscountService>(productDiscountService);

        //    var product = new CustomProduct(Shirt_product_3.Discount_Price_Too_High, store);
        //    Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        //}

        //[TestMethod]
        //public void Unlinked_ProductDiscount_NotApplied()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();

        //    new UmbracoHelperCreator(reg, fac);

        //    var store = Objects.Objects.Get_DK_Store_Vat_Included();

        //    var discountPerc50 = Objects.Objects.Get_ProductDiscount_percentage_50();
        //    var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discountPerc50);
        //    var productDiscountService = new ProductDiscountService(cache);
        //    reg.Register<IProductDiscountService>(productDiscountService);

        //    var product = new CustomProduct(Shirt_product_2.json, store);
        //    Assert.IsTrue(product.Price.OriginalValue == product.Price.WithVat.Value);
        //}
    }
}
