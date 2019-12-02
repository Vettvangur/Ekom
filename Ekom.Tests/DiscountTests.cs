using Ekom.Controllers;
using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Models.Discounts;
using Ekom.Models.OrderedObjects;
using Ekom.Services;
using Ekom.Tests;
using Ekom.Tests.MockClasses;
using Ekom.Tests.Utilities;
using Examine;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models.PublishedContent;
using Umbraco.Web;

namespace Ekom.Tests
{
    [TestClass]
    public class DiscountTests
    {
        [TestCleanup]
        public void TearDown()
        {
            Current.Reset();
        }

        const string OrderedDiscount = @"{""Key"": ""9f01d763-635e-41c5-8787-d405f5852940"",""Stackable"": false, ""Amount"": 0.2, ""Type"": 1, ""DiscountItems"": [], ""Constraints"": {""StartRange"": 0,""EndRange"": 0,""CountriesInZone"": []},""Coupons"": [],""HasMasterStock"": false}";

        [TestMethod]
        public void DeserialisesOrderedDiscount()
        {
            var od = JsonConvert.DeserializeObject<OrderedDiscount>(OrderedDiscount);

            Assert.IsNotNull(od);
            Assert.AreEqual(DiscountType.Percentage, od.Type);
            Assert.AreEqual(0.2m, od.Amount);
        }

