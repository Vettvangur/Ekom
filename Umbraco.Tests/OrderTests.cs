//using Ekom.Interfaces;
//using Ekom.Models;
//using Ekom.Models.Data;
//using Ekom.Tests.MockClasses;
//using Ekom.Utilities;
//using Microsoft.VisualStudio.TestTools.UnitTesting;
//using Moq;
//using System;
//using Umbraco.Core;
//using Umbraco.Core.Composing;

//namespace Ekom.Tests
//{
//    [TestClass]
//    public class OrderTests
//    {
//        [TestCleanup]
//        public void TearDown()
//        {
//            Current.Reset();
//        }

//        [TestMethod]
//        public void CreatesOrderInfo()
//        {
//            Helpers.RegisterAll();

//            var od = new OrderData
//            {
//                UniqueId = new Guid("11554DAC-F6DE-4A57-8736-967242C653B8"),

//                ReferenceId = 206,

//                OrderInfo = "{\"$type\":\"Ekom.Models.OrderInfo, Ekom\",\"StoreInfo\":{\"$type\":\"Ekom.Models.OrderedObjects.StoreInfo, Ekom\",\"Key\":\"558f05fd-7f52-489f-b62e-0df2120ef69b\",\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"Currencies\":[{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"}],\"Culture\":\"is-IS\",\"Alias\":\"Distica\",\"VatIncludedInPrice\":false,\"Vat\":0},\"Discount\":null,\"Coupon\":null,\"UniqueId\":\"ab19d7a5-4738-41b2-959e-e4674e4baf47\",\"ReferenceId\":123,\"OrderNumber\":\"0123\",\"OrderLines\":[{\"$type\":\"Ekom.Models.OrderLine, Ekom\",\"ProductKey\":\"3db8b1b4-1a2a-4f84-b98e-915a1857d886\",\"Key\":\"0ed73085-4f42-4c9d-b459-2b37f8d989e2\",\"Product\":{\"$type\":\"Ekom.Models.OrderedObjects.OrderedProduct, Ekom\",\"Properties\":{\"$type\":\"System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\",\"__NodeId\":\"4032\",\"__IndexType\":\"content\",\"__NodeTypeAlias\":\"ekmProduct\",\"icon\":\"icon-loupe\",\"__Published\":\"y\",\"id\":\"4032\",\"__Key\":\"3db8b1b4-1a2a-4f84-b98e-915a1857d886\",\"parentID\":\"3976\",\"level\":\"5\",\"creatorID\":\"-1\",\"sortOrder\":\"11\",\"createDate\":\"637599519502570000\",\"updateDate\":\"637599519502570000\",\"nodeName\":\"KRUUSE nlar 2,1 x 38mm ll 14g  bovivet\",\"urlName\":\"kruuse-nlar-2-1-x-38mm-ll-14g-bovivet\",\"path\":\"-1,2915,2916,3972,3976,4032\",\"nodeType\":\"2898\",\"creatorName\":\"Vettvangur\",\"writerName\":\"Vettvangur\",\"writerID\":\"-1\",\"templateID\":\"3668\",\"__VariesByCulture\":\"n\",\"title\":\"{\\\"values\\\":{\\\"Distica\\\":\\\"KRUUSE nálar 2,1 x 38mm ll 14g - bovivet\\\"},\\\"dtdGuid\\\":\\\"b3b2ca16-f5c9-4160-805a-d3abb2e2f9d9\\\"}\",\"slug\":\"{\\\"values\\\":{\\\"Distica\\\":\\\"kruuse-nalar-2-1-x-38mm-ll-14g-bovivet\\\"},\\\"dtdGuid\\\":\\\"b3b2ca16-f5c9-4160-805a-d3abb2e2f9d9\\\"}\",\"sku\":\"112460K\",\"price\":\"{\\\"values\\\":{\\\"Distica\\\":[{\\\"Currency\\\":\\\"is-IS\\\",\\\"Price\\\":4554.0}]},\\\"dtdGuid\\\":\\\"f1d3d974-4611-4858-a009-98c944609677\\\"}\",\"priceFrom\":\"4554\",\"hasVariants\":\"1\",\"agent\":\"Vistor hf.\",\"contact\":\"Anna Ólöf Haraldsdóttir\",\"pharmaceutical\":\"0\",\"prescriptionDrug\":\"0\",\"contactEmail\":\"annah@vistor.is\",\"contact2\":\"Margrét Dögg Halldórsdóttir\",\"producerName\":\"KRUUSE\",\"itemNo\":\"112460K;\",\"contactEmail2\":\"margret@vistor.is\",\"searchPath\":\"|-1|2915|2916|3972|3976|4032|\",\"__Path\":\"-1,2915,2916,3972,3976,4032\",\"__Icon\":\"icon-loupe\"},\"Id\":4032,\"Key\":\"3db8b1b4-1a2a-4f84-b98e-915a1857d886\",\"SKU\":\"112460K\",\"ProductDiscount\":null,\"Title\":\"KRUUSE nálar 2,1 x 38mm ll 14g - bovivet\",\"Price\":{\"$type\":\"Ekom.Models.Price, Ekom\",\"Discount\":null,\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"DiscountAlwaysBeforeVAT\":false,\"OriginalValue\":4554,\"Quantity\":1,\"BeforeDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"AfterDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"WithoutVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"Value\":4554,\"WithVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"}},\"Prices\":[{\"$type\":\"Ekom.Models.Price, Ekom\",\"Discount\":null,\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"DiscountAlwaysBeforeVAT\":false,\"OriginalValue\":4554,\"Quantity\":1,\"BeforeDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"AfterDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"WithoutVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"Value\":4554,\"WithVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"}}],\"Vat\":0,\"VariantGroups\":[{\"$type\":\"Ekom.Models.OrderedObjects.OrderedVariantGroup, Ekom\",\"Properties\":{\"$type\":\"System.Collections.ObjectModel.ReadOnlyDictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\",\"__NodeId\":\"4034\",\"__IndexType\":\"content\",\"__NodeTypeAlias\":\"ekmProductVariantGroup\",\"icon\":\"icon-folder\",\"__Published\":\"y\",\"id\":\"4034\",\"__Key\":\"16d29a4f-7291-4fbe-bb2f-23b0fe8cc516\",\"parentID\":\"4032\",\"level\":\"6\",\"creatorID\":\"-1\",\"sortOrder\":\"0\",\"createDate\":\"637599519508200000\",\"updateDate\":\"637599519508200000\",\"nodeName\":\"Variants\",\"urlName\":\"variants\",\"path\":\"-1,2915,2916,3972,3976,4032,4034\",\"nodeType\":\"2897\",\"creatorName\":\"Vettvangur\",\"writerName\":\"Vettvangur\",\"writerID\":\"-1\",\"templateID\":\"0\",\"__VariesByCulture\":\"n\",\"title\":\"{\\\"values\\\":{\\\"Distica\\\":\\\"Variants\\\"},\\\"dtdGuid\\\":\\\"b3b2ca16-f5c9-4160-805a-d3abb2e2f9d9\\\"}\",\"sku\":\"112460K\",\"images\":\"4035\",\"price\":\"{\\\"values\\\":{\\\"Distica\\\":[{\\\"Currency\\\":\\\"is-IS\\\",\\\"Price\\\":4554.0}]},\\\"dtdGuid\\\":\\\"f1d3d974-4611-4858-a009-98c944609677\\\"}\",\"itemNumber\":\"112460K\",\"itemDescription\":\"100 STK\",\"unitDescription\":\"100 STK\",\"variantPrice\":\"4554\",\"variantOrder\":\"0\",\"specialOrder\":\"0\",\"vat\":\"24\",\"searchPath\":\"|-1|2915|2916|3972|3976|4032|4034|\",\"__Path\":\"-1,2915,2916,3972,3976,4032,4034\",\"__Icon\":\"icon-folder\"},\"Id\":4034,\"Key\":\"16d29a4f-7291-4fbe-bb2f-23b0fe8cc516\",\"Title\":\"Variants\",\"Variants\":[{\"$type\":\"Ekom.Models.OrderedObjects.OrderedVariant, Ekom\",\"Properties\":{\"$type\":\"System.Collections.ObjectModel.ReadOnlyDictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\",\"__NodeId\":\"4036\",\"__IndexType\":\"content\",\"__NodeTypeAlias\":\"ekmProductVariant\",\"icon\":\"icon-layers-alt\",\"__Published\":\"y\",\"id\":\"4036\",\"__Key\":\"94367dcc-956c-45be-8a09-c966db915d70\",\"parentID\":\"4034\",\"level\":\"7\",\"creatorID\":\"-1\",\"sortOrder\":\"0\",\"createDate\":\"637599519518200000\",\"updateDate\":\"637599519518200000\",\"nodeName\":\"100 STK\",\"urlName\":\"100-stk\",\"path\":\"-1,2915,2916,3972,3976,4032,4034,4036\",\"nodeType\":\"2896\",\"creatorName\":\"Vettvangur\",\"writerName\":\"Vettvangur\",\"writerID\":\"-1\",\"templateID\":\"0\",\"__VariesByCulture\":\"n\",\"title\":\"{\\\"values\\\":{\\\"Distica\\\":\\\"100 STK\\\"},\\\"dtdGuid\\\":\\\"b3b2ca16-f5c9-4160-805a-d3abb2e2f9d9\\\"}\",\"sku\":\"112460K\",\"images\":\"4035\",\"price\":\"{\\\"values\\\":{\\\"Distica\\\":[{\\\"Currency\\\":\\\"is-IS\\\",\\\"Price\\\":4554.0}]},\\\"dtdGuid\\\":\\\"f1d3d974-4611-4858-a009-98c944609677\\\"}\",\"itemNumber\":\"112460K\",\"itemDescription\":\"100 STK\",\"unitDescription\":\"100 STK\",\"variantPrice\":\"4554\",\"variantOrder\":\"0\",\"specialOrder\":\"0\",\"vat\":\"24\",\"searchPath\":\"|-1|2915|2916|3972|3976|4032|4034|4036|\",\"__Path\":\"-1,2915,2916,3972,3976,4032,4034,4036\",\"__Icon\":\"icon-layers-alt\"},\"Price\":{\"$type\":\"Ekom.Models.Price, Ekom\",\"Discount\":null,\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"DiscountAlwaysBeforeVAT\":false,\"OriginalValue\":4554,\"Quantity\":1,\"BeforeDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"AfterDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"WithoutVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"Value\":5646.96,\"WithVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":5646.96,\"CurrencyString\":\"5.647 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":1092.96,\"CurrencyString\":\"1.093 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"}},\"Prices\":[{\"$type\":\"Ekom.Models.Price, Ekom\",\"Discount\":null,\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"DiscountAlwaysBeforeVAT\":false,\"OriginalValue\":4554,\"Quantity\":1,\"BeforeDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"AfterDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"WithoutVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"Value\":5646.96,\"WithVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":5646.96,\"CurrencyString\":\"5.647 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":1092.96,\"CurrencyString\":\"1.093 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"}}],\"ProductVat\":0,\"Vat\":0.24}]}]},\"Quantity\":1,\"OrderLineInfo\":{\"$type\":\"Ekom.Models.OrderLineInfo, Ekom\",\"Properties\":{\"$type\":\"System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\"}},\"Discount\":null,\"Coupon\":null,\"OrderlineLink\":\"00000000-0000-0000-0000-000000000000\",\"Amount\":{\"$type\":\"Ekom.Models.Price, Ekom\",\"Discount\":null,\"Currency\":{\"$type\":\"Ekom.Models.CurrencyModel, Ekom\",\"CurrencyFormat\":\"C\",\"CurrencyValue\":\"is-IS\",\"CurrencySymbol\":\"ISK\",\"ISOCurrencySymbol\":\"ISK\"},\"DiscountAlwaysBeforeVAT\":false,\"OriginalValue\":4554,\"Quantity\":1,\"BeforeDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"AfterDiscount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"WithoutVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"Value\":5646.96,\"WithVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":5646.96,\"CurrencyString\":\"5.647 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":1092.96,\"CurrencyString\":\"1.093 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"}},\"Vat\":0.24}],\"ShippingProvider\":null,\"PaymentProvider\":null,\"TotalQuantity\":1,\"CustomerInformation\":{\"$type\":\"Ekom.Models.CustomerInfo, Ekom\",\"CustomerIpAddress\":\"194.144.232.94\",\"Customer\":{\"$type\":\"Ekom.Models.Customer, Ekom\",\"Properties\":{\"$type\":\"System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\"},\"Name\":\" \",\"FirstName\":\"\",\"LastName\":\"\",\"Email\":\"\",\"Address\":\"\",\"City\":\"\",\"Apartment\":\"\",\"Country\":\"\",\"ZipCode\":\"\",\"Phone\":\"\",\"UserId\":0,\"UserName\":null},\"Shipping\":{\"$type\":\"Ekom.Models.CustomerShippingInfo, Ekom\",\"Properties\":{\"$type\":\"System.Collections.Generic.Dictionary`2[[System.String, mscorlib],[System.String, mscorlib]], mscorlib\"},\"Name\":\"\",\"FirstName\":\"\",\"LastName\":\"\",\"Address\":\"\",\"City\":\"\",\"Apartment\":\"\",\"Country\":\"\",\"ZipCode\":\"\"}},\"OrderLineTotal\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":5647,\"CurrencyString\":\"5.647 kr\"},\"SubTotal\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":4554,\"CurrencyString\":\"4.554 kr\"},\"Vat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"},\"ChargedVat\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":1093,\"CurrencyString\":\"1.093 kr\"},\"GrandTotal\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":5647,\"CurrencyString\":\"5.647 kr\"},\"DiscountAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":0,\"CurrencyString\":\"0 kr\"},\"ChargedAmount\":{\"$type\":\"Ekom.Models.CalculatedPrice, Ekom\",\"Value\":5647,\"CurrencyString\":\"5.647 kr\"},\"CreateDate\":\"2021-08-16T10:17:57.1879997+00:00\",\"UpdateDate\":\"2021-08-16T10:17:57.2661253+00:00\",\"PaidDate\":null,\"OrderStatus\":3,\"HangfireJobs\":[]}",

