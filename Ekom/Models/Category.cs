using Ekom.Cache;
using Ekom.Interfaces;
using Ekom.Utilities;
using Examine;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Script.Serialization;
using System.Xml.Serialization;
using Umbraco.Core;
using Umbraco.Core.Composing;
using Umbraco.Core.Models;

namespace Ekom.Models
{
    /// <summary>
    /// Categories are groupings of products, categories can also be nested, f.x.
    /// Women->Winter->Shirts
    /// </summary>
    public class Category : PerStoreNodeEntity, IPerStoreNodeEntity, ICategory
    {
        private IPerStoreCache<ICategory> _categoryCache => Current.Factory.GetInstance<IPerStoreCache<ICategory>>();
        private IPerStoreCache<IProduct> _productCache => Current.Factory.GetInstance<IPerStoreCache<IProduct>>();

        /// <summary>
        /// Short spaceless descriptive title used to create URLs
        /// </summary>
        public string Slug => Properties.GetPropertyValue("slug", base.Store.Alias);

        /// <summary>
        /// Parent umbraco node
        /// </summary>
        public int ParentId { get; set; }

        /// <summary>
        /// All category Urls, computed from stores
        /// </summary>
        public IEnumerable<string> Urls { get; set; }
        /// <summary>
        /// Our eldest ancestor category
        /// </summary>
        public ICategory RootCategory
        {
            get
            {
                return Ancestors().FirstOrDefault();
            }
        }
        /// <summary>
        /// Category Url
        /// </summary>
        public string Url
        {
            get
            {
                //var appCache = ApplicationContext.Current.ApplicationCache;
                //var r = appCache.RequestCache.GetCacheItem("ekmRequest") as ContentRequest;

                //var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(r.DomainPrefix));
                

                var req = Current.Factory.GetInstance<HttpContextBase>().Request;
                var path = req.Url.AbsolutePath;

                var findUrlByPrefix = Urls.FirstOrDefault(x => x.StartsWith(path));

                return findUrlByPrefix ?? Urls.FirstOrDefault();
            }
        }
        /// <summary>
        /// All direct child categories
        /// </summary>
        public IEnumerable<ICategory> SubCategories
        {
            get
            {
                return _categoryCache.Cache[Store.Alias]
                                    .Where(x => x.Value.ParentId == Id)
                                    .Select(x => x.Value)
                                    .OrderBy(x => x.SortOrder);
            }
        }

        /// <summary>
        /// All direct child products of category. (No descendants)
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        public IEnumerable<IProduct> Products
        {
            get
            {
                return _productCache.Cache[Store.Alias]
                                   .Where(x => x.Value.Categories.Any(z => z.Id == Id))
                                   .Select(x => x.Value)
                                   .OrderBy(x => x.SortOrder);
            }
        }

        /// <summary>
        /// All descendant categories, includes grandchild categories
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
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

        /// <summary>
        /// All descendant products of category, this includes child products of sub-categories
        /// </summary>
        [ScriptIgnore]
        [JsonIgnore]
        [XmlIgnore]
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

        /// <summary>
        /// All parent categories, grandparent categories and so on.
        /// </summary>
        /// <returns></returns>
        public IEnumerable<ICategory> Ancestors()
        {
            var list = new List<ICategory>();

            foreach (var id in Path.Split(','))
            {
                if (int.TryParse(id, out int categoryId))
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
        /// Used by Ekom extensions, keep logic empty to allow full customisation of object construction.
        /// </summary>
        /// <param name="store"></param>
        public Category(IStore store) : base(store) { }
        /// <summary>
        /// Construct Category from Examine item
        /// </summary>
        /// <param name="item"></param>
        /// <param name="store"></param>
        public Category(ISearchResult item, IStore store) : base(item, store)
        {
            var pathField = item.Values["__Path"];

            var examineItemsFromPath = NodeHelper.GetAllCatalogItemsFromPath(pathField).ToList();

            ParentId = Convert.ToInt32(item.Values["parentID"]);

            Urls = UrlHelper.BuildCategoryUrls(examineItemsFromPath, store);
        }
        /// <summary>
        /// Construct Category from umbraco publish event
        /// </summary>
        /// <param name="node"></param>
        /// <param name="store"></param>
        public Category(IContent node, IStore store) : base(node, store)
        {
            var pathArray = node.Path.Split(',');

            // Skip Root, Ekom container, Catalog container
            var Ids = pathArray.Skip(3);
            // Skip this category that's being created
            Ids = Ids.Take(Ids.Count() - 1);

            var hierarchy = NodeHelper.GetAllCatalogItemsFromPath(Ids)
                .Select(x => x.GetStoreProperty("slug", store.Alias))
                .ToList();

            ParentId = node.ParentId;

            Urls = UrlHelper.BuildCategoryUrls(Slug, hierarchy, store);
        }
    }
}
