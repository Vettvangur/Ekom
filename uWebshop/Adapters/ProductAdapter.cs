using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Umbraco.Web;
using uWebshop.Cache;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Adapters
{
    public static class ProductAdapter
    {
        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        public static Product CreateProductItemFromExamine(SearchResult item, Store store)
        {

            try
            {
                var pathField = item.Fields["path"];

                var examineItemsFromPath = ExamineService.GetAllCatalogItemsFromPath(pathField);

                if (!CatalogService.IsItemDisabled(examineItemsFromPath, store))
                {
                    var product = new Product();

                    int categoryId = Convert.ToInt32(item.Fields["parentID"]);

                    var categoryField = item.Fields.Any(x => x.Key == "categories") ? item.Fields["categories"] : "";

                    var categories = new List<Category>();

                    var primaryCategory = CategoryCache.Instance._cache.FirstOrDefault(x => x.Value.Id == categoryId && x.Value.Store.Alias == store.Alias).Value;

                    if (primaryCategory != null)
                    {
                        categories.Add(primaryCategory);
                    }

                    if (!string.IsNullOrEmpty(categoryField))
                    {
                        var categoryIds = categoryField.Split(',');

                        foreach (var catId in categoryIds)
                        {
                            var intCatId = Convert.ToInt32(catId);

                            var categoryItem = CategoryCache.Instance._cache.FirstOrDefault(x => x.Value.Id == intCatId && x.Value.Store.Alias == store.Alias).Value;

                            if (categoryItem != null && !categories.Contains(categoryItem))
                            {
                                categories.Add(categoryItem);
                            }
                        }

                    }

                    var priceField = ExamineService.GetProperty(item, "price", store.Alias);
                    decimal originalPrice = 0;

                    decimal.TryParse(priceField, out originalPrice);

                    product.Id = item.Id;
                    product.Title = ExamineService.GetProperty(item, "title", store.Alias);
                    product.Path = pathField;
                    product.Slug = ExamineService.GetProperty(item, "slug", store.Alias);
                    product.OriginalPrice = originalPrice;
                    product.Categories = categories;
                    product.Store = store;
                    product.SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);
                    product.Urls = UrlService.BuildProductUrls(product.Slug, product.Categories, store);
                    product.Level = Convert.ToInt32(item.Fields["level"]);

                    if (product.Urls.Any() && !string.IsNullOrEmpty(product.Title))
                    {
                        return product;
                    }
                    else
                    {
                        return null;
                    }

                } else
                {
                    return null;
                }
     
            }
            catch(Exception ex) {
                Log.Error("Error on creating product item from Examine. Node id: " + item.Id, ex);
                return null;
            }
           
        }
    }
}