//                OrderNumber = "0206",

//                OrderStatusCol = 3,

//                CustomerEmail = "gudmundur@veritas.is",

//                CustomerName = "1743",

//                CustomerId = 1283,

//                CustomerUsername = "1002813929",

//                TotalAmount = 16011,

//                Currency = "kr",

//                StoreAlias = "Distica",

//                //CreateDate
//            };
//            var oi = new OrderInfo(od);
//        }
//        //[TestMethod]
//        //public void CreatesOrder()
//        //{
//        //    var (fac, reg) = Helpers.RegisterAll();
//        //    reg.Register(Mock.Of<IProductDiscountService>());
//        //    Helpers.RegisterUmbracoHelper(reg, fac);

//        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
//        //    var product = Objects.Objects.Get_Shirt3_Product();

//        //    var orderSvc = new OrderServiceMocks().orderSvc;

//        //    var oi = orderSvc.AddOrderLineAsync(product, 1, store).Result;

//        //    Assert.AreEqual(1, oi.OrderLines.Count);
//        //}

//        //[TestMethod]
//        //public void AddsOrderLine()
//        //{
//        //    var (fac, reg) = Helpers.RegisterAll();
//        //    reg.Register(Mock.Of<IProductDiscountService>());
//        //    Helpers.RegisterUmbracoHelper(reg, fac);

