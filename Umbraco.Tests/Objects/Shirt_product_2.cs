using Examine;
using System.Collections.Generic;

namespace Ekom.Tests.Objects
{
    static class Shirt_product_2
    {
        /// <summary>
        /// Member of variant group
        ///{
        ///	"__NodeId": "1079",
        ///	"__Key": "30762e80-4959-4c24-a29a-13583ff73a06",
        ///	"parentID": "1179",
        ///	"level": "5",
        ///	"writerID": "0",
        ///	"creatorID": "0",
        ///	"nodeType": "1058",
        ///	"template": "1084",
        ///	"sortOrder": "1",
        ///	"createDate": "20160907134933000",
        ///	"updateDate": "20171127162854000",
        ///	"nodeName": "Shirt Product 2",
        ///	"urlName": "shirt-product-2",
        ///	"writerName": "Vettvangur@vettvangur.is",
        ///	"creatorName": "Vettvangur@vettvangur.is",
        ///	"__NodeTypeAlias": "ekmProduct",
        ///	"__Path": "-1,1066,1067,1179,1079",
        ///	"disable": "{\"values\":{\"IS\":\"0\",\"EN\":\"0\",\"DK\":\"0\",\"EU\":\"0\"},\"dtdGuid\":\"383bb1cf-eb59-4bff-b5de-48f17f8d3bef\"}",
        ///	"images": "2208",
        ///	"price": "{\"values\":{\"IS\":\"3990\",\"EN\":\"123\",\"DK\":\"123\",\"EU\":\"123\"},\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}",
        ///	"primaryVariantGroup": "{\"values\":{\"IS\":\"umb://document/6741e6d98a7c43308b2f2f65073f62a4\"},\"dtdGuid\":\"9a8c8132-a088-45d0-b310-afb04443977a\"}",
        ///	"sku": "women-sku-shirt-2",
        ///	"slug": "{\"values\":{\"IS\":\"skyrta-vara-2\",\"EN\":\"shirt-product-2\",\"DK\":\"skjorter-2\",\"EU\":\"shirt-product-2\"},\"dtdGuid\":\"42271614-4e0a-4a93-81e4-ad280faadcaf\"}",
        ///	"title": "{\"values\":{\"IS\":\"Skyrta vara 2\",\"EN\":\"Shirt Product 2\",\"DK\":\"Skjorter 2\",\"EU\":\"Shirt Product 2\"},\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}"
        ///}
        /// </summary>
        public const string json = @"{""__NodeId"":""1079"",""__Key"":""30762e80-4959-4c24-a29a-13583ff73a06"",""parentID"":""1179"",""level"":""5"",""writerID"":""0"",""creatorID"":""0"",""nodeType"":""1058"",""template"":""1084"",""sortOrder"":""1"",""createDate"":""20160907134933000"",""updateDate"":""20171127162854000"",""nodeName"":""Shirt Product 2"",""urlName"":""shirt-product-2"",""writerName"":""Vettvangur@vettvangur.is"",""creatorName"":""Vettvangur@vettvangur.is"",""__NodeTypeAlias"":""ekmProduct"",""__Path"":""-1,1066,1067,1179,1079"",""disable"":""{\""values\"":{\""IS\"":\""0\"",\""EN\"":\""0\"",\""DK\"":\""0\"",\""EU\"":\""0\""},\""dtdGuid\"":\""383bb1cf-eb59-4bff-b5de-48f17f8d3bef\""}"",""images"":""2208"",""price"":""{\""values\"":{\""IS\"":\""3990\"",\""EN\"":\""123\"",\""DK\"":\""123\"",\""EU\"":\""123\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}"",""primaryVariantGroup"":""{\""values\"":{\""IS\"":\""umb://document/6741e6d98a7c43308b2f2f65073f62a4\""},\""dtdGuid\"":\""9a8c8132-a088-45d0-b310-afb04443977a\""}"",""sku"":""women-sku-shirt-2"",""slug"":""{\""values\"":{\""IS\"":\""skyrta-vara-2\"",\""EN\"":\""shirt-product-2\"",\""DK\"":\""skjorter-2\"",\""EU\"":\""shirt-product-2\""},\""dtdGuid\"":\""42271614-4e0a-4a93-81e4-ad280faadcaf\""}"",""title"":""{\""values\"":{\""IS\"":\""Skyrta vara 2\"",\""EN\"":\""Shirt Product 2\"",\""DK\"":\""Skjorter 2\"",\""EU\"":\""Shirt Product 2\""},\""dtdGuid\"":\""75e484b5-66b9-4d86-b651-5ebb7a3c580b\""}""}";
        public static SearchResult SearchResult()
            => new SearchResult("1079", 0, () => new Dictionary<string, List<string>>
            {
                    { "__NodeId", new List<string> { "1079" } },
                    { "__NodeTypeAlias", new List<string> { "ekmProduct" } },
                    { "__Published", new List<string> { "y" } },
                    { "__Key", new List<string> { "30762e80-4959-4c24-a29a-13583ff73a06" } },
                    { "parentID", new List<string> { "1179" } },
                    { "level", new List<string> { "5" } },
                    { "nodeName", new List<string> { "Shirt Product 2" } },
                    { "urlName", new List<string> { "shirt-product-2" } },
                    { "__Path", new List<string> { "-1,1066,1067,1179,1079" } },
                    { "nodeType", new List<string> { "1058" } },
                    { "disable", new List<string> { "{\"values\":{\"IS\":\"0\",\"EN\":\"0\",\"DK\":\"0\",\"EU\":\"0\"},\"dtdGuid\":\"383bb1cf-eb59-4bff-b5de-48f17f8d3bef\"}" } },
                    { "images", new List<string> { "2208" } },
                    { "price", new List<string> { "{\"values\":{\"IS\":\"3990\",\"EN\":\"123\",\"DK\":\"123\",\"EU\":\"123\"},\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}" } },
                    { "primaryVariantGroup", new List<string> { "{\"values\":{\"IS\":\"umb://document/6741e6d98a7c43308b2f2f65073f62a4\"},\"dtdGuid\":\"9a8c8132-a088-45d0-b310-afb04443977a\"}" } },
                    { "sku", new List<string> { "women-sku-shirt-2" } },
                    { "slug", new List<string> { "{\"values\":{\"IS\":\"skyrta-vara-2\",\"EN\":\"shirt-product-2\",\"DK\":\"skjorter-2\",\"EU\":\"shirt-product-2\"},\"dtdGuid\":\"42271614-4e0a-4a93-81e4-ad280faadcaf\"}" } },
                    { "title", new List<string> { "{\"values\":{\"IS\":\"Skyrta vara 2\",\"EN\":\"Shirt Product 2\",\"DK\":\"Skjorter 2\",\"EU\":\"Shirt Product 2\"},\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}" } },
            });
    }
}
