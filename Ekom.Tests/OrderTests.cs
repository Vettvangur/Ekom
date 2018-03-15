using Ekom.Models;
using Ekom.Models.Data;
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

            var orderSvc = Helpers.GetOrderService();
            var oi = orderSvc.AddOrderLine(product, 1, store);

            Assert.IsTrue(oi.OrderLines.Count == 1);
        }

        [TestMethod]
        public void CanParseOrderJson()
        {
            string json = @"{""CustomerInformation"":{""CustomerIpAddress"":""127.0.0.1"",""Customer"":{""Properties"":{},""Name"":null,""Email"":null,""Address"":null,""City"":null,""Country"":null,""ZipCode"":null,""Phone"":null,""UserId"":0,""UserName"":null},""Shipping"":{""Properties"":{},""Name"":null,""Address"":null,""City"":null,""Country"":null,""ZipCode"":null}},""UniqueId"":""3605279c-6b8f-4fa5-bbe8-0c6234b89d0e"",""ReferenceId"":29,""OrderNumber"":""IS0029"",""OrderLines"":[{""Id"":""fed0c713-d43e-4d3e-96f9-b043359dca8e"",""Product"":{""Properties"":{""id"":""1078"",""key"":""584f7b36-b87d-4605-8169-254da1f66dca"",""parentID"":""1068"",""level"":""5"",""writerID"":""1"",""creatorID"":""0"",""nodeType"":""1058"",""template"":""1084"",""sortOrder"":""0"",""createDate"":""20160907134925000"",""updateDate"":""20171127163323000"",""nodeName"":""Shirt Product 1"",""urlName"":""shirt-product-1"",""writerName"":""gardar@vettvangur.is"",""creatorName"":""Vettvangur@vettvangur.is"",""nodeTypeAlias"":""ekmProduct"",""path"":""-1,1066,1067,1179,1068,1078"",""categories"":""1069"",""disable"":""{\""values\"":{\""IS\"":\""1\"",\""EN\"":\""1\"",\""DK\"":\""0\"",\""EU\"":\""0\""},\""dtdGuid\"":\""383bb1cf-eb59-4bff-b5de-48f17f8d3bef\""}"",""images"":""2208,2209"",""price"":""{\""values\"":{\""IS\"":\""5\"",\""EN\"":\""1\"",\""DK\"":\""3\"",\""EU\"":\""2\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}"",""primaryVariantGroup"":""{\""values\"":null,\""dtdGuid\"":\""9a8c8132-a088-45d0-b310-afb04443977a\""}"",""sku"":""women-sku-shirt-1"",""slug"":""{\""values\"":{\""IS\"":\""skyrta-vara-1\"",\""EN\"":\""shirt-product-1\"",\""DK\"":\""skjorter-1\"",\""EU\"":\""shirt-product-1\""},\""dtdGuid\"":\""9c96b135-1e25-47fd-8421-3e1d53ad4785\""}"",""title"":""{\""values\"":{\""IS\"":\""Skyrta vara 1\"",\""EN\"":\""b\"",\""DK\"":\""Skjorter 1\"",\""EU\"":\""Shirt Product 1\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}""},""Price"":{""Value"":5.0,""WithVat"":{""Value"":5.5,""ToCurrencyString"":""6 ISK""},""WithoutVat"":{""Value"":5.0,""ToCurrencyString"":""5 ISK""},""BeforeDiscount"":null,""Discount"":null,""Vat"":null},""ImageIds"":[""bb6e4219-f646-4fd7-aebd-63f773756067"",""528eab0e-76f8-47e7-bad1-dd442beded0c""],""VariantGroups"":[]},""Quantity"":1,""Amount"":{""Value"":5.0,""WithVat"":{""Value"":5.5,""ToCurrencyString"":""6 ISK""},""WithoutVat"":{""Value"":5.0,""ToCurrencyString"":""5 ISK""},""BeforeDiscount"":null,""Discount"":null,""Vat"":null}}],""ShippingProvider"":null,""PaymentProvider"":null,""Quantity"":1,""OrderLineTotal"":{""Value"":5.0,""WithVat"":{""Value"":5.5,""ToCurrencyString"":""6 ISK""},""WithoutVat"":{""Value"":5.0,""ToCurrencyString"":""5 ISK""},""BeforeDiscount"":null,""Discount"":null,""Vat"":null},""SubTotal"":{""Value"":5.0,""WithVat"":{""Value"":5.5,""ToCurrencyString"":""6 ISK""},""WithoutVat"":{""Value"":5.0,""ToCurrencyString"":""5 ISK""},""BeforeDiscount"":null,""Discount"":null,""Vat"":null},""ChargedAmount"":{""Value"":5.0,""WithVat"":{""Value"":5.5,""ToCurrencyString"":""6 ISK""},""WithoutVat"":{""Value"":5.0,""ToCurrencyString"":""5 ISK""},""BeforeDiscount"":null,""Discount"":null,""Vat"":null},""StoreInfo"":{""Key"":""4fb25750-35d8-4b48-a288-fa2ae876993f"",""Currency"":"""",""Culture"":""is-IS"",""Alias"":""IS"",""VatIncludedInPrice"":false,""Vat"":10.0},""CreateDate"":""2018-01-12T13:18:23.547Z"",""UpdateDate"":""2018-01-12T14:37:21.237Z"",""PaidDate"":""0001-01-01T00:00:00"",""OrderStatus"":3}";
            var store = new Store();

            var orderData = new OrderData
            {
                OrderInfo = json,
            };

            var orderInfo = new OrderInfo(orderData);

            Assert.IsTrue(orderInfo.OrderLines.Count == 1);
        }
    }
}
