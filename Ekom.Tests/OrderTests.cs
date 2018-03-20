using Ekom.Helpers;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Tests.MockClasses;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Ekom.Tests
{
    [TestClass]
    public class OrderTests
    {
        [TestMethod]
        public void CreatesOrder()
        {
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvc = new OrderServiceMocks().orderSvc;

            var oi = orderSvc.AddOrderLine(product, 1, store);

            Assert.AreEqual(1, oi.OrderLines.Count);
        }

        [TestMethod]
        public void AddsOrderLine()
        {
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product2 = Objects.Objects.Get_Shirt2_Product();
            var product3 = Objects.Objects.Get_Shirt3_Product();

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLine(product2, 1, store);
            Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
            oi = orderSvc.AddOrderLine(product3, 1, store);

            Assert.AreEqual(2, oi.OrderLines.Count);
        }

        [TestMethod]
        public void UpdatesQuantityPositive()
        {
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLine(product, 2, store);
            Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
            oi = orderSvc.AddOrderLine(product, 2, store);


            Assert.AreEqual(4, oi.TotalQuantity);
        }

        [TestMethod]
        public void UpdatesQuantityNegative()
        {
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLine(product, 3, store);
            Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
            oi = orderSvc.AddOrderLine(product, -1, store);


            Assert.AreEqual(2, oi.TotalQuantity);
        }

        [TestMethod]
        public void SetsQuantity()
        {
            var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
            var product = Objects.Objects.Get_Shirt3_Product();

            var orderSvcMocks = new OrderServiceMocks();
            var orderSvc = orderSvcMocks.orderSvc;
            var oi = orderSvc.AddOrderLine(product, 1, store);
            Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
            oi = orderSvc.AddOrderLine(product, 3, store, OrderAction.Set);


            Assert.AreEqual(3, oi.TotalQuantity);
        }

        const string orderJson = @"{""StoreInfo"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":"""",""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""Discount"":null,""Coupon"":null,""UniqueId"":""1af7be2e-4ec3-45b6-b91c-5de2b63c0502"",""ReferenceId"":67,""OrderNumber"":""IS0067"",""OrderLines"":[{""ProductKey"":""e3402d14-1bfc-4992-b7d6-13a8923b0de3"",""Key"":""64a9d8bb-dfc1-4c8a-a400-65b4c643070c"",""Product"":{""Properties"":{""id"":""1082"",""key"":""e3402d14-1bfc-4992-b7d6-13a8923b0de3"",""parentID"":""1070"",""level"":""5"",""writerID"":""1"",""creatorID"":""0"",""nodeType"":""1058"",""template"":""1084"",""sortOrder"":""2"",""createDate"":""20160907134955000"",""updateDate"":""20171122213919000"",""nodeName"":""Jackets Product 1"",""urlName"":""jackets-product-1"",""writerName"":""gardar@vettvangur.is"",""creatorName"":""Vettvangur@vettvangur.is"",""nodeTypeAlias"":""ekmProduct"",""path"":""-1,1066,1067,1179,1070,1082"",""disable"":""{\""values\"":{\""IS\"":\""0\"",\""EN\"":\""0\"",\""DK\"":\""0\"",\""EU\"":\""0\""},\""dtdGuid\"":\""383bb1cf-eb59-4bff-b5de-48f17f8d3bef\""}"",""price"":""{\""values\"":{\""IS\"":\""25000\"",\""EN\"":\""234\"",\""DK\"":\""345\"",\""EU\"":\""456\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}"",""sku"":""women-sku-jacket-1"",""slug"":""{\""values\"":{\""IS\"":\""jakki-vara-1\"",\""EN\"":\""jackets-product-1\"",\""DK\"":\""jackets-product-1\"",\""EU\"":\""jackets-product-1\""},\""dtdGuid\"":\""c24bad83-32b9-4664-ad88-8bd76c10aea1\""}"",""title"":""{\""values\"":{\""IS\"":\""Jakki vara 1\"",\""EN\"":\""Jackets Product 1\"",\""DK\"":\""Jackets Product 1\"",\""EU\"":\""Jackets Product 1\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}""},""Price"":{""Discount"":null,""Store"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":"""",""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""DiscountAlwaysBeforeVAT"":false,""OriginalValue"":25000.0,""Quantity"":1,""Value"":27500.0},""ImageIds"":[],""VariantGroups"":[]},""Quantity"":2,""Discount"":null,""Coupon"":null,""Amount"":{""Discount"":null,""Store"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":"""",""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""DiscountAlwaysBeforeVAT"":false,""OriginalValue"":25000.0,""Quantity"":2,""Value"":55000.0}}],""ShippingProvider"":null,""PaymentProvider"":null,""TotalQuantity"":2,""CustomerInformation"":{""CustomerIpAddress"":""127.0.0.1"",""Customer"":{""Properties"":{},""Name"":"""",""Email"":"""",""Address"":"""",""City"":"""",""Country"":"""",""ZipCode"":"""",""Phone"":"""",""UserId"":0,""UserName"":null},""Shipping"":{""Properties"":{},""Name"":"""",""Address"":"""",""City"":"""",""Country"":"""",""ZipCode"":""""}},""OrderLineTotal"":{""Value"":50000.0,""CurrencyString"":""50.000 ISK""},""SubTotal"":{""Value"":50000.0,""CurrencyString"":""50.000 ISK""},""Vat"":{""Value"":5000.0,""CurrencyString"":""5.000 ISK""},""GrandTotal"":{""Value"":55000.0,""CurrencyString"":""55.000 ISK""},""ChargedAmount"":{""Value"":55000.0,""CurrencyString"":""55.000 ISK""},""CreateDate"":""2018-03-20T15:39:36.8320656+00:00"",""UpdateDate"":""0001-01-01T00:00:00"",""PaidDate"":""0001-01-01T00:00:00"",""OrderStatus"":3,""HangfireJobs"":[]}";

        [TestMethod]
        public void CanParseOrderJson()
        {
            var container = Helpers.InitMockContainer();
            container.Setup(c => c.GetInstance<Configuration>()).Returns(new Configuration());

            var orderData = new OrderData
            {
                OrderInfo = orderJson,
            };

            var orderInfo = new OrderInfo(orderData);

            Assert.AreEqual(1, orderInfo.OrderLines.Count);
            Assert.AreEqual("127.0.0.1", orderInfo.CustomerInformation.CustomerIpAddress);
            Assert.AreEqual("55.000 ISK", orderInfo.ChargedAmount.CurrencyString);
        }
    }
}
