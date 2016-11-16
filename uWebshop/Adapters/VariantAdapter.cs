using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Cache;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Adapters
{
    public static class VariantAdapter
    {

        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        public static Variant CreateVariantItemFromExamine(SearchResult item, Store store)
        {

            try
            {
                var variant = new Variant();

                int variantGroupId = Convert.ToInt32(item.Fields["parentID"]);

                var pathField = item.Fields["path"];
                var paths = pathField.Split(',');

                int productId = Convert.ToInt32(paths.Reverse().Skip(2).Take(1).FirstOrDefault());

                var priceField = ExamineService.GetProperty(item,"price",store.Alias);
                decimal originalPrice = 0;

                decimal.TryParse(priceField, out originalPrice);

                variant.Id = item.Id;
                variant.Title = ExamineService.GetProperty(item, "title", store.Alias);
                variant.Path = pathField;
                variant.OriginalPrice = originalPrice;
                variant.VariantGroupId = variantGroupId;
                variant.ProductId = productId;
                variant.Store = store;
                variant.SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);

                return variant;
            }
            catch(Exception ex)
            {
                Log.Error("Error on creating variant item from Examine. Node id: " + item.Id, ex);
                return null;
            }

        }

        public static VariantGroup CreateVariantGroupItemFromExamine(SearchResult item, Store store)
        {

            try
            {

                int productId = Convert.ToInt32(item.Fields["parentID"]);
                var variantGroup = new VariantGroup(productId);
                
                variantGroup.Id = item.Id;
                variantGroup.Title = ExamineService.GetProperty(item, "title", store.Alias);
                variantGroup.Store = store;
                variantGroup.SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);

                return variantGroup;
            }
            catch(Exception ex)
            {
                Log.Error("Error on creating variant group item from Examine. Node id: " + item.Id, ex);
                return null;
            }

        }
    }
}