//        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
//        //    var product2 = Objects.Objects.Get_Shirt2_Product();
//        //    var product3 = Objects.Objects.Get_Shirt3_Product();

//        //    var orderSvcMocks = new OrderServiceMocks();
//        //    var orderSvc = orderSvcMocks.orderSvc;
//        //    var oi = orderSvc.AddOrderLineAsync(product2, 1, store).Result;
//        //    Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
//        //    oi = orderSvc.AddOrderLineAsync(product3, 1, store).Result;

//        //    Assert.AreEqual(2, oi.OrderLines.Count);
//        //}

//        //[TestMethod]
//        //public void UpdatesQuantityPositive()
//        //{
//        //    var (fac, reg) = Helpers.RegisterAll();
//        //    reg.Register(Mock.Of<IProductDiscountService>());
//        //    Helpers.RegisterUmbracoHelper(reg, fac);

//        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
//        //    var product = Objects.Objects.Get_Shirt3_Product();

//        //    var orderSvcMocks = new OrderServiceMocks();
//        //    var orderSvc = orderSvcMocks.orderSvc;
//        //    var oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;
//        //    Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
//        //    oi = orderSvc.AddOrderLineAsync(product, 2, store).Result;

//        //    Assert.AreEqual(4, oi.TotalQuantity);
//        //}

