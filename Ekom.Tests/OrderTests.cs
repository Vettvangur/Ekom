using Ekom.Interfaces;
using Ekom.Models;
using Ekom.Models.Data;
using Ekom.Tests.MockClasses;
using Ekom.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using Umbraco.Core;
using Umbraco.Core.Composing;

namespace Ekom.Tests
{
    [TestClass]
    public class OrderTests
    {
        [TestCleanup]
        public void TearDown()
        {
            Current.Reset();
        }

        [TestMethod]
        public void CreatesOrderInfo()
        {
            var od = new OrderData
            {
                UniqueId = new Guid("11554DAC-F6DE-4A57-8736-967242C653B8"),

                ReferenceId = 206,

                OrderInfo = "{\"$type\":\"Ekom.Models.OrderInfo, Ekom\",\"StoreInfo\":{\"$type\":\"Ekom.Models.OrderedObjects.StoreInfo, Ekom\",\"Key\":\"8d7f1c8f-8bf9-4276-b230-4ad6957d3d64\",\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"Currencies\":[{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"}],\"Culture\":\"is-IS\",\"Alias\":\"Distica\",\"VatIncludedInPrice\":false,\"Vat\":0},\"Discount\":null,\"Coupon\":null,\"UniqueId\":\"11554dac-f6de-4a57-8736-967242c653b8\",\"ReferenceId\":206,\"OrderNumber\":\"0206\",\"OrderLines\":[{\"$type\":\"Ekom.Models.OrderLine, Ekom\",\"ProductKey\":\"79efde1b-39b6-4636-82e5-d4f4b4b9db3b\",\"Key\":\"c650de9f-1eee-49ed-8012-54e49a0a2b1d\",\"Product\":{\"$type\":\"Ekom.Models.OrderedObjects.OrderedProduct, Ekom\",\"Properties\":{\"$type\":\"System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\",\"__NodeId\":\"36868\",\"__IndexType\":\"content\",\"__NodeTypeAlias\":\"ekmProduct\",\"icon\":\"icon-loupe\",\"__Published\":\"y\",\"id\":\"36868\",\"__Key\":\"79efde1b-39b6-4636-82e5-d4f4b4b9db3b\",\"parentID\":\"36810\",\"level\":\"5\",\"creatorID\":\"-1\",\"sortOrder\":\"19\",\"createDate\":\"637593267220430000\",\"updateDate\":\"637607932781630000\",\"nodeName\":\"Leucofeligen FeLV/RCP stungulyf, dreifa\",\"urlName\":\"leucofeligen-felv-rcp-stungulyf-dreifa\",\"path\":\"-1,2915,2916,12318,36810,36868\",\"nodeType\":\"2898\",\"creatorName\":\"Vettvangur\",\"writerName\":\"Vettvangur\",\"writerID\":\"-1\",\"templateID\":\"3474\",\"__VariesByCulture\":\"n\",\"title\":\"{\\\"values\\\":{\\\"Distica\\\":\\\"Leucofeligen FeLV/RCP stungulyf, dreifa\\\"},\\\"dtdGuid\\\":\\\"b3b2ca16-f5c9-4160-805a-d3abb2e2f9d9\\\"}\",\"slug\":\"{\\\"values\\\":{\\\"Distica\\\":\\\"leucofeligen-felv-rcp-stungulyf-dreifa-082512\\\"},\\\"dtdGuid\\\":\\\"b3b2ca16-f5c9-4160-805a-d3abb2e2f9d9\\\"}\",\"sku\":\"082512\",\"price\":\"{\\\"values\\\":{\\\"Distica\\\":[{\\\"Currency\\\":\\\"is-IS\\\",\\\"Price\\\":12912.0}]},\\\"dtdGuid\\\":\\\"f1d3d974-4611-4858-a009-98c944609677\\\"}\",\"priceFrom\":\"12912\",\"hasVariants\":\"1\",\"agent\":\"Vistor hf.\",\"contact\":\"Anna Ólöf Haraldsdóttir\",\"atc\":\"QI06AH07\",\"smPCLink\":\"https://www.ema.europa.eu/documents/product-information/leucofeligen-felv/rcp-epar-product-information_is.pdf\",\"pharmaceutical\":\"1\",\"prescriptionDrug\":\"1\",\"pharmaceuticalInfoLink\":\"https://www.ema.europa.eu/documents/product-information/leucofeligen-felv/rcp-epar-product-information_is.pdf\",\"marketingAuthorizationHolder\":\"Virbac S.A\",\"contactEmail\":\"annah@vistor.is\",\"itemNo\":\"082512;\",\"searchPath\":\"|-1|2915|2916|12318|36810|36868|\",\"__Path\":\"-1,2915,2916,12318,36810,36868\",\"__Icon\":\"icon-loupe\"},\"Id\":36868,\"Key\":\"79efde1b-39b6-4636-82e5-d4f4b4b9db3b\",\"SKU\":\"082512\",\"ProductDiscount\":null,\"Title\":\"Leucofeligen FeLV/RCP stungulyf, dreifa\",\"Price\":{\"$type\":\"Ekom.Models.Price, Ekom\",\"Discount\":null,\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"DiscountAlwaysBeforeVAT\":false,\"OriginalValue\":12912,\"Quantity\":1,\"BeforeDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"AfterDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"WithoutVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"Value\":12912,\"WithVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"}},\"Prices\":[{\"$type\":\"Ekom.Models.Price, Ekom\",\"Discount\":null,\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"DiscountAlwaysBeforeVAT\":false,\"OriginalValue\":12912,\"Quantity\":1,\"BeforeDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"AfterDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"WithoutVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"Value\":12912,\"WithVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"}}],\"Vat\":0,\"VariantGroups\":[{\"$type\":\"Ekom.Models.OrderedObjects.OrderedVariantGroup, Ekom\",\"Properties\":{\"$type\":\"System.Collections.ObjectModel.ReadOnlyDictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\",\"__NodeId\":\"36869\",\"__IndexType\":\"content\",\"__NodeTypeAlias\":\"ekmProductVariantGroup\",\"icon\":\"icon-folder\",\"__Published\":\"y\",\"id\":\"36869\",\"__Key\":\"ad738578-c6ac-42d4-af18-a92c951a9cac\",\"parentID\":\"36868\",\"level\":\"6\",\"creatorID\":\"-1\",\"sortOrder\":\"0\",\"createDate\":\"637593267224630000\",\"updateDate\":\"637593267224630000\",\"nodeName\":\"Variants\",\"urlName\":\"variants\",\"path\":\"-1,2915,2916,12318,36810,36868,36869\",\"nodeType\":\"2897\",\"creatorName\":\"Vettvangur\",\"writerName\":\"Vettvangur\",\"writerID\":\"-1\",\"templateID\":\"0\",\"__VariesByCulture\":\"n\",\"title\":\"{\\\"values\\\":{\\\"Distica\\\":\\\"Variants\\\"},\\\"dtdGuid\\\":\\\"b3b2ca16-f5c9-4160-805a-d3abb2e2f9d9\\\"}\",\"sku\":\"082512\",\"price\":\"{\\\"values\\\":{\\\"Distica\\\":[{\\\"Currency\\\":\\\"is-IS\\\",\\\"Price\\\":13193.0}]},\\\"dtdGuid\\\":\\\"f1d3d974-4611-4858-a009-98c944609677\\\"}\",\"itemNumber\":\"082512\",\"itemDescription\":\"10 stk\",\"unitDescription\":\"10 stk\",\"variantPrice\":\"13193\",\"variantOrder\":\"0\",\"specialOrder\":\"0\",\"vat\":\"24\",\"searchPath\":\"|-1|2915|2916|12318|36810|36868|36869|\",\"__Path\":\"-1,2915,2916,12318,36810,36868,36869\",\"__Icon\":\"icon-folder\"},\"Id\":36869,\"Key\":\"ad738578-c6ac-42d4-af18-a92c951a9cac\",\"Title\":\"Variants\",\"Variants\":[{\"$type\":\"Ekom.Models.OrderedObjects.OrderedVariant, Ekom\",\"Properties\":{\"$type\":\"System.Collections.ObjectModel.ReadOnlyDictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\",\"__NodeId\":\"36870\",\"__IndexType\":\"content\",\"__NodeTypeAlias\":\"ekmProductVariant\",\"icon\":\"icon-layers-alt\",\"__Published\":\"y\",\"id\":\"36870\",\"__Key\":\"0d38f4a2-13d4-44c0-ab3f-d2ae1df759d1\",\"parentID\":\"36869\",\"level\":\"7\",\"creatorID\":\"-1\",\"sortOrder\":\"0\",\"createDate\":\"637593267236100000\",\"updateDate\":\"637593267236100000\",\"nodeName\":\"10 stk\",\"urlName\":\"10-stk\",\"path\":\"-1,2915,2916,12318,36810,36868,36869,36870\",\"nodeType\":\"2896\",\"creatorName\":\"Vettvangur\",\"writerName\":\"Vettvangur\",\"writerID\":\"-1\",\"templateID\":\"0\",\"__VariesByCulture\":\"n\",\"title\":\"{\\\"values\\\":{\\\"Distica\\\":\\\"10 stk\\\"},\\\"dtdGuid\\\":\\\"b3b2ca16-f5c9-4160-805a-d3abb2e2f9d9\\\"}\",\"sku\":\"082512\",\"price\":\"{\\\"values\\\":{\\\"Distica\\\":[{\\\"Currency\\\":\\\"is-IS\\\",\\\"Price\\\":13193.0}]},\\\"dtdGuid\\\":\\\"f1d3d974-4611-4858-a009-98c944609677\\\"}\",\"itemNumber\":\"082512\",\"itemDescription\":\"10 stk\",\"unitDescription\":\"10 stk\",\"variantPrice\":\"13193\",\"variantOrder\":\"0\",\"specialOrder\":\"0\",\"vat\":\"24\",\"searchPath\":\"|-1|2915|2916|12318|36810|36868|36869|36870|\",\"__Path\":\"-1,2915,2916,12318,36810,36868,36869,36870\",\"__Icon\":\"icon-layers-alt\"},\"Price\":{\"$type\":\"Ekom.Models.Price, Ekom\",\"Discount\":null,\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"DiscountAlwaysBeforeVAT\":false,\"OriginalValue\":12912,\"Quantity\":1,\"BeforeDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"AfterDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"WithoutVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"Value\":322800,\"WithVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":322800,\"CurrencyString\":\"322.800 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":309888,\"CurrencyString\":\"309.888 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"}},\"Prices\":[{\"$type\":\"Ekom.Models.Price, Ekom\",\"Discount\":null,\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"DiscountAlwaysBeforeVAT\":false,\"OriginalValue\":12912,\"Quantity\":1,\"BeforeDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"AfterDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"WithoutVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"Value\":322800,\"WithVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":322800,\"CurrencyString\":\"322.800 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":309888,\"CurrencyString\":\"309.888 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"}}],\"Vat\":0}]}]},\"Quantity\":1,\"OrderLineInfo\":{\"$type\":\"Ekom.Models.OrderLineInfo, Ekom\",\"Properties\":{\"$type\":\"System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\"}},\"Discount\":null,\"Coupon\":null,\"OrderlineLink\":\"00000000-0000-0000-0000-000000000000\",\"Amount\":{\"$type\":\"Ekom.Models.Price, Ekom\",\"Discount\":null,\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"DiscountAlwaysBeforeVAT\":false,\"OriginalValue\":12912,\"Quantity\":1,\"BeforeDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"AfterDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"WithoutVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"Value\":16010.88,\"WithVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":16010.88,\"CurrencyString\":\"16.011 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":3098.88,\"CurrencyString\":\"3.099 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"}},\"Vat\":0.24}],\"ShippingProvider\":null,\"PaymentProvider\":null,\"TotalQuantity\":1,\"CustomerInformation\":{\"$type\":\"Ekom.Models.CustomerInfo, Ekom\",\"CustomerIpAddress\":\"81.15.104.62\",\"Customer\":{\"$type\":\"Ekom.Models.Customer, Ekom\",\"Properties\":{\"$type\":\"System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\",\"customerName\":\"1743\",\"customerEmail\":\"gudmundur@veritas.is\",\"customerReceiptUrl\":\"/kvittun/?orderNumber=\"},\"Name\":\"1743\",\"FirstName\":\"\",\"LastName\":\"\",\"Email\":\"gudmundur@veritas.is\",\"Address\":\"\",\"City\":\"\",\"Apartment\":\"\",\"Country\":\"\",\"ZipCode\":\"\",\"Phone\":\"\",\"UserId\":1283,\"UserName\":\"1002813929\"},\"Shipping\":{\"$type\":\"Ekom.Models.CustomerShippingInfo, Ekom\",\"Properties\":{\"$type\":\"System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\",\"shippingProvider\":\"00000000-0000-0000-0000-000000000000\",\"shippingName\":\"1\",\"shippingDeliveryMessage\":\"test\"},\"Name\":\"1\",\"FirstName\":\"\",\"LastName\":\"\",\"Address\":\"\",\"City\":\"\",\"Apartment\":\"\",\"Country\":\"\",\"ZipCode\":\"\"}},\"OrderLineTotal\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":16011,\"CurrencyString\":\"16.011 kr\"},\"SubTotal\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":12912,\"CurrencyString\":\"12.912 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"},\"ChargedVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":3099,\"CurrencyString\":\"3.099 kr\"},\"GrandTotal\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":16011,\"CurrencyString\":\"16.011 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"},\"ChargedAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":16011,\"CurrencyString\":\"16.011 kr\"},\"CreateDate\":\"2021-07-30T00:55:31.4375576+00:00\",\"UpdateDate\":\"2021-07-30T00:55:31.4850656+00:00\",\"PaidDate\":null,\"OrderStatus\":3,\"HangfireJobs\":[]}",

                OrderNumber = "0206",

                OrderStatusCol = 3,

                CustomerEmail = "gudmundur@veritas.is",

                CustomerName = "1743",

                CustomerId = 1283,

                CustomerUsername = "1002813929",

                TotalAmount = 16011,

                Currency = "kr",

                StoreAlias = "Distica",

                //CreateDate
            };
            var oi = new OrderInfo(od);
        }
        //[TestMethod]
        //public void CreatesOrder()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    reg.Register(Mock.Of<IProductDiscountService>());
        //    Helpers.RegisterUmbracoHelper(reg, fac);

        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
        //    var product = Objects.Objects.Get_Shirt3_Product();

        //    var orderSvc = new OrderServiceMocks().orderSvc;

        //    var oi = orderSvc.AddOrderLineAsync(product, 1, store).Result;

        //    Assert.AreEqual(1, oi.OrderLines.Count);
        //}

        //[TestMethod]
        //public void AddsOrderLine()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    reg.Register(Mock.Of<IProductDiscountService>());
        //    Helpers.RegisterUmbracoHelper(reg, fac);

        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
        //    var product2 = Objects.Objects.Get_Shirt2_Product();
        //    var product3 = Objects.Objects.Get_Shirt3_Product();

        //    var orderSvcMocks = new OrderServiceMocks();
        //    var orderSvc = orderSvcMocks.orderSvc;
        //    var oi = orderSvc.AddOrderLineAsync(product2, 1, store).Result;
        //    Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
        //    oi = orderSvc.AddOrderLineAsync(product3, 1, store).Result;

        //    Assert.AreEqual(2, oi.OrderLines.Count);
        //}

        //[TestMethod]
        //public void UpdatesQuantityPositive()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    reg.Register(Mock.Of<IProductDiscountService>());
        //    Helpers.RegisterUmbracoHelper(reg, fac);

        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
        //    var product = Objects.Objects.Get_Shirt3_Product();

        //    var orderSvcMocks = new OrderServiceMocks();
        //    var orderSvc = orderSvcMocks.orderSvc;
        //    var oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;
        //    Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
        //    oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;

        //    Assert.AreEqual(4, oi.TotalQuantity);
        //}

        //[TestMethod]
        //public void UpdatesQuantityNegative()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    reg.Register(Mock.Of<IProductDiscountService>());
        //    Helpers.RegisterUmbracoHelper(reg, fac);

        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
        //    var product = Objects.Objects.Get_Shirt3_Product();

        //    var orderSvcMocks = new OrderServiceMocks();
        //    var orderSvc = orderSvcMocks.orderSvc;
        //    var oi = orderSvc.AddOrderLineAsync(product, 3, store).Result;
        //    Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
        //    oi = orderSvc.AddOrderLineAsync(product, -1, store).Result;

        //    Assert.AreEqual(2, oi.TotalQuantity);
        //}

        //[TestMethod]
        //public void SetsQuantity()
        //{
        //    var (fac, reg) = Helpers.RegisterAll();
        //    reg.Register(Mock.Of<IProductDiscountService>());
        //    Helpers.RegisterUmbracoHelper(reg, fac);

        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
        //    var product = Objects.Objects.Get_Shirt3_Product();

        //    var orderSvcMocks = new OrderServiceMocks();
        //    var orderSvc = orderSvcMocks.orderSvc;
        //    var oi = orderSvc.AddOrderLineAsync(product, 1, store).Result;
        //    Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
        //    oi = orderSvc.AddOrderLineAsync(product, 3, store, OrderAction.Set).Result;

        //    Assert.AreEqual(3, oi.TotalQuantity);
        //}

        //const string orderJson = @"{""StoreInfo"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":{""CurrencyFormat"":""C"",""ISOCurrencySymbol"":""USD"",""CurrencyValue"":""en-US"",""CurrencySymbol"":""kr""},""Currencies"":[{""CurrencyFormat"":""C"",""ISOCurrencySymbol"":""USD"",""CurrencyValue"":""en-US"",""CurrencySymbol"":""kr""}],""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""Discount"":null,""Coupon"":null,""UniqueId"":""1af7be2e-4ec3-45b6-b91c-5de2b63c0502"",""ReferenceId"":67,""OrderNumber"":""IS0067"",""OrderLines"":[{""ProductKey"":""e3402d14-1bfc-4992-b7d6-13a8923b0de3"",""Key"":""64a9d8bb-dfc1-4c8a-a400-65b4c643070c"",""Product"":{""Properties"":{""id"":""1082"",""key"":""e3402d14-1bfc-4992-b7d6-13a8923b0de3"",""parentID"":""1070"",""level"":""5"",""writerID"":""1"",""creatorID"":""0"",""nodeType"":""1058"",""template"":""1084"",""sortOrder"":""2"",""createDate"":""20160907134955000"",""updateDate"":""20171122213919000"",""nodeName"":""Jackets Product 1"",""urlName"":""jackets-product-1"",""writerName"":""gardar@vettvangur.is"",""creatorName"":""Vettvangur@vettvangur.is"",""nodeTypeAlias"":""ekmProduct"",""path"":""-1,1066,1067,1179,1070,1082"",""disable"":""{\""values\"":{\""IS\"":\""0\"",\""EN\"":\""0\"",\""DK\"":\""0\"",\""EU\"":\""0\""},\""dtdGuid\"":\""383bb1cf-eb59-4bff-b5de-48f17f8d3bef\""}"",""price"":""{\""values\"":{\""IS\"":\""25000\"",\""EN\"":\""234\"",\""DK\"":\""345\"",\""EU\"":\""456\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}"",""sku"":""women-sku-jacket-1"",""slug"":""{\""values\"":{\""IS\"":\""jakki-vara-1\"",\""EN\"":\""jackets-product-1\"",\""DK\"":\""jackets-product-1\"",\""EU\"":\""jackets-product-1\""},\""dtdGuid\"":\""c24bad83-32b9-4664-ad88-8bd76c10aea1\""}"",""title"":""{\""values\"":{\""IS\"":\""Jakki vara 1\"",\""EN\"":\""Jackets Product 1\"",\""DK\"":\""Jackets Product 1\"",\""EU\"":\""Jackets Product 1\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}""},""Price"":{""Discount"":null,""Store"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":[{""CurrencyFormat"":""C"",""CurrencyValue"":""en-US""}],""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""DiscountAlwaysBeforeVAT"":false,""OriginalValue"":25000.0,""Quantity"":1,""Value"":27500.0},""ImageIds"":[],""VariantGroups"":[]},""Quantity"":2,""Discount"":null,""Coupon"":null,""Amount"":{""Discount"":null,""Store"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":[{""CurrencyFormat"":""C"",""Currency"":""en-US"",""CurrencyValue"":""en-US""}],""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""DiscountAlwaysBeforeVAT"":false,""OriginalValue"":25000.0,""Quantity"":2,""Value"":55000.0}}],""ShippingProvider"":null,""PaymentProvider"":null,""TotalQuantity"":2,""CustomerInformation"":{""CustomerIpAddress"":""127.0.0.1"",""Customer"":{""Properties"":{},""Name"":"""",""Email"":"""",""Address"":"""",""City"":"""",""Country"":"""",""ZipCode"":"""",""Phone"":"""",""UserId"":0,""UserName"":null},""Shipping"":{""Properties"":{},""Name"":"""",""Address"":"""",""City"":"""",""Country"":"""",""ZipCode"":""""}},""OrderLineTotal"":{""Value"":50000.0,""CurrencyString"":""50.000 ISK""},""SubTotal"":{""Value"":50000.0,""CurrencyString"":""50.000 ISK""},""Vat"":{""Value"":5000.0,""CurrencyString"":""5.000 ISK""},""GrandTotal"":{""Value"":55000.0,""CurrencyString"":""55.000 ISK""},""ChargedAmount"":{""Value"":55000.0,""CurrencyString"":""55.000 ISK""},""CreateDate"":""2018-03-20T15:39:36.8320656+00:00"",""UpdateDate"":""0001-01-01T00:00:00"",""PaidDate"":""0001-01-01T00:00:00"",""OrderStatus"":3,""HangfireJobs"":[]}";

        //[TestMethod]
        //public void CanParseOrderJson_WithCurrencyFormat()
        //{
        //    Helpers.RegisterAll();

        //    var orderData = new OrderData
        //    {
        //        OrderInfo = orderJson,
        //    };

        //    var orderInfo = new OrderInfo(orderData);

        //    Assert.AreEqual(1, orderInfo.OrderLines.Count);
        //    Assert.AreEqual("127.0.0.1", orderInfo.CustomerInformation.CustomerIpAddress);

        //    Assert.AreEqual("$55,000.00", orderInfo.ChargedAmount.CurrencyString);
        //}
    }
}
