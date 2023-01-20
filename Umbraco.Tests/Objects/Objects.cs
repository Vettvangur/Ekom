//using Examine;
//using Ekom.Models;
//using Ekom.Models.Discounts;
//using System.Collections.Generic;

//namespace Ekom.Tests.Objects
//{
//    partial class Objects
//    {
//        /// <summary>
//        /// IS store with 10% Vat not included
//        /// </summary>
//        /// <returns></returns>
//        public static Store Get_IS_Store_Vat_NotIncluded()
//            => new CustomStore(Store_IS_Vat_NotIncluded.json, 1059);
//        public static Store Get_IS_Store_Vat_Included()
//            => new CustomStore(Store_IS_Vat_Included.json, 1059);
//        public static Store Get_DK_Store_Vat_Included()
//            => new CustomStore(Store_DK_Vat_Included.json, 1110);

//        public static SearchResult StoreResult
//            => new SearchResult("1096", 0, () => new Dictionary<string, List<string>>
//            {
//                { "__NodeId", new List<string> { "1096" } },
//                { "__NodeTypeAlias", new List<string> { "ekmStore" } },
//                { "__Published", new List<string> { "y" } },
//                { "__Key", new List<string> { "9d67c718-a703-4958-8e2d-271670faf207" } },
//                { "parentID", new List<string> { "1094" } },
//                { "level", new List<string> { "3" } },
//                { "nodeName", new List<string> { "IS" } },
//                { "urlName", new List<string> { "is" } },
//                { "__Path", new List<string> { "-1,1089,1094,1096" } },
//                { "nodeType", new List<string> { "1084" } },
//                { "storeRootNode", new List<string> { "umb://document/8cea2a5d0290434caa9812850ae4c7d6" } },
//                { "vat", new List<string> { "25" } },
//                { "culture", new List<string> { "is-IS" } },
//                { "currency", new List<string> { "is-IS" } },
//                { "vatIncludedInPrice", new List<string> { "1" } },
//                { "orderNumberTemplate", new List<string> { "#orderIdPadded#" } },
//                { "sortOrder", new List<string> { "1" } },
//            });
//        public static CustomProduct Get_Shirt2_Product()
//            => new CustomProduct(Shirt_product_2.json, Get_IS_Store_Vat_NotIncluded());
//        public static CustomProduct Get_Shirt3_Product()
//            => new CustomProduct(Shirt_product_3.oldjson, Get_IS_Store_Vat_NotIncluded());
//        public static Product Get_Shirt3_Product_ForProductDiscount()
//            => new CustomProduct(Shirt_product_3.json, Get_IS_Store_Vat_NotIncluded());

//        public static SearchResult Get_shirt2_blue_S_variant_SearchResult()
//            => new SearchResult("1200", 0, () => new Dictionary<string, List<string>>
//                {
//                    { "__NodeId", new List<string> { "1200" } },
//                    { "__NodeTypeAlias", new List<string> { "ekmProductVariant" } },
//                    { "__Published", new List<string> { "y" } },
//                    { "__Key", new List<string> { "ae263831-ff11-4af9-96d6-3cbc5b35b197" } },
//                    { "parentID", new List<string> { "1195" } },
//                    { "level", new List<string> { "7" } },
//                    { "nodeName", new List<string> { "S" } },
//                    { "urlName", new List<string> { "s" } },
//                    { "__Path", new List<string> { "-1,1066,1067,1179,1079,1195,1200" } },
//                    { "nodeType", new List<string> { "1090" } },
//                    { "price", new List<string> { "{\"values\":{\"IS\":\"4900\"},\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}" } },
//                    { "sku", new List<string> { "women-sku-shirt-2-blue-s" } },
//                    { "stock", new List<string> { "{\"values\":null,\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}" } },
//                    { "title", new List<string> { "{\"values\":{\"IS\":\"S\",\"EN\":\"S\",\"DK\":\"S\",\"EU\":\"S\"},\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}" } },
//                    { "sortOrder", new List<string> { "1" } },
//                });
//        public static Variant Get_shirt2_blue_S_variant()
//            => new Variant(
//                Get_shirt2_blue_S_variant_SearchResult(),
//                Get_IS_Store_Vat_NotIncluded()
//            );
//        public static SearchResult Get_shirt2_blue_variantgroup_SearchResult()
//            => new SearchResult("1195", 0, () => new Dictionary<string, List<string>>
//                {
//                    { "__NodeId", new List<string> { "1195" } },
//                    { "__NodeTypeAlias", new List<string> { "ekmProductVariantGroup" } },
//                    { "__Published", new List<string> { "y" } },
//                    { "__Key", new List<string> { "6741e6d9-8a7c-4330-8b2f-2f65073f62a4" } },
//                    { "parentID", new List<string> { "1079" } },
//                    { "level", new List<string> { "6" } },
//                    { "nodeName", new List<string> { "Blue" } },
//                    { "urlName", new List<string> { "blue" } },
//                    { "__Path", new List<string> { "-1,1066,1067,1179,1079,1195" } },
//                    { "nodeType", new List<string> { "1089" } },
//                    { "color", new List<string> { "0056ff" } },
//                    { "images", new List<string> { "2209,2210" } },
//                    { "title", new List<string> { "{\"values\":{\"IS\":\"Blár\",\"EN\":\"Blue\",\"DK\":\"Blue\",\"EU\":\"Blue\"},\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}" } },
//                    { "sortOrder", new List<string> { "1" } },
//                });
//        public static VariantGroup Get_shirt2_blue_variantgroup()
//            => new VariantGroup(
//                Get_shirt2_blue_variantgroup_SearchResult(),
//                Get_IS_Store_Vat_NotIncluded());