//        //[TestMethod]
//        //public void UpdatesQuantityNegative()
//        //{
//        //    var (fac, reg) = Helpers.RegisterAll();
//        //    reg.Register(Mock.Of<IProductDiscountService>());
//        //    Helpers.RegisterUmbracoHelper(reg, fac);

//        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
//        //    var product = Objects.Objects.Get_Shirt3_Product();

//        //    var orderSvcMocks = new OrderServiceMocks();
//        //    var orderSvc = orderSvcMocks.orderSvc;
//        //    var oi = orderSvc.AddOrderLineAsync(product, 3, store).Result;
//        //    Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
//        //    oi = orderSvc.AddOrderLineAsync(product, -1, store).Result;

//        //    Assert.AreEqual(2, oi.TotalQuantity);
//        //}

//        //[TestMethod]
//        //public void SetsQuantity()
//        //{
//        //    var (fac, reg) = Helpers.RegisterAll();
//        //    reg.Register(Mock.Of<IProductDiscountService>());
//        //    Helpers.RegisterUmbracoHelper(reg, fac);

//        //    var store = Objects.Objects.Get_IS_Store_Vat_NotIncluded();
//        //    var product = Objects.Objects.Get_Shirt3_Product();

//        //    var orderSvcMocks = new OrderServiceMocks();
//        //    var orderSvc = orderSvcMocks.orderSvc;
//        //    var oi = orderSvc.AddOrderLineAsync(product, 1, store).Result;
//        //    Helpers.AddOrderInfoToHttpSession(oi, store, orderSvcMocks);
//        //    oi = orderSvc.AddOrderLineAsync(product, 3, store, OrderAction.Set).Result;

