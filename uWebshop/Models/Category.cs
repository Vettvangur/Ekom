using Examine;
using log4net;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using uWebshop.Cache;
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
        public IEnumerable<String> Urls { get; set; }
        public int Level { get; set; }
        public string Url
        {
            get
            {
                var r = (ContentRequest)HttpContext.Current.Cache["uwbsRequest"];

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
                return CategoryCache.Instance._cache.Where(x => x.Value.ParentCategoryId == Id && x.Value.Store.Alias == Store.Alias).OrderBy(x => x.Value.SortOrder).Select(x => x.Value);
            }
        }
        public IEnumerable<Product> Products
        {
            get
            {
                return ProductCache.Instance._cache.Where(x => x.Value.Categories.Any(z => z.Id == Id) && x.Value.Store.Alias == Store.Alias).OrderBy(x => x.Value.SortOrder).Select(x => x.Value);
            }
        }

        public IEnumerable<Category> SubCategoriesRecursive
        {
            get
            {
                return CategoryCache.Instance._cache.Where(x => x.Value.Path.Split(',').Contains(Id.ToString()) && x.Value.Level > Level && x.Value.Store.Alias == Store.Alias).OrderBy(x => x.Value.SortOrder).Select(x => x.Value);
            }
        }

        public IEnumerable<Product> ProductsRecursive
        {
            get
            {
                var categories = CategoryCache.Instance._cache.Where(x => x.Value.Path.Split(',').Contains(Id.ToString()) && x.Value.Level >= Level && x.Value.Store.Alias == Store.Alias).OrderBy(x => x.Value.SortOrder).Select(x => x.Value);

                var products = categories.SelectMany(x => x.Products);

                return products;
            }
        }

        public Category() : base() { }
        public Category(SearchResult item, Store store)
        {
            try
            {
                var pathField = item.Fields["path"];

                int parentCategoryId = Convert.ToInt32(item.Fields["parentID"]);

                var examineItemsFromPath = ExamineService.GetAllCatalogItemsFromPath(pathField);

                if (!CatalogService.IsItemDisabled(examineItemsFromPath, store))
                {
                    Id               = item.Id;
                    Path             = pathField;
                    ParentCategoryId = parentCategoryId;
                    Store            = store;

                    Title            = ExamineService.GetProperty(item, "title", store.Alias);
                    Slug             = ExamineService.GetProperty(item, "slug", store.Alias);

                    SortOrder        = Convert.ToInt32(item.Fields["sortOrder"]);
                    Level            = Convert.ToInt32(item.Fields["level"]);

                    Urls             = UrlService.BuildCategoryUrl(Slug, examineItemsFromPath, store);

                    return;
                }
                else
                {
                    throw new Exception("Error, Catalog is disabled");
                }
            }
            catch (Exception ex)
            {
                Log.Error("Error on creating category item from Examine. Node id: " + item.Id, ex);
                throw;
            }
        }

        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
