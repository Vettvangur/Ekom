using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Umbraco.Core;
using Umbraco.Core.Models;
using uWebshop.Cache;
using uWebshop.Helpers;
using uWebshop.Services;

namespace uWebshop.Models
{
    public class Category
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Slug { get; set; }
        public string Path { get; set; }
        public int ParentCategoryId { get; set; }
        public Store Store { get; set; }
        public int SortOrder { get; set; }
        public IEnumerable<string> Urls { get; set; }
        public int Level { get; set; }
        public string Url
        {
            get
            {
                var appCache = ApplicationContext.Current.ApplicationCache;
                var r = appCache.RequestCache.GetCacheItem("uwbsRequest") as ContentRequest;

                var defaulUrl = Urls.FirstOrDefault();
                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));

                if (findUrlByPrefix != null)
                {
                    return findUrlByPrefix;
                }

                return defaulUrl;
            }
        }
        public IEnumerable<Category> SubCategories
        {
            get
            {
                return CategoryCache.Cache[Store.Alias]
                                    .Where(x => x.Value.ParentCategoryId == Id)
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder);
            }
        }
        public IEnumerable<Product> Products
        {
            get
            {
                return ProductCache.Cache[Store.Alias]
                                   .Where(x => x.Value.Categories.Any(z => z.Id == Id))
                                   .Select(x => x.Value)
                                   .OrderBy(x => x.SortOrder);
            }
        }

        public IEnumerable<Category> SubCategoriesRecursive
        {
            get
            {
                return CategoryCache.Cache[Store.Alias]
                                    .Where(x => x.Value.Level > Level &&
                                                x.Value.Path.Split(',').Contains(Id.ToString()))
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder);
            }
        }

        public IEnumerable<Product> ProductsRecursive
        {
            get
            {
                return CategoryCache.Cache[Store.Alias]
                                    .Where(x => x.Value.Level >= Level &&
                                                x.Value.Path.Split(',').Contains(Id.ToString()))
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder)
                                    .SelectMany(x => x.Products);
            }
        }

        public Category() : base() { }
        public Category(SearchResult item, Store store)
        {
            var pathField = item.Fields["path"];

            int parentCategoryId = Convert.ToInt32(item.Fields["parentID"]);

            var examineItemsFromPath = NodeHelper.GetAllCatalogItemsFromPath(pathField);

            Id               = item.Id;
            Path             = pathField;
            ParentCategoryId = parentCategoryId;
            Store            = store;

            Title            = item.GetStoreProperty("title", store.Alias);
            Slug             = item.GetStoreProperty("slug", store.Alias);

            SortOrder        = Convert.ToInt32(item.Fields["sortOrder"]);
            Level            = Convert.ToInt32(item.Fields["level"]);

            Urls             = UrlService.BuildCategoryUrl(Slug, examineItemsFromPath, store);
        }
        public Category(IContent node, Store store)
        {
            var pathField = node.Path;

            int parentCategoryId = node.ParentId;

            var examineItemsFromPath = NodeHelper.GetAllCatalogItemsFromPath(pathField);

            Id               = node.Id;
            Path             = pathField;
            ParentCategoryId = parentCategoryId;
            Store            = store;

            Title = node.GetStoreProperty("title", store.Alias);
            Slug  = node.GetStoreProperty("slug", store.Alias);

            SortOrder = node.SortOrder;
            Level     = node.Level;

            Urls = UrlService.BuildCategoryUrl(Slug, examineItemsFromPath, store);
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
