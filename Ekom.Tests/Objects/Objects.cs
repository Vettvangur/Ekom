using Ekom.Models;
using Ekom.Models.Discounts;

namespace Ekom.Tests.Objects
{
    partial class Objects
    {
        const string shirt2_blue_S_variant_json = "{\"id\":\"1200\",\"key\":\"ae263831-ff11-4af9-96d6-3cbc5b35b197\",\"parentID\":\"1195\",\"level\":\"7\",\"writerID\":\"1\",\"creatorID\":\"1\",\"nodeType\":\"1090\",\"template\":\"0\",\"sortOrder\":\"0\",\"createDate\":\"20171123194102000\",\"updateDate\":\"20171127170839000\",\"nodeName\":\"S\",\"urlName\":\"s\",\"writerName\":\"gardar@vettvangur.is\",\"creatorName\":\"gardar@vettvangur.is\",\"nodeTypeAlias\":\"ekmProductVariant\",\"path\":\"-1,1066,1067,1179,1068,1079,1195,1200\",\"price\":\"{\\\"values\\\":{\\\"IS\\\":\\\"4900\\\"},\\\"dtdGuid\\\":\\\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\\\"}\",\"sku\":\"women-sku-shirt-2-blue-s\",\"stock\":\"{\\\"values\\\":null,\\\"dtdGuid\\\":\\\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\\\"}\",\"title\":\"{\\\"values\\\":{\\\"IS\\\":\\\"S\\\",\\\"EN\\\":\\\"S\\\",\\\"DK\\\":\\\"S\\\",\\\"EU\\\":\\\"S\\\"},\\\"dtdGuid\\\":\\\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\\\"}\"}";
        const string shirt2_blue_variantgroup_json = "{\"id\":\"1195\",\"key\":\"6741e6d9-8a7c-4330-8b2f-2f65073f62a4\",\"parentID\":\"1079\",\"level\":\"6\",\"writerID\":\"1\",\"creatorID\":\"1\",\"nodeType\":\"1089\",\"template\":\"0\",\"sortOrder\":\"1\",\"createDate\":\"20171123192228000\",\"updateDate\":\"20171124130014000\",\"nodeName\":\"Blue\",\"urlName\":\"blue\",\"writerName\":\"gardar@vettvangur.is\",\"creatorName\":\"gardar@vettvangur.is\",\"nodeTypeAlias\":\"ekmProductVariantGroup\",\"path\":\"-1,1066,1067,1179,1068,1079,1195\",\"color\":\"0056ff\",\"images\":\"2209,2210\",\"title\":\"{\\\"values\\\":{\\\"IS\\\":\\\"Blár\\\",\\\"EN\\\":\\\"Blue\\\",\\\"DK\\\":\\\"Blue\\\",\\\"EU\\\":\\\"Blue\\\"},\\\"dtdGuid\\\":\\\"75e484b5-66b9-4d86-b651-5ebb7a3c580b\\\"}\"}";

        /// <summary>
        /// IS store with 10% Vat not included
        /// </summary>
        /// <returns></returns>
        public static Store Get_IS_Store_Vat_NotIncluded() => new CustomStore(Store_IS_Vat_NotIncluded.json, 1059);
        public static Store Get_IS_Store_Vat_Included() => new CustomStore(Store_IS_Vat_Included.json, 1059);

        public static Store Get_DK_Store_Vat_Included() => new CustomStore(Store_DK_Vat_Included.json, 1110);

        public static Product Get_Shirt2_Product()
            => new CustomProduct(Shirt_product_2.json, Get_IS_Store_Vat_NotIncluded());
        public static Product Get_Shirt3_Product()
            => new CustomProduct(Shirt_product_3.oldjson, Get_IS_Store_Vat_NotIncluded());
        public static Product Get_Shirt3_Product_ForProductDiscount()
            => new CustomProduct(Shirt_product_3.json, Get_IS_Store_Vat_NotIncluded());
        public static Variant Get_shirt2_blue_S_variant()
            => new Variant(new CustomSearchResult(shirt2_blue_S_variant_json), Get_IS_Store_Vat_NotIncluded());
        public static VariantGroup Get_shirt2_blue_variantgroup()
            => new VariantGroup(new CustomSearchResult(shirt2_blue_variantgroup_json), Get_IS_Store_Vat_NotIncluded());

        public static Discount Get_Discount_fixed_500()
            => new Discount(new CustomSearchResult(Discount_fixed_500.json), Get_IS_Store_Vat_NotIncluded());
        public static Discount Get_Discount_percentage_50()
            => new Discount(new CustomSearchResult(Discount_percentage_50.json), Get_IS_Store_Vat_NotIncluded());
        public static Discount Get_Discount_fixed_1000_Min_2000()
            => new Discount(new CustomSearchResult(Discount_fixed_1000_Min_2000.json), Get_IS_Store_Vat_NotIncluded());

        public static Category Get_Category_Women()
            => new Category(new CustomSearchResult(Category_Women.json), Get_IS_Store_Vat_NotIncluded());
        public static ProductDiscount GetProductDiscount()
            => new ProductDiscount(new CustomSearchResult(Category_Women.json), Get_IS_Store_Vat_NotIncluded());
    }
}