//        private static SearchResult Get_Discount_fixed_500_SearchResult()
//            => new SearchResult("2246", 0, () => new Dictionary<string, List<string>>
//                {
//                    { "__NodeId", new List<string> { "2246" } },
//                    { "__NodeTypeAlias", new List<string> { "ekmdiscount" } },
//                    { "__Published", new List<string> { "y" } },
//                    { "__Key", new List<string> { "1506f28f-6397-4fd6-b330-9e2cabd50a57" } },
//                    { "parentID", new List<string> { "2245" } },
//                    { "level", new List<string> { "3" } },
//                    { "nodeName", new List<string> { "10% off" } },
//                    { "urlName", new List<string> { "10-off" } },
//                    { "__Path", new List<string> { "-1,1066,2245,2246" } },
//                    { "nodeType", new List<string> { "2237" } },
//                    { "disable", new List<string> { "{\"values\":{\"IS\":\"0\"},\"dtdGuid\":\"383bb1cf-eb59-4bff-b5de-48f17f8d3bef\"}" } },
//                    { "discount", new List<string> { "{\"IS\": [{ \"Currency\": \"is-IS\", \"Value\": 500 } ] }" } },
//                    { "discountItems", new List<string> { "umb://document/9e8665c7d40542b58913175ca066d5c9,umb://document/30762e8049594c24a29a13583ff73a06" } },
//                    { "title", new List<string> { "{\"values\":null,\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}" } },
//                    { "type", new List<string> { "Fixed" } },
//                    { "sortOrder", new List<string> { "1" } },
//                    { "stackable", new List<string> { "1" } },
//                });
//        //private static SearchResult Get_OrderDiscount_fixed_500_SearchResult()
//        //    => new SearchResult("2246", 0, () => new Dictionary<string, List<string>>
//        //        {
//        //            { "__NodeId", new List<string> { "2246" } },
//        //            { "__NodeTypeAlias", new List<string> { "ekmdiscount" } },
//        //            { "__Published", new List<string> { "y" } },
//        //            { "__Key", new List<string> { "1506f28f-6397-4fd6-b330-9e2cabd50a57" } },
//        //            { "parentID", new List<string> { "2245" } },
//        //            { "level", new List<string> { "3" } },
//        //            { "nodeName", new List<string> { "10% off" } },
//        //            { "urlName", new List<string> { "10-off" } },
//        //            { "__Path", new List<string> { "-1,1066,2245,2246" } },
//        //            { "nodeType", new List<string> { "2237" } },
//        //            { "disable", new List<string> { "{\"values\":{\"IS\":\"0\"},\"dtdGuid\":\"383bb1cf-eb59-4bff-b5de-48f17f8d3bef\"}" } },
//        //            { "discount", new List<string> { "{\"IS\": [{ \"Currency\": \"is-IS\", \"Value\": 500 } ] }" } },
//        //            { "discountItems", new List<string> { "" } },
//        //            { "title", new List<string> { "{\"values\":null,\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}" } },
//        //            { "type", new List<string> { "Fixed" } },
//        //            { "sortOrder", new List<string> { "1" } },
//        //        });
//        public static Discount Get_Discount_fixed_500()
//            => new Discount(
//                Get_Discount_fixed_500_SearchResult(),
//                Get_IS_Store_Vat_NotIncluded());
//        //public static Discount Get_OrderDiscount_fixed_500()
//        //    => new Discount(
//        //        Get_OrderDiscount_fixed_500_SearchResult(),
//        //        Get_IS_Store_Vat_NotIncluded());
//        public static Discount Get_ExclusiveDiscount_fixed_500()
//            => new Discount(
//                new SearchResult("2246", 0, () => new Dictionary<string, List<string>>
//                {
//                    { "__NodeId", new List<string> { "2246" } },
//                    { "__NodeTypeAlias", new List<string> { "ekmdiscount" } },
//                    { "__Published", new List<string> { "y" } },
//                    { "__Key", new List<string> { "1506f28f-6397-4fd6-b330-9e2cabd50a57" } },
//                    { "parentID", new List<string> { "2245" } },
//                    { "level", new List<string> { "3" } },
//                    { "nodeName", new List<string> { "10% off" } },
//                    { "urlName", new List<string> { "10-off" } },
//                    { "__Path", new List<string> { "-1,1066,2245,2246" } },
//                    { "nodeType", new List<string> { "2237" } },
//                    { "disable", new List<string> { "{\"values\":{\"IS\":\"0\"},\"dtdGuid\":\"383bb1cf-eb59-4bff-b5de-48f17f8d3bef\"}" } },
//                    { "discount", new List<string> { "{\"IS\": [{ \"Currency\": \"is-IS\", \"Value\": 500 } ] }" } },
//                    { "discountItems", new List<string> { "" } },
//                    { "title", new List<string> { "{\"values\":null,\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}" } },
//                    { "stackable", new List<string> { "0" } },
//                    { "sortOrder", new List<string> { "1" } },
//                }),
//                Get_IS_Store_Vat_NotIncluded());
//        public static ProductDiscount Get_ProductDiscount_fixed_500()
//            => new ProductDiscount(
//                Get_Discount_fixed_500_SearchResult(),
//                Get_IS_Store_Vat_NotIncluded());
//        private static SearchResult DiscountPercentage50_SearchResult()
//            => new SearchResult("2246", 0, () => new Dictionary<string, List<string>>
//                {
//                    { "__NodeId", new List<string> { "2246" } },
//                    { "__NodeTypeAlias", new List<string> { "ekmdiscount" } },
//                    { "__Published", new List<string> { "y" } },
//                    { "__Key", new List<string> { "1506f28f-6397-4fd6-b330-9e2cabd50a58" } },
//                    { "parentID", new List<string> { "2245" } },
//                    { "level", new List<string> { "3" } },
//                    { "nodeName", new List<string> { "50% off" } },
//                    { "urlName", new List<string> { "50-off" } },
//                    { "__Path", new List<string> { "-1,1066,2245,2246" } },
//                    { "nodeType", new List<string> { "2237" } },
//                    { "disable", new List<string> { "" } },
//                    { "discount", new List<string> { "{\"IS\": [{ \"Currency\": \"is-IS\", \"Value\": 50 } ] }" } },
//                    { "discountItems", new List<string> { "umb://document/9e8665c7d40542b58913175ca066d5c9" } },
//                    { "title", new List<string> { "" } },
//                    { "type", new List<string> { "Percentage" } },
//                    { "sortOrder", new List<string> { "1" } },
//                    { "stackable", new List<string> { "1" } },
//                });