//        //    Assert.AreEqual(3, oi.TotalQuantity);
//        //}

//        //const string orderJson = @"{""StoreInfo"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":{""CurrencyFormat"":""C"",""ISOCurrencySymbol"":""USD"",""CurrencyValue"":""en-US"",""CurrencySymbol"":""kr""},""Currencies"":[{""CurrencyFormat"":""C"",""ISOCurrencySymbol"":""USD"",""CurrencyValue"":""en-US"",""CurrencySymbol"":""kr""}],""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""Discount"":null,""Coupon"":null,""UniqueId"":""1af7be2e-4ec3-45b6-b91c-5de2b63c0502"",""ReferenceId"":67,""OrderNumber"":""IS0067"",""OrderLines"":[{""ProductKey"":""e3402d14-1bfc-4992-b7d6-13a8923b0de3"",""Key"":""64a9d8bb-dfc1-4c8a-a400-65b4c643070c"",""Product"":{""Properties"":{""id"":""1082"",""key"":""e3402d14-1bfc-4992-b7d6-13a8923b0de3"",""parentID"":""1070"",""level"":""5"",""writerID"":""1"",""creatorID"":""0"",""nodeType"":""1058"",""template"":""1084"",""sortOrder"":""2"",""createDate"":""20160907134955000"",""updateDate"":""20171122213919000"",""nodeName"":""Jackets Product 1"",""urlName"":""jackets-product-1"",""writerName"":""gardar@vettvangur.is"",""creatorName"":""Vettvangur@vettvangur.is"",""nodeTypeAlias"":""ekmProduct"",""path"":""-1,1066,1067,1179,1070,1082"",""disable"":""{\""values\"":{\""IS\"":\""0\"",\""EN\"":\""0\"",\""DK\"":\""0\"",\""EU\"":\""0\""},\""dtdGuid\"":\""383bb1cf-eb59-4bff-b5de-48f17f8d3bef\""}"",""price"":""{\""values\"":{\""IS\"":\""25000\"",\""EN\"":\""234\"",\""DK\"":\""345\"",\""EU\"":\""456\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}"",""sku"":""women-sku-jacket-1"",""slug"":""{\""values\"":{\""IS\"":\""jakki-vara-1\"",\""EN\"":\""jackets-product-1\"",\""DK\"":\""jackets-product-1\"",\""EU\"":\""jackets-product-1\""},\""dtdGuid\"":\""c24bad83-32b9-4664-ad88-8bd76c10aea1\""}"",""title"":""{\""values\"":{\""IS\"":\""Jakki vara 1\"",\""EN\"":\""Jackets Product 1\"",\""DK\"":\""Jackets Product 1\"",\""EU\"":\""Jackets Product 1\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}""},""Price"":{""Discount"":null,""Store"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":[{""CurrencyFormat"":""C"",""CurrencyValue"":""en-US""}],""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""DiscountAlwaysBeforeVAT"":false,""OriginalValue"":25000.0,""Quantity"":1,""Value"":27500.0},""ImageIds"":[],""VariantGroups"":[]},""Quantity"":2,""Discount"":null,""Coupon"":null,""Amount"":{""Discount"":null,""Store"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":[{""CurrencyFormat"":""C"",""Currency"":""en-US"",""CurrencyValue"":""en-US""}],""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":0.1},""DiscountAlwaysBeforeVAT"":false,""OriginalValue"":25000.0,""Quantity"":2,""Value"":55000.0}}],""ShippingProvider"":null,""PaymentProvider"":null,""TotalQuantity"":2,""CustomerInformation"":{""CustomerIpAddress"":""127.0.0.1"",""Customer"":{""Properties"":{},""Name"":"""",""Email"":"""",""Address"":"""",""City"":"""",""Country"":"""",""ZipCode"":"""",""Phone"":"""",""UserId"":0,""UserName"":null},""Shipping"":{""Properties"":{},""Name"":"""",""Address"":"""",""City"":"""",""Country"":"""",""ZipCode"":""""}},""OrderLineTotal"":{""Value"":50000.0,""CurrencyString"":""50.000 ISK""},""SubTotal"":{""Value"":50000.0,""CurrencyString"":""50.000 ISK""},""Vat"":{""Value"":5000.0,""CurrencyString"":""5.000 ISK""},""GrandTotal"":{""Value"":55000.0,""CurrencyString"":""55.000 ISK""},""ChargedAmount"":{""Value"":55000.0,""CurrencyString"":""55.000 ISK""},""CreateDate"":""2018-03-20T15:39:36.8320656+00:00"",""UpdateDate"":""0001-01-01T00:00:00"",""PaidDate"":""0001-01-01T00:00:00"",""OrderStatus"":3,""HangfireJobs"":[]}";

//        //[TestMethod]
//        //public void CanParseOrderJson_WithCurrencyFormat()
//        //{
//        //    Helpers.RegisterAll();

//        //    var orderData = new OrderData
//        //    {
//        //        OrderInfo = orderJson,
//        //    };

//        //    var orderInfo = new OrderInfo(orderData);

//        //    Assert.AreEqual(1, orderInfo.OrderLines.Count);
//        //    Assert.AreEqual("127.0.0.1", orderInfo.CustomerInformation.CustomerIpAddress);

//        //    Assert.AreEqual("$55,000.00", orderInfo.ChargedAmount.CurrencyString);
//        //}
//    }
//}
