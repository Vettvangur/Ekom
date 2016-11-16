using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using uWebshop.Models;
using uWebshop.Services;

namespace uWebshop.Adapters
{
    public static class CategoryAdapter
    {
        private static readonly ILog Log =
                LogManager.GetLogger(
                    MethodBase.GetCurrentMethod().DeclaringType
                );

        public static Category CreateCategoryItemFromExamine(SearchResult item, Store store)
        {

            var category = new Category();

            try
            {
                var pathField = item.Fields["path"];

                int parentCategoryId = Convert.ToInt32(item.Fields["parentID"]);

                var examineItemsFromPath = ExamineService.GetAllCatalogItemsFromPath(pathField);

                if (!CatalogService.IsItemDisabled(examineItemsFromPath,store))
                {
                    category.Id = item.Id;
                    category.Title = ExamineService.GetProperty(item, "title", store.Alias);
                    category.Slug = ExamineService.GetProperty(item, "slug", store.Alias);
                    category.Path = pathField;
                    category.ParentCategoryId = parentCategoryId;
                    category.Store = store;
                    category.SortOrder = Convert.ToInt32(item.Fields["sortOrder"]);
                    category.Urls = UrlService.BuildCategoryUrl(category.Slug, examineItemsFromPath, store);
                    category.Level = Convert.ToInt32(item.Fields["level"]);

                    return category;
                } else
                {
                    return null;
                }

            }
            catch (Exception ex)
            {
                Log.Error("Error on creating category item from Examine. Node id: " + item.Id, ex);
                return null;
            }

        }
    }
}
