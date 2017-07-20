using System;
using Microsoft.Practices.Unity;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using uWebshop.Models;
using uWebshop.Models.Data;
using uWebshop.Services;
using uWebshop.App_Start;
using uWebshop.Cache;
using Umbraco.Core.Models;

namespace uWebshop.Tests
{
    [TestClass]
    public class OrderRepository
    {
        [TestMethod]
        public void CanParseOrderJson()
        {
            var container = UnityConfig.GetConfiguredContainer();
            container.Resolve<IBaseCache<IDomain>>();
            container.Resolve<IBaseCache<Store>>();
            var orderSvc = container.Resolve<OrderService>();

            string json = @"{""UniqueId"":""373c4ca5-f6a7-45db-a749-0f9369191aaf"",""ReferenceId"":2,""OrderNumber"":""IS0002"",""OrderLines"":[{""Id"":""bcd9428a-c3b8-4222-bcd2-aa17a25bf6d0"",""Product"":{""Properties"":{""id"":""1078"",""key"":""584f7b36-b87d-4605-8169-254da1f66dca"",""parentID"":""1068"",""level"":""4"",""writerID"":""1"",""creatorID"":""0"",""nodeType"":""1058"",""template"":""1084"",""sortOrder"":""0"",""createDate"":""20160907134925000"",""updateDate"":""20170707123123000"",""nodeName"":""Shirt Product 1"",""urlName"":""shirt-product-1"",""writerName"":""gardar@vettvangur.is"",""creatorName"":""Vettvangur@vettvangur.is"",""nodeTypeAlias"":""uwbsProduct"",""path"":""-1,1066,1067,1068,1078"",""categories"":""1069"",""disable"":""0"",""disable_EN"":""0"",""price"":""10000"",""price_EN"":""20000"",""sku"":""sku-1234"",""slug"":""skyrta1"",""slug_EN"":""shirt-en-slug"",""title"":""Skyrta Vara 1123"",""title_EN"":""Shirt Product 1""},""Price"":{""Value"":10000.0,""WithVat"":{""Value"":11000.0},""WithoutVat"":{""Value"":10000.0},""BeforeDiscount"":null,""Discount"":null,""Vat"":null},""VariantGroups"":[]},""Quantity"":1,""Amount"":{""Value"":10000.0,""WithVat"":{""Value"":11000.0},""WithoutVat"":{""Value"":10000.0},""BeforeDiscount"":null,""Discount"":null,""Vat"":null}}],""Quantity"":1,""ChargedAmount"":{""Value"":11000.0},""StoreInfo"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":"""",""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":10.0},""CreateDate"":""2017-07-13T12:38:18.5519114+00:00"",""UpdateDate"":""0001-01-01T00:00:00"",""PaidDate"":""0001-01-01T00:00:00"",""OrderStatus"":3}";
            var store = new Store();

            var orderData = new OrderData
            {
                OrderInfo = json,
            };

            var orderInfo = new OrderInfo(orderData, store);

            var orderLines = orderSvc.CreateOrderLinesFromJson(orderData.OrderInfo);

            Assert.IsTrue(orderLines.Count == 1);
        }
    }
}