//        public static Discount Get_Discount_percentage_50()
//            => new Discount(
//                DiscountPercentage50_SearchResult(),
//                Get_IS_Store_Vat_NotIncluded());
//        public static ProductDiscount Get_ProductDiscount_percentage_50()
//            => new ProductDiscount(
//                DiscountPercentage50_SearchResult(),
//                Get_IS_Store_Vat_NotIncluded());
//        public static Discount Get_ExclusiveDiscount_percentage_50()
//            => new Discount(
//                new SearchResult("2246", 0, () => new Dictionary<string, List<string>>
//                {
//                    { "__NodeId", new List<string> { "2246" } },
//                    { "__NodeTypeAlias", new List<string> { "ekmdiscount" } },
//                    { "__Published", new List<string> { "y" } },
//                    { "__Key", new List<string> { "1506f28f-6397-4fd6-b330-9e2cabd50a58" } },
//                    { "parentID", new List<string> { "2245" } },
//                    { "level", new List<string> { "3" } },
//                    { "nodeName", new List<string> { "50% off" } },
//                    { "urlName", new List<string> { "50-off" } },
//                    { "__Path", new List<string> { "-1,1066,2245,2246" } },
//                    { "nodeType", new List<string> { "2237" } },
//                    { "disable", new List<string> { "" } },
//                    { "discount", new List<string> { "{\"IS\": [{ \"Currency\": \"is-IS\", \"Value\": 50 } ] }" } },
//                    { "discountItems", new List<string> { "umb://document/9e8665c7d40542b58913175ca066d5c9" } },
//                    { "title", new List<string> { "" } },
//                    { "type", new List<string> { "Percentage" } },
//                    { "stackable", new List<string> { "0" } },
//                    { "sortOrder", new List<string> { "1" } },
//                }),
//                Get_IS_Store_Vat_NotIncluded());
//        public static Discount Get_Discount_fixed_1000_Min_2000()
//            => new Discount(
//                new SearchResult("2246", 0, () => new Dictionary<string, List<string>>
//                {
//                    { "__NodeId", new List<string> { "2246" } },
//                    { "__NodeTypeAlias", new List<string> { "ekmdiscount" } },
//                    { "__Published", new List<string> { "y" } },
//                    { "__Key", new List<string> { "1506f28f-6397-4fd6-b330-9e2cabd50a57" } },
//                    { "parentID", new List<string> { "2245" } },
//                    { "level", new List<string> { "3" } },
//                    { "nodeName", new List<string> { "Global Fixed 1000 Minimum 2000" } },
//                    { "urlName", new List<string> { "global-fixed-1000-minimum-2000" } },
//                    { "__Path", new List<string> { "-1,1066,2245,2246" } },
//                    { "nodeType", new List<string> { "2237" } },
//                    { "disable", new List<string> { "" } },
//                    { "discount", new List<string> { "{\"IS\": [{ \"Currency\": \"is-IS\", \"Value\": 1000 } ] }" } },
//                    { "discountItems", new List<string> { "umb://document/9e8665c7d40542b58913175ca066d5c9" } },
//                    { "startOfRange", new List<string> { "2000" } },
//                    { "title", new List<string> { "" } },
//                    { "type", new List<string> { "Fixed" } },
//                    { "sortOrder", new List<string> { "1" } },
//                    { "stackable", new List<string> { "1" } },
//                }),
//                Get_IS_Store_Vat_NotIncluded());