        const string OrderInfoWithLineDiscount = @"{""StoreInfo"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":[{""CurrencyValue"":""is-IS"",""Currency"":""is-IS"", ""CurrencyFormat"": ""C""}],""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""Discount"":{""Key"":""9f01d763-635e-41c5-8787-d405f5852940"",""Amount"":0.2, ""Type"": 1, ""Stackable"": true, ""Constraints"":{""StartRange"":0,""EndRange"":0,""CountriesInZone"":[]},""Coupons"":[],""HasMasterStock"":false},""Coupon"":null,""UniqueId"":""bd43d2a8-46ae-45b4-aba8-762f32693652"",""ReferenceId"":66,""OrderNumber"":""IS0066"",""OrderLines"":[{""ProductKey"":""017b5721-ced9-406e-80cd-309c95b0ab63"",""Key"":""745b63ec-8347-4960-b0aa-06665682d7ab"",""Product"":{""Properties"":{""id"":""1081"",""key"":""017b5721-ced9-406e-80cd-309c95b0ab63"",""parentID"":""1069"",""level"":""5"",""writerID"":""1"",""creatorID"":""0"",""nodeType"":""1058"",""template"":""1084"",""sortOrder"":""3"",""createDate"":""20160907134948000"",""updateDate"":""20171123100029000"",""nodeName"":""Pants Product 2"",""urlName"":""pants-product-2"",""writerName"":""gardar@vettvangur.is"",""creatorName"":""Vettvangur@vettvangur.is"",""nodeTypeAlias"":""ekmProduct"",""path"":""-1,1066,1067,1179,1069,1081"",""disable"":""{\""values\"":{\""IS\"":\""0\"",\""EN\"":\""0\"",\""DK\"":\""0\"",\""EU\"":\""0\""},\""dtdGuid\"":\""383bb1cf-eb59-4bff-b5de-48f17f8d3bef\""}"",""price"":""{\""values\"":{\""IS\"":\""17990\"",\""EN\"":\""345\"",\""DK\"":\""566\"",\""EU\"":\""34535\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}"",""sku"":""women-sku-pants-2"",""slug"":""{\""values\"":{\""IS\"":\""buxur-vara-2\"",\""EN\"":\""pants-product-2\"",\""DK\"":\""pants-product-2\"",\""EU\"":\""pants-product-2\""},\""dtdGuid\"":\""93f62b61-ec87-49f4-98a3-fb0d4eff20ab\""}"",""title"":""{\""values\"":{\""IS\"":\""Buxur vara 2\"",\""EN\"":\""Pants Product 2\"",\""DK\"":\""Pants Product 2\"",\""EU\"":\""Pants Product 2\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}""},""Price"":{""Discount"":null,""Store"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":[{""CurrencyValue"":""is-IS"",""Currency"":""is-IS"", ""CurrencyFormat"": ""C""}],""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""DiscountAlwaysBeforeVAT"":false,""OriginalValue"":17990.0,""Quantity"":1,""Value"":19789.0},""ImageIds"":[],""VariantGroups"":[]},""Quantity"":2,""Discount"":{""Key"":""1506f28f-6397-4fd6-b330-9e2cabd50a57"",""Amount"": 500.0, ""Type"": 0, ""Constraints"":{""StartRange"":0,""EndRange"":0,""CountriesInZone"":[]},""Coupons"":[""sup""],""HasMasterStock"":false},""Coupon"":null,""Amount"":{""Discount"":{""Key"":""1506f28f-6397-4fd6-b330-9e2cabd50a57"",""Amount"": 500.0, ""Type"": 0, ""Constraints"":{""StartRange"":0,""EndRange"":0,""CountriesInZone"":[]},""Coupons"":[""sup""],""HasMasterStock"":false},""Store"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":[{""CurrencyValue"":""is-IS"",""Currency"":""is-IS"", ""CurrencyFormat"": ""C""}],""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""DiscountAlwaysBeforeVAT"":false,""OriginalValue"":17990.0,""Quantity"":2,""Value"":38478.0}}],""ShippingProvider"":null,""PaymentProvider"":null,""TotalQuantity"":2,""CustomerInformation"":{""CustomerIpAddress"":""::1"",""Customer"":{""Properties"":{},""Name"":"""",""Email"":"""",""Address"":"""",""City"":"""",""Country"":"""",""ZipCode"":"""",""Phone"":"""",""UserId"":0,""UserName"":null},""Shipping"":{""Properties"":{},""Name"":"""",""Address"":"""",""City"":"""",""Country"":"""",""ZipCode"":""""}},""OrderLineTotal"":{""Value"":35980.0,""CurrencyString"":""35.980 ISK""},""SubTotal"":{""Value"":34980.0,""CurrencyString"":""34.980 ISK""},""Vat"":{""Value"":3498.0,""CurrencyString"":""3.498 ISK""},""GrandTotal"":{""Value"":38478.0,""CurrencyString"":""38.478 ISK""},""ChargedAmount"":{""Value"":38478.0,""CurrencyString"":""38.478 ISK""},""CreateDate"":""2018-03-20T15:19:54.4137102+00:00"",""UpdateDate"":""0001-01-01T00:00:00"",""PaidDate"":""0001-01-01T00:00:00"",""OrderStatus"":3,""HangfireJobs"":[]}";

        [TestMethod]
        public void DeserialisesOrderInfoWithLineDiscount()
        {
            Helpers.RegisterAll();
            var orderData = new OrderData
            {
                OrderInfo = OrderInfoWithLineDiscount,
            };

            var orderInfo = new OrderInfo(orderData);

            Assert.AreEqual(1, orderInfo.OrderLines.Count);
            Assert.AreEqual(500m, orderInfo.OrderLines.First().Discount.Amount);

            try
            {
                Assert.AreEqual("38.478 kr", orderInfo.ChargedAmount.CurrencyString);
            }
            catch (AssertFailedException)
            {
                Assert.AreEqual("38.478 ISK", orderInfo.ChargedAmount.CurrencyString);
            }
        }

        [TestMethod]
        public void AppliesFixedDiscountToOrder()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());

            var productKey = Guid.Parse("9e8665c7-d405-42b5-8913-175ca066d5c9");

            new UmbracoHelperCreator(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();
            var discount = Objects.Objects.Get_Discount_fixed_500();
            discount.discountItems = new List<Guid> { productKey };

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;

            Assert.IsTrue(orderSvc.ApplyDiscountToOrderAsync(discount, store.Alias, null, oi).Result);

            Assert.AreEqual(2000, oi.SubTotal.Value);
        }

        [TestMethod]
        public void AppliesPercentageDiscountToOrder()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());

            var productKey = Guid.Parse("9e8665c7-d405-42b5-8913-175ca066d5c9");

            new UmbracoHelperCreator(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var discount = Objects.Objects.Get_Discount_percentage_50();
            discount.discountItems = new List<Guid> { productKey };

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;

            Assert.IsTrue(orderSvc.ApplyDiscountToOrderAsync(discount as Discount, store.Alias, null, oi).Result);

            Assert.AreEqual(1500, oi.SubTotal.Value);
        }

        [TestMethod]
        public void DiscountLinkedToProductAppliesToNewOrderLines()
        {
            var (fac, reg) = Helpers.RegisterAll();

            var productKey = Guid.Parse("9e8665c7-d405-42b5-8913-175ca066d5c9");

            new UmbracoHelperCreator(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var discount = Objects.Objects.Get_GlobalDiscount_percentage_50();
            discount.discountItems = new List<Guid> { productKey };

            var cache = Helpers.CreateGlobalDiscountCacheWithDiscount(store.Alias, discount);
            var productDiscountService = new ProductDiscountService(cache);
            reg.Register<IProductDiscountService>(productDiscountService);

            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;

            Assert.IsNotNull(oi.orderLines.First().Discount);
            Assert.AreEqual(1500, oi.SubTotal.Value);
        }

        [TestMethod]
        public void DeterminesBestDiscount()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());

            var productKey = Guid.Parse("9e8665c7-d405-42b5-8913-175ca066d5c9");

            new UmbracoHelperCreator(reg, fac);
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var discountFixed500 = Objects.Objects.Get_Discount_fixed_500();
            discountFixed500.discountItems = new List<Guid> { productKey };
            var discountPerc50 = Objects.Objects.Get_Discount_percentage_50();
            discountPerc50.discountItems = new List<Guid> { productKey };

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product, 1, store).Result;

            Assert.IsTrue(orderSvc.ApplyDiscountToOrderAsync(
                discountFixed500,
                store.Alias,
                null,
                oi
            ).Result);
            Assert.IsTrue(orderSvc.ApplyDiscountToOrderAsync(
                discountPerc50,
                store.Alias,
                null,
                oi
            ).Result);

            Assert.AreEqual(750, oi.SubTotal.Value);
        }

        [TestMethod]
        public void AppliesFixedDiscountToOrderLine()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());

            var product2Key = Guid.Parse("30762e80-4959-4c24-a29a-13583ff73a06");
            var product3Key = Guid.Parse("9e8665c7-d405-42b5-8913-175ca066d5c9");

            new UmbracoHelperCreator(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product2 = Objects.Objects.Get_Shirt2_Product();
            var product3 = Objects.Objects.Get_Shirt3_Product();

            var discountFixed500 = Objects.Objects.Get_Discount_fixed_500();
            discountFixed500.discountItems = new List<Guid> { product2Key, product3Key };

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product2, 1, store).Result;
            Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
            oi = orderSvc.AddOrderLineAsync(product3, 1, store).Result;

            Assert.IsTrue(orderSvc.ApplyDiscountToOrderLineProductAsync(
                product3,
                discountFixed500,
                store.Alias,
                coupon: null,
                orderInfo: oi
            ).Result);

            Assert.AreEqual(4990, oi.SubTotal.Value);
        }

        [TestMethod]
        public void AppliesFixedDiscountToOrderLine2()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());

            var product2Key = Guid.Parse("30762e80-4959-4c24-a29a-13583ff73a06");
            var product3Key = Guid.Parse("9e8665c7-d405-42b5-8913-175ca066d5c9");

            new UmbracoHelperCreator(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product2 = Objects.Objects.Get_Shirt2_Product();
            var product3 = Objects.Objects.Get_Shirt3_Product();

            var discountFixed500 = Objects.Objects.Get_Discount_fixed_500();
            discountFixed500.discountItems = new List<Guid> { product2Key, product3Key };

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product2, 1, store).Result;
            Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);

            Assert.IsTrue(orderSvc.ApplyDiscountToOrderLineProductAsync(
                product2,
                discountFixed500,
                store.Alias,
                null,
                oi
            ).Result);

            oi = orderSvc.AddOrderLineAsync(product3, 1, store).Result;


            Assert.AreEqual(4990, oi.SubTotal.Value);
        }

        [TestMethod]
        public void ControllerResponseBadRequestWithBadParameters_AddOrderLine()
        {
            var (fac, reg) = Helpers.RegisterAll();
            Helpers.RegisterUmbracoHelper(reg, fac);

            var ctrl = fac.CreateInstance<OrderController>();

            //ctrl.ApplyDiscountToOrder()
        }

        [TestMethod]
        public void GlobalDiscountGetsApplied()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());

            new UmbracoHelperCreator(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product2 = Objects.Objects.Get_Shirt2_Product();

            var discountPerc50 = Objects.Objects.Get_Discount_percentage_50();
            discountPerc50.discountItems.Add(product2.Key);

            var orderSvcMocks = new OrderServiceMocks();
            orderSvcMocks.discountCache.GlobalDiscounts[store.Alias][discountPerc50.Key] = discountPerc50;
            var orderSvc = orderSvcMocks.orderSvc;

            var oi = orderSvc.AddOrderLineAsync(product2, 1, store).Result;
            Assert.AreEqual(1995, oi.SubTotal.Value);
        }

        [TestMethod]
        public void NonCompliantDiscountGetsRemoved()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());

            //var product3Key = Guid.Parse("9e8665c7-d405-42b5-8913-175ca066d5c9");

            new UmbracoHelperCreator(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product3 = Objects.Objects.Get_Shirt3_Product();
            var discount_fixed_1000_Min_2000 = Objects.Objects.Get_Discount_fixed_1000_Min_2000();

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product3, 2, store).Result;

            Assert.IsTrue(orderSvc.ApplyDiscountToOrderAsync(
                discount_fixed_1000_Min_2000,
                store.Alias,
                null,
                oi
            ).Result);

            Assert.AreEqual(1000, oi.SubTotal.Value);

            Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
            oi = orderSvc.AddOrderLineAsync(product3, -1, store).Result;

            Assert.AreEqual(1500, oi.SubTotal.Value);
        }

        [TestMethod]
        public void OrderLineDiscountAmountCalculates()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());

            var umbHelperCreator = new UmbracoHelperCreator(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product2 = Objects.Objects.Get_Shirt2_Product();

            var discountFixed500 = Objects.Objects.Get_Discount_fixed_500();

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product2, 1, store).Result;

            Assert.IsTrue(orderSvc.ApplyDiscountToOrderLineProductAsync(
                product2,
                discountFixed500,
                store.Alias,
                coupon: null,
                orderInfo: oi
            ).Result);

            Assert.AreEqual(500, oi.OrderLines.First().Amount.DiscountAmount.Value);
        }

        [TestMethod]
        public void OrderInfoDiscountAmountCalculates()
        {
            var (fac, reg) = Helpers.RegisterAll();
            reg.Register(Mock.Of<IProductDiscountService>());

            new UmbracoHelperCreator(reg, fac);

            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var discount = Objects.Objects.Get_Discount_fixed_500();
            discount.discountItems = new List<Guid> { product.Key };

            var orderSvc = new OrderServiceMocks().orderSvc;
            var oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;

            Assert.IsTrue(orderSvc.ApplyDiscountToOrderAsync(
                discount, 
                store.Alias,
                coupon: null,
                orderInfo: oi
            ).Result);

            Assert.AreEqual(1000, oi.DiscountAmount.Value);
        }
    }
}
