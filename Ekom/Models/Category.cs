using Ekom.Cache;
using Ekom.Helpers;
using Ekom.Interfaces;
using Ekom.Services;
using Ekom.Utilities;
using Examine;
using log4net;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Web;
using System.Web.Script.Serialization;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    public class Category : PerStoreNodeEntity, IPerStoreNodeEntity, ICategory
    {
        private IPerStoreCache<ICategory> _categoryCache
        {
            get
            {
                return Configuration.container.GetInstance<IPerStoreCache<ICategory>>();
            }
        }

        private IPerStoreCache<IProduct> _productCache
        {
            get
            {
                return Configuration.container.GetInstance<IPerStoreCache<IProduct>>();
            }
        }

        /// <summary>
        /// Short spaceless descriptive title used to create URLs
        /// </summary>
        public string Slug
        {
            get
            {
                return Properties.GetPropertyValue("slug", base.Store.Alias);
            }
        }
        public int ParentCategoryId { get; set; }

        public IEnumerable<string> Urls { get; set; }
        public ICategory RootCategory
        {
            get
            {
                return Ancestors().FirstOrDefault();
            }
        }
        public string Url
        {
            get
            {
                //var appCache = ApplicationContext.Current.ApplicationCache;
                //var r = appCache.RequestCache.GetCacheItem("ekmRequest") as ContentRequest;

                //var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));

                var path = HttpContext.Current.Request.Url.AbsolutePath;
                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(path));

                return findUrlByPrefix ?? Urls.FirstOrDefault();
            }
        }
        public IEnumerable<ICategory> SubCategories
        {
            get
            {
                return _categoryCache.Cache[Store.Alias]
                                    .Where(x => x.Value.ParentCategoryId == Id)
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder);
            }
        }

        [ScriptIgnore]
        [JsonIgnore]
        public IEnumerable<IProduct> Products
        {
            get
            {
                return _productCache.Cache[Store.Alias]
                                   .Where(x => x.Value.Categories().Any(z => z.Id == Id))
                                   .Select(x => x.Value)
                                   .OrderBy(x => x.SortOrder);
            }
        }

        [ScriptIgnore]
        [JsonIgnore]
        public IEnumerable<ICategory> SubCategoriesRecursive
        {
            get
            {
                return _categoryCache.Cache[Store.Alias]
                                    .Where(x => x.Value.Level > Level &&
                                                x.Value.Path.Split(',').Contains(Id.ToString()))
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder);
            }
        }

        [ScriptIgnore]
        [JsonIgnore]
        public IEnumerable<IProduct> ProductsRecursive
        {
            get
            {
                return _categoryCache.Cache[Store.Alias]
                                    .Where(x => x.Value.Level >= Level &&
                                                x.Value.Path.Split(',').Contains(Id.ToString()))
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder)
                                    .SelectMany(x => x.Products);
            }
        }

        public IEnumerable<ICategory> Ancestors()
        {
            var list = new List<ICategory>();

            foreach (var id in Path.Split(','))
            {
                int categoryId;

                if (int.TryParse(id, out categoryId))
                {

                    var category = _categoryCache.Cache[Store.Alias]
                        .FirstOrDefault(x => x.Value.Id == categoryId);

                    if (category.Value != null)
                    {
                        list.Add(category.Value);
                    }

                }
            }

            return list;

        }

        /// <summary>
        /// Used by Ekom extensions
        /// </summary>
        /// <param name="store"></param>
        public Category(IStore store) : base(store) { }
        public Category(SearchResult item, IStore store) : base(item, store)
        {
            var pathField = item.Fields["path"];

            var examineItemsFromPath = NodeHelper.GetAllCatalogItemsFromPath(pathField);

            ParentCategoryId = Convert.ToInt32(item.Fields["parentID"]);

            Urls = UrlService.BuildCategoryUrls(examineItemsFromPath, store);
        }
        public Category(IContent node, IStore store) : base(node, store)
        {
            var pathField = node.Path;
            var examineItemsFromPath = NodeHelper.GetAllCatalogItemsFromPath(pathField);

            ParentCategoryId = node.ParentId;

            Urls = UrlService.BuildCategoryUrls(examineItemsFromPath, store);
        }


        private static readonly ILog Log =
            LogManager.GetLogger(
                MethodBase.GetCurrentMethod().DeclaringType
            );
    }
}