//        public static SearchResult Get_Category_Women_SearchResult()
//            => new SearchResult("1179", 0, () => new Dictionary<string, List<string>>
//                {
//                    { "__NodeId", new List<string> { "1179" } },
//                    { "__NodeTypeAlias", new List<string> { "ekmCategory" } },
//                    { "__Published", new List<string> { "y" } },
//                    { "__Key", new List<string> { "3a5d1dd5-9cbd-4e21-a83d-a759d77190a1" } },
//                    { "parentID", new List<string> { "1067" } },
//                    { "level", new List<string> { "3" } },
//                    { "nodeName", new List<string> { "Women" } },
//                    { "urlName", new List<string> { "women" } },
//                    { "__Path", new List<string> { "-1,1066,1067,1179" } },
//                    { "disable", new List<string> { "{\"values\":{\"IS\":\"1\",\"EN\":\"0\",\"DK\":\"0\",\"EU\":\"0\"},\"dtdGuid\":\"383bb1cf-eb59-4bff-b5de-48f17f8d3bef\"}" } },
//                    { "slug", new List<string> { "\"values\":{\"IS\":\"konur\",\"EN\":\"women\",\"DK\":\"kvinder\",\"EU\":\"women\"},\"dtdGuid\":\"b79a2484-7a52-47a1-8070-c3a347bc22ee\"}" } },
//                    { "title", new List<string> { "{\"values\":{\"IS\":\"Konur\",\"EN\":\"Women\",\"DK\":\"Kvinder\",\"EU\":\"Women\"},\"dtdGuid\":\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\"}" } },
//                    { "sortOrder", new List<string> { "1" } },
//                });

//        public static Category Get_Category_Women()
//            => new Category(
//                Get_Category_Women_SearchResult(),
//                Get_IS_Store_Vat_NotIncluded());
//    }
//}
